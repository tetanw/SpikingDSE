import json
import math
import pandas

# parse model
m = json.load(open("res/exp/exp1/model.json"))

# parse costs
cost = json.load(open("Scripts/cost.json"))
alu_ops = cost["ALU"]["Ops"]
alu_units = cost["ALU"]["Units"]

def addr(x):
    return math.ceil(math.log2(x))


width = m["NoC"]["Width"]
height = m["NoC"]["Height"]
size = width * height
feedback = 1
voltage = 1.1
period = 10

# address size calculations
dx = addr(width)
dy = addr(height)
neuron_bits = addr(m["MaxNeurons"])
syn_bits = addr(m["MaxSynapses"])
layer_bits = addr(m["MaxLayers"])
packet_disc = addr(m["NrPacketTypes"])


# packet memory
spike_packet = packet_disc + layer_bits + neuron_bits + dx + dy + feedback
sync_packet = packet_disc + dx + dy
sync_done_packet = packet_disc + dx + dy
packet_size = max(spike_packet, sync_packet, sync_done_packet)

# core memory
neuron_mem_width = m["NeuronSize"]
neuron_mem = m["MaxNeurons"] * neuron_mem_width
syn_mem_width = m["SynapseSize"]
syn_mem = m["MaxSynapses"] * syn_mem_width
split_mem = dx + dy + layer_bits + feedback
layer_mem_width = (4 * neuron_bits + 2 * layer_bits + m["MaxSplits"] * split_mem)
layer_mem = m["MaxLayers"] * layer_mem_width
output_mem = packet_size * m["OutputBufferDepth"]
end = 1
compute_mem = (neuron_bits + layer_bits + feedback + end) * m["MaxFanIn"]
core_mem = neuron_mem + syn_mem + layer_mem + output_mem + compute_mem

# router memory
router_input_mem = m["NoC"]["InputSize"] * packet_size
router_output_mem = m["NoC"]["OutputSize"] * packet_size


def calc_area(bits):
    return 0.467 * bits + 16204


# core area
neuron_area = calc_area(neuron_mem)
syn_area = calc_area(syn_mem)
layer_area = calc_area(layer_mem)
output_area = calc_area(output_mem)
compute_area = calc_area(compute_mem)
alu_area = {}
for name, amount in m["CoreALU"].items():
    alu_area[name] = amount * alu_units[name]["Area"]
alu_area_total = sum(v for _, v in alu_area.items())
core_mem_area = neuron_area + syn_area + \
    layer_area + output_area + compute_area
core_area = core_mem_area + alu_area_total

# router area
router_input_area = calc_area(router_input_mem)
router_output_area = calc_area(output_mem)
router_area = 5 * router_input_area + 5 * router_output_area
chip_area = core_area * size + router_area * size


def mem_static(bits):
    return (8E-05 * bits + 1.6029) * 10E-6


# Static: Depends on memory + layer units
core_dynamic = mem_static(core_mem)
neuron_static = mem_static(neuron_mem)
layer_static = mem_static(layer_mem)
syn_static = mem_static(syn_mem)
compute_static = mem_static(compute_mem)
output_static = mem_static(output_mem)
core_mem_static = (neuron_static + layer_static + syn_static +
                   compute_static + output_static) * voltage
router_mem_static = 5 * \
    mem_static(router_input_mem) + 5 * mem_static(router_output_mem)
chip_static = size * core_mem_static + size * router_mem_static
alu_static = {}
for name, amount in m["CoreALU"].items():
    alu_static[name] = amount * alu_units[name]["Static"] / 1E6
alu_static_total = sum(v for _, v in alu_static.items())
core_static = core_mem_static + alu_static_total

# Dynamic: Depends on memory + layer operations using Aladdin
l = (core_area/1000000)**(0.5)  # calculate dimensions of meory
technology = voltage**2 / 1.2**2
wolkotte_mesh = (1.37 + 0.12 * l) * technology
router_dyn = wolkotte_mesh  # Depends on formula from Wolkotte

# TODO: Find better formulas for memory reads
def dynamic_read_sram(bits, word_size):
    return (0.012 * bits**(0.5) + 4.61) / 16 * word_size

def dynamic_write_sram(bits, word_size):
    return (0.012 * bits**(0.5) + 4.61) / 16 * word_size

