import json
import math
import sys


class Costs():
    def __init__(self, model_path: str):
        # parse model
        m = json.load(open(model_path))
        self.m = m

        # parse costs
        self.alu_costs = {
            "Addf32": {
                "Static": 15E-6,  # uW
                "Area": 2060,  # um^2
                "Dynamic": 7.701E-12  # pJ
            },
            "Subf32": {
                "Dynamic": 7.701E-12  # pJ
            },
            "Multf32": {
                "Static": 44.8E-6,  # mW
                "Area": 2060,  # um^2
                "Dynamic": 26.6E-12  # pJ
            },
            "Cmpf32": {
                "Dynamic": 7.701E-12
            }
        }

        self.width = m["NoC"]["Width"]
        self.height = m["NoC"]["Height"]
        self.size = self.width * self.height
        self.nr_cores = self.size - 1
        feedback = 1
        my_voltage = 1.1
        wolkotte_voltage = 1.2
        self.period = 1E4
        neuron_offset_size = 16

        def addr(x):
            return math.ceil(math.log2(x))

        def mem_area(bits):  # um^2
            return 0.4586 * bits + 12652

        def mem_leakage(bits):  # W
            return (8E-05 * bits + 1.822) * my_voltage * 1E-6

        def mem_dyn_read(bits, word_size):  # J
            return (0.0000331817313*bits+0.200534285*word_size+3.70946309)*1E-12

        def mem_dyn_write(bits, word_size):  # J
            return (0.0000467955605*bits+0.305233644*word_size+3.23205817)*1E-12

        # address size calculations
        dx = addr(self.width)
        dy = addr(self.height)
        neuron_bits = addr(m["MaxNeurons"])
        syn_bits = addr(m["MaxSynapses"])
        layer_bits = addr(m["MaxLayers"])
        split_bits = addr(m["MaxSplits"])
        packet_disc = addr(m["NrPacketTypes"])

        # packet memory
        self.spike_packet = packet_disc + dx + dy + layer_bits + neuron_bits + feedback
        self.sync_packet = packet_disc + dx + dy
        self.ready_packet = packet_disc + dx + dy
        self.packet_size = max(
            self.spike_packet, self.sync_packet, self.ready_packet)

        # core memory
        nr_parallel = m["NrParallel"]
        self.neuron_width = m["NeuronStateSize"]
        self.neuron_mem_width = self.neuron_width * nr_parallel
        self.neuron_mem_size = m["MaxNeurons"] * self.neuron_width
        self.syn_width = m["SynpaseStateSize"]
        self.syn_mem_width = self.syn_width * nr_parallel
        self.syn_mem_size = m["MaxSynapses"] * self.syn_width
        split_mem = dx + dy + layer_bits
        self.layer_width = (m["LayerStateSize"] + neuron_offset_size + neuron_bits + neuron_bits +
                            syn_bits + syn_bits + 2 * (m["MaxSplits"] * split_mem + split_bits))
        self.layer_mem_width = self.layer_width
        self.layer_mem_size = m["MaxLayers"] * self.layer_width
        self.output_mem_width = self.packet_size
        self.output_mem_size = self.output_mem_width * m["OutputBufferDepth"]
        self.compute_mem_width = layer_bits + neuron_bits + feedback
        self.compute_mem_size = self.compute_mem_width * (2 * m["MaxFanIn"])
        self.core_mem = self.neuron_mem_size + self.syn_mem_size + \
            self.layer_mem_size + self.output_mem_size + self.compute_mem_size

        # router memory
        self.router_input_mem_size = m["NoC"]["InputSize"] * self.packet_size
        self.router_output_mem_size = m["NoC"]["OutputSize"] * self.packet_size

        # core area: um^2
        self.neuron_area = mem_area(self.neuron_mem_size)
        self.syn_area = mem_area(self.syn_mem_size)
        self.layer_area = mem_area(self.layer_mem_size)
        self.output_area = mem_area(self.output_mem_size)
        self.compute_area = mem_area(self.compute_mem_size)
        core_mem_area = self.neuron_area + self.syn_area + \
            self.layer_area + self.output_area + self.compute_area

        self.alu_area = {}
        for name, amount in m["CoreALU"].items():
            self.alu_area[name] = amount * self.alu_costs[name]["Area"]
        self.alu_area_total = sum(v for _, v in self.alu_area.items())

        self.core_area = core_mem_area + self.alu_area_total

        # router area
        self.router_input_area = mem_area(self.router_input_mem_size)
        self.router_output_area = mem_area(self.router_output_mem_size)
        self.router_area = 5 * self.router_input_area + 5 * self.router_output_area
        self.chip_area = (self.core_area + self.router_area) * self.nr_cores
        self.synaptic_area = self.chip_area / \
            (self.nr_cores * m["MaxSynapses"])

        # Static: W
        self.neuron_static = mem_leakage(self.neuron_mem_size)
        self.layer_static = mem_leakage(self.layer_mem_size)
        self.syn_static = mem_leakage(self.syn_mem_size)
        self.compute_static = mem_leakage(self.compute_mem_size)
        self.output_static = mem_leakage(self.output_mem_size)
        self.core_mem_static = (self.neuron_static + self.layer_static + self.syn_static +
                                self.compute_static + self.output_static)

        self.alu_static = {}
        for name, amount in m["CoreALU"].items():
            self.alu_static[name] = amount * \
                self.alu_costs[name]["Static"]

        self.core_alu_static = sum(v for _, v in self.alu_static.items())
        self.core_static = self.core_mem_static + self.core_alu_static
        self.chip_static = self.core_static * self.nr_cores

        # Dynamic: Depends on memory + layer operations using Aladdin
        l = (self.core_area/1E6)**(0.5)  # calculate dimensions of cores
        technology = my_voltage**2 / wolkotte_voltage**2
        self.router_dyn_bit = 0.98 * technology * 1E-12
        self.link_dyn_bit = (0.39 + 0.12*l) * technology * 1E-12
        self.router_dyn_packet = self.router_dyn_bit * \
            self.packet_size  # Depends on formula from Wolkotte
        self.link_dyn_packet = self.link_dyn_bit * self.packet_size

        # memory energies
        # per layer, per neuron, per synapse energies
        self.layer_mem_read = mem_dyn_read(
            self.layer_mem_size, self.layer_mem_width)
        self.layer_mem_write = mem_dyn_write(
            self.layer_mem_size, self.layer_mem_width)
        self.neuron_mem_read = mem_dyn_read(
            self.neuron_mem_size, self.neuron_mem_width) / nr_parallel
        self.neuron_mem_write = mem_dyn_write(
            self.neuron_mem_size, self.neuron_mem_width) / nr_parallel
        self.syn_mem_read = mem_dyn_read(
            self.syn_mem_size, self.syn_mem_width) / nr_parallel
        self.syn_mem_write = mem_dyn_write(
            self.syn_mem_size, self.syn_mem_width) / nr_parallel

        # Buffer energies
        self.compute_buf_pops = mem_dyn_read(
            self.compute_mem_size, self.compute_mem_width)
        self.compute_buf_pushes = mem_dyn_write(
            self.compute_mem_size, self.compute_mem_width)
        self.output_buf_pops = mem_dyn_read(
            self.output_mem_size, self.output_mem_width)
        self.output_buf_pushes = mem_dyn_write(
            self.output_mem_size, self.output_mem_width)

        # should be in PS
        noc = m["NoC"]
        self.router_transfer_delay = noc["TransferDelay"] if "NrDataWires" not in noc \
            else math.ceil(self.packet_size / noc["NrDataWires"]) * noc["TransferDelay"]

    def print_summary(self):
        print(f"Core memory:")
        print(
            f"  Neuron: {self.neuron_mem_size:,} bits (Width: {self.neuron_width} bits)")
        print(
            f"  Syn: {self.syn_mem_size:,} bits (Width: {self.syn_width} bits)")
        print(
            f"  Layer: {self.layer_mem_size:,} bits (Width: {self.layer_width} bits)")
        print(
            f"  Output: {self.output_mem_size:,} bits (Width: {self.output_mem_width} bits)")
        print(
            f"  Compute: {self.compute_mem_size:,} bits (Width: {self.compute_mem_width} bits)")
        print(f"  Total: {self.core_mem:,} bits")

        print(f"Router memory:")
        print(f"  Input: {self.router_input_mem_size:,} bits")
        print(f"  Output: {self.router_output_mem_size:,} bits")

        print(f"Packet memory:")
        print(f"  Spike: {self.spike_packet:,} bits")
        print(f"  Sync: {self.sync_packet:,} bits")
        print(f"  SyncDone: {self.ready_packet:,} bits")
        print(f"  Total: {self.packet_size:,} bits")

        print(f"Synpatic area: {self.synaptic_area:.2f}")

        print(f"Area:")
        print(
            f"  Core: {self.core_area:,} um^2 ({self.core_area*1E-6:,.2f} mm^2)")
        print(
            f"    Neuron mem: {self.neuron_area:,.2f} um^2 ({self.neuron_area*1E-6:,.2f} mm^2)")
        print(
            f"    Synapse mem: {self.syn_area:,.2f} um^2 ({self.syn_area*1E-6:,.2f} mm^2)")
        print(
            f"    Layer mem: {self.layer_area:,.2f} um^2 ({self.layer_area*1E-6:,.2f} mm^2)")
        print(
            f"    Compute buffer: {self.compute_area:,.2f} um^2 ({self.compute_area*1E-6:,.2f} mm^2)")
        print(
            f"    Output buffer: {self.output_area:,.2f} um^2 ({self.output_area*1E-6:,.2f} mm^2)")
        print(
            f"    ALU: {self.alu_area_total:,.2f} um^2 ({self.alu_area_total*1E-6:,.2f} mm^2)")
        for name, power in self.alu_area.items():
            print(f"      {name}: {power} um^2 ({power*1E-6:,.2f} mm^2)")
        print(
            f"  Router: {self.router_area:,.2f} um^2 ({self.router_area*1E-6:,.2f} mm^2)")
        print(
            f"    Input: {self.router_input_area:,.2f} um^2 ({self.router_input_area*1E-6:,.2f} mm^2)")
        print(
            f"    Output: {self.router_output_area:,.2f} um^2 ({self.router_output_area*1E-6:,.2f} mm^2)")
        print(
            f"  Chip: {self.chip_area:,.2f} um^2 ({self.chip_area*1E-6:,.2f} mm^2)")

        print(f"Static power:")
        print(f"  Chip: {self.chip_static*1E6:,.2f} uW")
        print(f"    Core: {self.core_static*1E6:,.2f} uW")
        print(f"      Mem: {self.core_mem_static*1E6:,.2f} uW")
        print(f"        Syn: {self.syn_static*1E6:,.2f} uW")
        print(f"        Neuron: {self.neuron_static*1E6:,.2f} uW")
        print(f"        Layer: {self.layer_static*1E6:,.2f} uW")
        print(f"        Compute buffer: {self.compute_static*1E6:,.2f} uW")
        print(f"        Output bufffer: {self.output_static*1E6:,.2f} uW")
        print(f"      ALU: {self.core_alu_static * 1E6} uW")
        for name, power in self.alu_static.items():
            print(f"        {name}: {power*1E6} uW")

        print(f"Dynamic energy:")
        print(f"  Router:")
        print(
            f"    Hop: {self.router_dyn_bit * 1E12:,.2f} pJ/b ({self.router_dyn_packet * 1E12:,.2f} pJ/packet)")
        print(
            f"    Switch: {self.link_dyn_bit * 1E12:,.2f} pJ/b ({self.link_dyn_packet * 1E12:,.2f} pJ/packet)")
        print(f"  Core:")
        print(f"    Memories:")
        print(f"      Layer:")
        print(f"        Read: {self.layer_mem_read * 1E12:,.2f} pJ / read")
        print(f"        Write: {self.layer_mem_write * 1E12:,.2f} pJ / write")
        print(f"      Neuron:")
        print(
            f"        Read: {self.neuron_mem_read * 1E12:,.2f} pJ / neuron read")
        print(
            f"        Write: {self.neuron_mem_write * 1E12:,.2f} pJ / neuron written")
        print(f"      Synapse:")
        print(f"        Read: {self.syn_mem_read * 1E12:,.2f} pJ / syn read")
        print(
            f"        Write: {self.syn_mem_write * 1E12:,.2f} pJ / syn written")
        print(f"    Buffers:")
        print(f"      Compute:")
        print(f"        Pop: {self.compute_buf_pops * 1E12:,.2f} pJ / pop")
        print(f"        Push: {self.compute_buf_pushes * 1E12:,.2f} pJ / push")
        print(f"      Output:")
        print(f"        Pop: {self.output_buf_pops * 1E12:,.2f} pJ / pop")
        print(f"        Push: {self.output_buf_pushes * 1E12:,.2f} pJ / push")
        print(f"Delays:")
        print(
            f"  Packet transfer: {self.router_transfer_delay:,} ps")
        for layer, values in self.m["LayerDelays"].items():
            print(f"  {layer}:")
            print(
                f"    Sync: {values['SyncII']:,} ps II, {values['SyncLat']:,} ps Lat")
            print(
                f"    Integrate: {values['IntegrateII']:,} ps II, {values['IntegrateLat']:,} ps Lat")


if __name__ == "__main__":
    expName = sys.argv[1]

    c = Costs(f"res/exp/{expName}/model.json")
    c.print_summary()
