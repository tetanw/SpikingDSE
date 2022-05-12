import json
import math
import pandas

# parse x:
f = open("res/exp/exp1/model.json")
m = json.load(f)


def addr(x):
    return math.ceil(math.log2(x))


# address size calculations
dx = addr(m["NoC"]["Width"])
dy = addr(m["NoC"]["Height"])
neuron_bits = addr(m["MaxNeurons"])
syn_bits = addr(m["MaxSynapses"])
layer_bits = addr(m["MaxLayers"])
packet_disc = addr(m["NrPacketTypes"])
feedback = 1

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
input_mem = m["NoC"]["InputSize"] * packet_size
output_mem = m["NoC"]["OutputSize"] * packet_size

# area
bits_per_um2 = 0.46
core_area = bits_per_um2 * core_mem
chip_area = core_area * (m["NoC"]["Width"] * m["NoC"]["Height"])

exp = pandas.read_csv("res/exp/exp1/results/shd1/experiments.csv")
accuracy = (exp["correct"] == exp["predicted"]).sum() / exp.shape[0]

# router energy
l = (core_area/1000000)**(0.5)  # calculate dimensions of meory
technology = 1.1**2 / 1.2**2
wolkotte_mesh = (1.37 + 0.12 * l) / technology
router_dyn = wolkotte_mesh  # Depends on formula from Wolkotte
router_static = 0  # Depends on memory

# core energy
core_dynamic = 0  # Depends on memory + layer operations using Aladdin
neuron_static = 0
layer_static = 0
syn_static = 0
compute_static = 0
output_static = 0
core_static =  neuron_static + layer_static + syn_static + compute_static + output_static # Depends on memory + layer units

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
print(f"  Input: {input_mem:,} bits")
print(f"  Output: {output_mem:,} bits")

print(f"Packet memory:")
print(f"  Spike: {spike_packet:,} bits")
print(f"  Sync: {sync_packet:,} bits")
print(f"  SyncDone: {sync_done_packet:,} bits")
print(f"  Total: {packet_size:,} bits")

print(f"Area")
print(f"  Core: {core_area:,} um^2 ({core_area / 1000000:,} mm^2)")
print(f"  Chip: {chip_area:,} um^2 ({chip_area / 1000000:,} mm^2)")

print(f"Router energy:")
print(f"  Hop per bit: {wolkotte_mesh} pJ")
print(f"  Hop per packet: {wolkotte_mesh * packet_size} pJ")

print(f"Core energy:")
print(f"  ")

print(f"Accuracy: {accuracy}")