# memory energies
layer_mem_read = dynamic_read_sram(layer_mem, layer_mem_width)
layer_mem_write = dynamic_write_sram(layer_mem, layer_mem_width)
neuron_mem_read = dynamic_read_sram(neuron_mem, neuron_mem_width)
neuron_mem_write = dynamic_write_sram(neuron_mem, neuron_mem_width)
syn_mem_read = dynamic_read_sram(syn_mem, syn_mem_width)
syn_mem_write = dynamic_write_sram(syn_mem, syn_mem_width)

layer_energies = {}
for layer, layer_values in m["LayerOperations"].items():
    integrate = 0.0
    sync = 0.0
    for unit, amount in layer_values["Integrate"].items():
        integrate += amount * alu_ops[unit]

    for unit, amount in layer_values["Sync"].items():
        sync += amount * alu_ops[unit]
    
    layer_energies[layer] = {
        "Integrate": integrate,
        "Sync": sync
    }

router_transfer_delay = packet_size / m["NoC"]["DataWires"]

print(f"Core memory:")
print(f"  Neuron: {neuron_mem:,} bits")
print(f"  Syn: {syn_mem:,} bits")
print(f"  Layer: {layer_mem:,} bits")
print(f"  Output: {output_mem:,} bits")
print(f"  Compute: {compute_mem:,} bits")
print(f"  Total: {core_mem:,} bits")

print(f"Router memory:")
print(f"  Input: {router_input_mem:,} bits")
print(f"  Output: {router_output_mem:,} bits")

print(f"Packet memory:")
print(f"  Spike: {spike_packet:,} bits")
print(f"  Sync: {sync_packet:,} bits")
print(f"  SyncDone: {sync_done_packet:,} bits")
print(f"  Total: {packet_size:,} bits")

print(f"Area:")
print(f"  Core: {core_area:,} um^2 ({core_area / 1000000:,} mm^2)")
print(f"    Neuron mem: {neuron_area:,} um^2 ({neuron_area / 1000000:,} mm^2)")
print(f"    Synapse mem: {syn_area:,} um^2 ({syn_area / 1000000:,} mm^2)")
print(f"    Layer mem: {layer_area:,} um^2 ({layer_area / 1000000:,} mm^2)")
print(
    f"    Compute buffer: {compute_area:,} um^2 ({compute_area / 1000000:,} mm^2)")
print(
    f"    Output buffer: {output_area:,} um^2 ({output_area / 1000000:,} mm^2)")
print(f"    ALU: {alu_area_total:,} um^2 ({alu_area_total / 1000000:,} mm^2)")
for name, power in alu_area.items():
    print(f"      {name}: {power} um^2 ({power / 10E6:,} mm^2)")
print(f"  Router: {router_area:,} um^2 ({router_area / 1000000:,} mm^2)")
print(
    f"    Input: {router_input_area:,} um^2 ({router_input_area / 1000000:,} mm^2)")
print(
    f"    Output: {router_output_area:,} um^2 ({router_output_area / 1000000:,} mm^2)")
print(f"  Chip: {chip_area:,} um^2 ({chip_area / 1000000:,} mm^2)")

print(f"Static power:")
print(f"  Chip: {chip_static*10E6:,} uW")
print(f"    Core: {core_static*10E6:,} uW")
print(f"      Mem: {core_mem_static*10E6:,} uW")
print(f"      ALU: {alu_static_total*10E6:,} uW")
for name, power in alu_static.items():
    print(f"        {name}: {power*10E6} uW")
print(f"    Router mem: {router_mem_static*10E6:,} uW")

print(f"Dynamic energy:")
print(f"  Router:")
print(
    f"    Hop: {wolkotte_mesh} pJ/b ({wolkotte_mesh * packet_size} pJ/packet)")
print(f"  Core:")
print(f"    Memories:")
print(f"      Layer:")
print(f"        Read: {layer_mem_read} pJ / read")
print(f"        Write: {layer_mem_write} pJ / write")
print(f"      Neuron:")
print(f"        Read: {neuron_mem_read} pJ / read")
print(f"        Write: {neuron_mem_write} pj / write")
print(f"      Synapse:")
print(f"        Read: {syn_mem_read} pJ / read")
print(f"        Write: {syn_mem_write} pJ / write")
print(f"    Operations:")
for layer, values in layer_energies.items():
    print(f"      {layer}:")
    print(f"        Integrate: {values['Integrate']} pJ / Axon")
    print(f"        Sync: {values['Sync']} pJ / neuron")
print(f"Delays:")
print(f"  Packet transfer: {router_transfer_delay} cycles ({router_transfer_delay * period} ps)")
for layer, values in m["LayerDelays"].items():
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