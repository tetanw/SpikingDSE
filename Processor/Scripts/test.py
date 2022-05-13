import json
import math
import pandas

# parse x:
f = open("res/exp/exp1/model.json")
m = json.load(f)


def addr(x):
    return math.ceil(math.log2(x))

alu_costs = {
    "Addf32": {
        "Static": 15,
        "Area": 2060,
        "Dynamic": 7.701
    },
    "Multf32": {
        "Static": 44.8,
        "Area": 2060,
        "Dynamic": 26.6
    },
    "Counter": {
        "Static": 0,
        "Area": 214.5,
        "Dynamic": 0.20678
    },
    "Comparator": {
        "Static": 0, # uW
        "Area": 70, #um2
        "Dynamic": 0.02898, #pJ
    }
}

width = m["NoC"]["Width"]
height = m["NoC"]["Height"]
size = width * height
feedback = 1
voltage = 1.1

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
neuron_mem = m["MaxNeurons"] * m["NeuronSize"]
syn_mem = m["MaxSynapses"] * m["SynapseSize"]
split_mem = dx + dy + layer_bits + feedback
layer_mem = m["MaxLayers"] * \
    (4 * neuron_bits + 2 * layer_bits + m["MaxSplits"] * split_mem)
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
    alu_area[name] = amount * alu_costs[name]["Area"]
alu_area_total = sum(v for _, v in alu_area.items())
core_mem_area = neuron_area + syn_area + layer_area + output_area + compute_area
core_area = core_mem_area + alu_area_total

# router area
router_input_area = calc_area(router_input_mem)
router_output_area = calc_area(output_mem)
router_area = 5 * router_input_area + 5 * router_output_area
chip_area = core_area * size + router_area * size

exp = pandas.read_csv("res/exp/exp1/results/shd1/experiments.csv")
accuracy = (exp["correct"] == exp["predicted"]).sum() / exp.shape[0]

# router energy
l = (core_area/1000000)**(0.5)  # calculate dimensions of meory
technology = voltage**2 / 1.2**2
wolkotte_mesh = (1.37 + 0.12 * l) * technology
router_dyn = wolkotte_mesh  # Depends on formula from Wolkotte
router_static = 0  # Depends on memory

def mem_static(bits):
    return (8E-05 * bits + 1.6029) * 10E-6

# core energy
# Dynamic: Depends on memory + layer operations using Aladdin
# Static: Depends on memory + layer units
core_dynamic = mem_static(core_mem)  
neuron_static = mem_static(neuron_mem)
layer_static = mem_static(layer_mem)
syn_static = mem_static(syn_mem)
compute_static = mem_static(compute_mem)
output_static = mem_static(output_mem)
core_mem_static =  (neuron_static + layer_static + syn_static + compute_static + output_static) * voltage
router_mem_static = 5 * mem_static(router_input_mem) + 5 * mem_static(router_output_mem)
chip_static = size * core_mem_static + size * router_mem_static
alu_static = {}
for name, amount in m["CoreALU"].items():
    alu_static[name] = amount * alu_costs[name]["Static"] / 1E6
alu_static_total =  sum(v for _, v in alu_static.items())
core_static = core_mem_static + alu_static_total

hw = {"Global": {}, "CoreTemplates": {}}
hw["NoC"] = {
    "Type": "Mesh",
    "Width": m["NoC"]["Width"],
    "Height": m["NoC"]["Height"],
    "InputSize": m["NoC"]["InputSize"],
    "OutputSize": m["NoC"]["OutputSize"],
    "SwitchDelay": m["NoC"]["SwitchDelay"],
    "InputDelay": m["NoC"]["InputDelay"],
    "OutputDelay": m["NoC"]["OutputDelay"]
}
hw["CoreTemplates"]["Core"] = {
    "Type": "core-v1",
    "Accepts": [
        "ALIF",
        "output"
    ],
    "MaxNeurons": m["MaxNeurons"],
    "MaxSynapses": m["MaxSynapses"],
    "MaxFanIn": m["MaxFanIn"],
    "MaxLayers": m["MaxLayers"],
    "MaxSplits": m["MaxSplits"],
    "NrParallel": m["NrParallel"],
    "ReportSyncEnd": True,
    "OutputBufferDepth": m["OutputBufferDepth"],
    "DisableIfIdle": True,
    "LayerCosts": {
        "ALIF": {
            "SyncII": 2,
            "SyncLat": 5,
            "IntegrateII": 2,
            "IntegrateLat": 5
        },
        "output": {
            "SyncII": 2,
            "SyncLat": 5,
            "IntegrateII": 2,
            "IntegrateLat": 5
        }
    }
}
hw["Cores"] = []
prio = m["NoC"]["Width"]*m["NoC"]["Height"]
coreNumber = 0
for x in range(m["NoC"]["Width"]):
    for y in range(m["NoC"]["Height"]):
        if x == 0 and y == 0:
            hw["Cores"].append({
                "Name": "controller",
                "Type": "controller-v1",
                "Accepts": [
                    "input"
                ],
                "Priority": prio,
                "GlobalSync": False,
                "ConnectsTo": f"mesh,{x},{y}",
                "IgnoreIdleCores": True
            })
        else:
            hw["Cores"].append({
                "$Template": "Core",
                "Name": f"core{coreNumber}",
                "Priority": prio,
                "ConnectsTo": f"mesh,{x},{y}"
            })
            coreNumber = coreNumber + 1
        prio = prio - 1
json.dump(hw, open("test.json", mode="w"), indent=4, sort_keys=False)

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
print(f"    Compute buffer: {compute_area:,} um^2 ({compute_area / 1000000:,} mm^2)")
print(f"    Output buffer: {output_area:,} um^2 ({output_area / 1000000:,} mm^2)")
print(f"    ALU: {alu_area_total:,} um^2 ({alu_area_total / 1000000:,} mm^2)")
for name, power in alu_area.items():
    print(f"      {name}: {power} um^2 ({power / 10E6:,} mm^2)")
print(f"  Router: {router_area:,} um^2 ({router_area / 1000000:,} mm^2)")
print(f"    Input: {router_input_area:,} um^2 ({router_input_area / 1000000:,} mm^2)")
print(f"    Output: {router_output_area:,} um^2 ({router_output_area / 1000000:,} mm^2)")
print(f"  Chip: {chip_area:,} um^2 ({chip_area / 1000000:,} mm^2)")

print(f"Router energy:")
print(f"  Hop per bit: {wolkotte_mesh} pJ")
print(f"  Hop per packet: {wolkotte_mesh * packet_size} pJ")

print(f"Static:")
print(f"  Chip: {chip_static*10E6:,} uW")
print(f"    Core: {core_static*10E6:,} uW")
print(f"      Mem: {core_mem_static*10E6:,} uW")
print(f"      ALU: {alu_static_total*10E6:,} uW")
for name, power in alu_static.items():
    print(f"        {name}: {power*10E6} uW")
print(f"    Router mem: {router_mem_static*10E6:,} uW")

print(f"Accuracy: {accuracy}")
