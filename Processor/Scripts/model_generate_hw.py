import json
import math

from model_stats import Stats

def save_hw(model_path: str, hw_path: str, stats: Stats):
    f = open(model_path)
    m = json.load(f)
    noc = m["NoC"]

    # Assumption: two-phase handshake, changing values on req and ack each take 1 cycle
    # transfer_latency = 1 if noc["NrDataWires"] == -1 else math.ceil(stats.packet_size / noc["NrDataWires"])

    hw = {"Global": {}, "CoreTemplates": {}}
    hw["NoC"] = {
        "Type": "Mesh",
        "Width": m["NoC"]["Width"],
        "Height": m["NoC"]["Height"],
        "InputSize": m["NoC"]["InputSize"],
        "OutputSize": m["NoC"]["OutputSize"],
        "SwitchDelay": 0,
        "TransferDelay": int(stats.router_transfer_delay),
        "InputDelay": 1050,
        "OutputDelay": 1050
    }
    hw["CoreTemplates"]["Core"] = {
        "Type": "core-v1",
        "Accepts": [
            "ALIF"
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
                        "input",
                        "output"
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
    json.dump(hw, open(hw_path, mode="w"), indent=4, sort_keys=False)

if __name__  == "__main__":
    model_path = "res/exp/exp1/model.json"
    cost_path = "Scripts/cost.json"
    hw_path = "res/exp/exp1/hw.json"
    stats = Stats(model_path, cost_path)
    save_hw(model_path, hw_path, stats)