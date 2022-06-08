import json
import math
import sys
import pandas


class Costs():
    def __init__(self, model_path: str):
        # parse model
        m = json.load(open(model_path))
        self.m = m

        # parse costs
        self.alu_costs = {
            "Addf32": {
                "Static": 15,
                "Area": 2060,
                "Dynamic": 7.701E-12
            },
            "Subf32": {
                "Static": 15,
                "Area": 2060,
                "Dynamic": 7.701E-12
            },
            "Multf32": {
                "Static": 44.8,
                "Area": 2060,
                "Dynamic": 7.701E-12
            },
            "Cmpf32": {
                "Dynamic": 7.701E-12
            }
        }

        def addr(x):
            return math.ceil(math.log2(x))

        def mem_area(bits):
            return 0.4586 * bits + 12652

        def mem_leakage(bits):
            return (8E-05 * bits + 1.822) * 1E-6

        def mem_dyn_read(bits, word_size):
            return (0.0000331817313*bits+0.200534285*word_size+3.70946309)*1E-12

        def mem_dyn_write(bits, word_size):
            return (0.0000467955605*bits+0.305233644*word_size+3.23205817)*1E-12

        self.width = m["NoC"]["Width"]
        self.height = m["NoC"]["Height"]
        self.size = self.width * self.height
        feedback = 1
        v_wolkotte = 1.1
        v_simulator = 1.2
        self.period = 1E4

        # address size calculations
        dx = addr(self.width)
        dy = addr(self.height)
        neuron_bits = addr(m["MaxNeurons"])
        syn_bits = addr(m["MaxSynapses"])
        layer_bits = addr(m["MaxLayers"])
        packet_disc = addr(m["NrPacketTypes"])

        # packet memory
        self.spike_packet = packet_disc + layer_bits + neuron_bits + dx + dy + feedback
        self.sync_packet = packet_disc + dx + dy
        self.sync_done_packet = packet_disc + dx + dy
        self.packet_size = max(
            self.spike_packet, self.sync_packet, self.sync_done_packet)

        # core memory
        self.neuron_mem_width = m["NrParallel"] * m["NeuronSize"]
        self.neuron_mem = m["MaxNeurons"] * self.neuron_mem_width
        self.syn_mem_width = m["NrParallel"] * m["SynapseSize"]
        self.syn_mem = m["MaxSynapses"] * self.syn_mem_width
        split_mem = dx + dy + layer_bits + feedback
        self.layer_mem_width = (m["BaseLayerSize"] + neuron_bits + 2 * neuron_bits + 4 *
                                syn_bits + 2 * m["MaxSplits"] * split_mem)
        self.layer_mem = m["MaxLayers"] * self.layer_mem_width
        self.output_mem_width = self.packet_size
        self.output_mem = self.output_mem_width * m["OutputBufferDepth"]
        end = 1
        self.compute_mem_width = neuron_bits + layer_bits + feedback + end
        self.compute_mem = self.compute_mem_width * (2 * m["MaxFanIn"] + 1)
        self.core_mem = self.neuron_mem + self.syn_mem + \
            self.layer_mem + self.output_mem + self.compute_mem

        # router memory
        self.router_input_mem = m["NoC"]["InputSize"] * self.packet_size
        self.router_output_mem = m["NoC"]["OutputSize"] * self.packet_size

        # core area
        self.neuron_area = mem_area(self.neuron_mem)
        self.syn_area = mem_area(self.syn_mem)
        self.layer_area = mem_area(self.layer_mem)
        self.output_area = mem_area(self.output_mem)
        self.compute_area = mem_area(self.compute_mem)
        self.alu_area = {}
        for name, amount in m["CoreALU"].items():
            self.alu_area[name] = amount * self.alu_costs[name]["Area"]
        self.alu_area_total = sum(v for _, v in self.alu_area.items())
        core_mem_area = self.neuron_area + self.syn_area + \
            self.layer_area + self.output_area + self.compute_area
        self.core_area = core_mem_area + self.alu_area_total

        # router area
        self.router_input_area = mem_area(self.router_input_mem)
        self.router_output_area = mem_area(self.output_mem)
        self.router_area = 5 * self.router_input_area + 5 * self.router_output_area
        self.chip_area = self.core_area * self.size + self.router_area * self.size

        # Static: In watts
        self.core_dynamic = mem_leakage(self.core_mem)
        self.neuron_static = mem_leakage(self.neuron_mem)
        self.layer_static = mem_leakage(self.layer_mem)
        self.syn_static = mem_leakage(self.syn_mem)
        self.compute_static = mem_leakage(self.compute_mem)
        self.output_static = mem_leakage(self.output_mem)
        self.core_mem_static = (self.neuron_static + self.layer_static + self.syn_static +
                                self.compute_static + self.output_static) * v_wolkotte
        self.alu_static = {}
        for name, amount in m["CoreALU"].items():
            self.alu_static[name] = amount * \
                self.alu_costs[name]["Static"] / 1E6
        self.alu_static_total = sum(v for _, v in self.alu_static.items())
        self.core_static = self.core_mem_static + self.alu_static_total
        self.chip_static = (self.size - 1) * self.core_static

        # Dynamic: Depends on memory + layer operations using Aladdin
        l = (self.core_area/1E6)**(0.5)  # calculate dimensions of cores
        technology = v_wolkotte**2 / v_simulator**2
        self.router_dyn_bit = 0.98 * technology * 1E-12
        self.link_dyn_bit = (0.39 + 0.12*l) * technology * 1E-12
        self.router_dyn_packet = self.router_dyn_bit * \
            self.packet_size  # Depends on formula from Wolkotte
        self.link_dyn_packet = self.link_dyn_bit * self.packet_size

        # memory energies
        nr_parallel = m["NrParallel"]
        self.layer_mem_read = mem_dyn_read(
            self.layer_mem, self.layer_mem_width)
        self.layer_mem_write = mem_dyn_write(
            self.layer_mem, self.layer_mem_width)
        self.neuron_mem_read = mem_dyn_read(
            self.neuron_mem, self.neuron_mem_width) / nr_parallel
        self.neuron_mem_write = mem_dyn_write(
            self.neuron_mem, self.neuron_mem_width) / nr_parallel
        self.syn_mem_read = mem_dyn_read(
            self.syn_mem, self.syn_mem_width) / nr_parallel
        self.syn_mem_write = mem_dyn_write(
            self.syn_mem, self.syn_mem_width) / nr_parallel

        # Buffer energies
        self.compute_buf_pops = mem_dyn_read(
            self.compute_mem, self.compute_mem_width)
        self.compute_buf_pushes = mem_dyn_write(
            self.compute_mem, self.compute_mem_width)
        self.output_buf_pops = mem_dyn_read(
            self.output_mem, self.output_mem_width)
        self.output_buf_pushes = mem_dyn_write(
            self.output_mem, self.output_mem_width)

        # should be in PS
        noc = m["NoC"]
        self.router_transfer_delay = noc["TransferDelay"] if "NrDataWires" not in noc \
            else math.ceil(self.packet_size / noc["NrDataWires"]) * noc["TransferDelay"]

    def print_summary(self):
        print(f"Core memory:")
        print(
            f"  Neuron: {self.neuron_mem:,} bits (Width: {self.neuron_mem_width} bits)")
        print(
            f"  Syn: {self.syn_mem:,} bits (Width: {self.syn_mem_width} bits)")
        print(
            f"  Layer: {self.layer_mem:,} bits (Width: {self.layer_mem_width} bits)")
        print(
            f"  Output: {self.output_mem:,} bits (Width: {self.output_mem_width} bits)")
        print(
            f"  Compute: {self.compute_mem:,} bits (Width: {self.compute_mem_width} bits)")
        print(f"  Total: {self.core_mem:,} bits")

        print(f"Router memory:")
        print(f"  Input: {self.router_input_mem:,} bits")
        print(f"  Output: {self.router_output_mem:,} bits")

        print(f"Packet memory:")
        print(f"  Spike: {self.spike_packet:,} bits")
        print(f"  Sync: {self.sync_packet:,} bits")
        print(f"  SyncDone: {self.sync_done_packet:,} bits")
        print(f"  Total: {self.packet_size:,} bits")

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
        print(f"      ALU: {self.alu_static_total * 1E6} uW")
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
            f"        Write: {self.neuron_mem_write * 1E12:,.2f} pj / neuron written")
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

    c = Costs(f"res/exp/{expName}/model.json", f"res/exp/{expName}/cost.json")
    c.print_summary()
