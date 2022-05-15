import json
import math
import pandas

class Stats():
    def __init__(self, model_path: str, cost_path: str):
        # parse model
        m = json.load(open(model_path))
        self.m = m

        # parse costs
        cost = json.load(open(cost_path))
        self.cost = cost

        alu_ops = cost["ALU"]["Ops"]
        alu_units = cost["ALU"]["Units"]

        def addr(x):
            return math.ceil(math.log2(x))


        width = m["NoC"]["Width"]
        height = m["NoC"]["Height"]
        size = width * height
        feedback = 1
        voltage = 1.1
        self.period = 10

        # address size calculations
        dx = addr(width)
        dy = addr(height)
        neuron_bits = addr(m["MaxNeurons"])
        syn_bits = addr(m["MaxSynapses"])
        layer_bits = addr(m["MaxLayers"])
        packet_disc = addr(m["NrPacketTypes"])


        # packet memory
        self.spike_packet = packet_disc + layer_bits + neuron_bits + dx + dy + feedback
        self.sync_packet = packet_disc + dx + dy
        self.sync_done_packet = packet_disc + dx + dy
        self.packet_size = max(self.spike_packet, self.sync_packet, self.sync_done_packet)

        # core memory
        self.neuron_mem_width = m["NeuronSize"]
        self.neuron_mem = m["MaxNeurons"] * self.neuron_mem_width
        self.syn_mem_width = m["SynapseSize"]
        self.syn_mem = m["MaxSynapses"] * self.syn_mem_width
        split_mem = dx + dy + layer_bits + feedback
        self.layer_mem_width = (4 * neuron_bits + 2 * layer_bits + m["MaxSplits"] * split_mem)
        self.layer_mem = m["MaxLayers"] * self.layer_mem_width
        self.output_mem = self.packet_size * m["OutputBufferDepth"]
        end = 1
        self.compute_mem = (neuron_bits + layer_bits + feedback + end) * m["MaxFanIn"]
        self.core_mem = self.neuron_mem + self.syn_mem + self.layer_mem + self.output_mem + self.compute_mem

        # router memory
        self.router_input_mem = m["NoC"]["InputSize"] * self.packet_size
        self.router_output_mem = m["NoC"]["OutputSize"] * self.packet_size

        def calc_area(bits):
            return 0.467 * bits + 16204


        # core area
        self.neuron_area = calc_area(self.neuron_mem)
        self.syn_area = calc_area(self.syn_mem)
        self.layer_area = calc_area(self.layer_mem)
        self.output_area = calc_area(self.output_mem)
        self.compute_area = calc_area(self.compute_mem)
        self.alu_area = {}
        for name, amount in m["CoreALU"].items():
            self.alu_area[name] = amount * alu_units[name]["Area"]
        self.alu_area_total = sum(v for _, v in self.alu_area.items())
        core_mem_area = self.neuron_area + self.syn_area + \
            self.layer_area + self.output_area + self.compute_area
        self.core_area = core_mem_area + self.alu_area_total

        # router area
        self.router_input_area = calc_area(self.router_input_mem)
        self.router_output_area = calc_area(self.output_mem)
        self.router_area = 5 * self.router_input_area + 5 * self.router_output_area
        self.chip_area = self.core_area * size + self.router_area * size


        def mem_static(bits):
            return (8E-05 * bits + 1.6029) * 1E-6


        # Static: In watts
        self.core_dynamic = mem_static(self.core_mem)
        self.neuron_static = mem_static(self.neuron_mem)
        self.layer_static = mem_static(self.layer_mem)
        self.syn_static = mem_static(self.syn_mem)
        self.compute_static = mem_static(self.compute_mem)
        self.output_static = mem_static(self.output_mem)
        self.core_mem_static = (self.neuron_static + self.layer_static + self.syn_static +
                        self.compute_static + self.output_static) * voltage
        self.chip_static = size * self.core_mem_static
        self.alu_static = {}
        for name, amount in m["CoreALU"].items():
            self.alu_static[name] = amount * alu_units[name]["Static"] / 1E6
        self.alu_static_total = sum(v for _, v in self.alu_static.items())
        self.core_static = self.core_mem_static + self.alu_static_total

        # Dynamic: Depends on memory + layer operations using Aladdin
        l = (self.core_area/1E6)**(0.5)  # calculate dimensions of cores
        technology = voltage**2 / 1.2**2
        self.wolkotte_mesh = (1.37 + 0.12 * l) * technology
        self.router_dyn = self.wolkotte_mesh  # Depends on formula from Wolkotte

        # TODO: Find better formulas for memory reads
        def dynamic_read_sram(bits, word_size):
            return ((0.012 * bits**(0.5) + 4.61) / 16 * word_size) / 1E12

        def dynamic_write_sram(bits, word_size):
            return ((0.012 * bits**(0.5) + 4.61) / 16 * word_size) / 1E12

        # memory energies
        self.layer_mem_read = dynamic_read_sram(self.layer_mem, self.layer_mem_width)
        self.layer_mem_write = dynamic_write_sram(self.layer_mem, self.layer_mem_width)
        self.neuron_mem_read = dynamic_read_sram(self.neuron_mem, self.neuron_mem_width)
        self.neuron_mem_write = dynamic_write_sram(self.neuron_mem, self.neuron_mem_width)
        self.syn_mem_read = dynamic_read_sram(self.syn_mem, self.syn_mem_width)
        self.syn_mem_write = dynamic_write_sram(self.syn_mem, self.syn_mem_width)

        self.layer_energies = {}
        for layer, layer_values in m["LayerOperations"].items():
            integrate = 0.0
            sync = 0.0
            for unit, amount in layer_values["Integrate"].items():
                integrate += amount * alu_ops[unit]

            for unit, amount in layer_values["Sync"].items():
                sync += amount * alu_ops[unit]
            
            self.layer_energies[layer] = {
                "Integrate": integrate,
                "Sync": sync
            }

        self.router_transfer_delay = self.packet_size / m["NoC"]["DataWires"]

    def print_summary(self):
        print(f"Core memory:")
        print(f"  Neuron: {self.neuron_mem:,} bits")
        print(f"  Syn: {self.syn_mem:,} bits")
        print(f"  Layer: {self.layer_mem:,} bits")
        print(f"  Output: {self.output_mem:,} bits")
        print(f"  Compute: {self.compute_mem:,} bits")
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
        print(f"  Core: {self.core_area:,} um^2 ({self.core_area / 1000000:,} mm^2)")
        print(f"    Neuron mem: {self.neuron_area:,} um^2 ({self.neuron_area / 1000000:,} mm^2)")
        print(f"    Synapse mem: {self.syn_area:,} um^2 ({self.syn_area / 1000000:,} mm^2)")
        print(f"    Layer mem: {self.layer_area:,} um^2 ({self.layer_area / 1000000:,} mm^2)")
        print(
            f"    Compute buffer: {self.compute_area:,} um^2 ({self.compute_area / 1000000:,} mm^2)")
        print(
            f"    Output buffer: {self.output_area:,} um^2 ({self.output_area / 1000000:,} mm^2)")
        print(f"    ALU: {self.alu_area_total:,} um^2 ({self.alu_area_total / 1000000:,} mm^2)")
        for name, power in self.alu_area.items():
            print(f"      {name}: {power} um^2 ({power / 10E6:,} mm^2)")
        print(f"  Router: {self.router_area:,} um^2 ({self.router_area / 1000000:,} mm^2)")
        print(
            f"    Input: {self.router_input_area:,} um^2 ({self.router_input_area / 1000000:,} mm^2)")
        print(
            f"    Output: {self.router_output_area:,} um^2 ({self.router_output_area / 1000000:,} mm^2)")
        print(f"  Chip: {self.chip_area:,} um^2 ({self.chip_area / 1000000:,} mm^2)")

        print(f"Static power:")
        print(f"  Chip: {self.chip_static*1E6:,} uW")
        print(f"    Core: {self.core_static*1E6:,} uW")
        print(f"      Mem: {self.core_mem_static*1E6:,} uW")
        print(f"        Syn: {self.syn_static*1E6:,} uW")
        print(f"        Neuron: {self.neuron_static*1E6:,} uW")
        print(f"        Layer: {self.layer_static*1E6:,} uW")
        print(f"        Compute buffer: {self.compute_static*1E6:,} uW")
        print(f"        Output bufffer: {self.output_static*1E6:,} uW")
        print(f"      ALU:")
        for name, power in self.alu_static.items():
            print(f"        {name}: {power*1E6} uW")

        print(f"Dynamic energy:")
        print(f"  Router:")
        print(
            f"    Hop: {self.wolkotte_mesh} pJ/b ({self.wolkotte_mesh * self.packet_size} pJ/packet)")
        print(f"  Core:")
        print(f"    Memories:")
        print(f"      Layer:")
        print(f"        Read: {self.layer_mem_read * 1E12} pJ / read")
        print(f"        Write: {self.layer_mem_write * 1E12} pJ / write")
        print(f"      Neuron:")
        print(f"        Read: {self.neuron_mem_read * 1E12} pJ / read")
        print(f"        Write: {self.neuron_mem_write * 1E12} pj / write")
        print(f"      Synapse:")
        print(f"        Read: {self.syn_mem_read * 1E12} pJ / read")
        print(f"        Write: {self.syn_mem_write * 1E12} pJ / write")
        print(f"    Operations:")
        for layer, values in self.layer_energies.items():
            print(f"      {layer}:")
            print(f"        Integrate: {values['Integrate'] * 1E12} pJ / Axon")
            print(f"        Sync: {values['Sync'] * 1E12} pJ / neuron")
        print(f"Delays:")
        print(f"  Packet transfer: {self.router_transfer_delay} cycles ({self.router_transfer_delay * self.period} ps)")
        for layer, values in self.m["LayerDelays"].items():
            print(f"  {layer}:")
            print(f"    Sync: {values['SyncII']} cycles II, {values['SyncLat']} cycles Lat")
            print(f"    Integrate: {values['IntegrateII']} cycles II, {values['IntegrateLat']} cycles Lat")

# TODO: Find timing for router transfers
# TODO: Find timing for layer computations
# TODO: Find dynamic energy for output
# TODO: Find layer for dynamic values for SRAM read and write assuming
# TODO: Add bus model
# TODO: Add code for model_metrics analysis
# TODO: Move more stuff to cost file

if __name__ == "__main__":
    s = Stats("res/exp/exp1/model.json", "Scripts\cost.json")
    s.print_summary()