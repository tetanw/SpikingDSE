import json
import math
import sys

from model_stats import Stats


def save_hw(model_path: str, hw_path: str, stats: Stats):
    f = open(model_path)
    m = json.load(f)
    noc = m["NoC"]

    # Assumption: two-phase handshake, changing values on req and ack each take 1 cycle
    # transfer_latency = 1 if noc["NrDataWires"] == -1 else math.ceil(stats.packet_size / noc["NrDataWires"])

    hw = {"Global": {}, "CoreTemplates": {}}
    hw["NoC"] = {
        "Type": "XYMesh",
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
        "ShowMemStats": True,
        "ShowLayerStats": False,
        "ShowALUStats": True,
    }
    hw["CoreTemplates"]["Core"]["LayerCosts"] = {}
    for layer, latencies in m["LayerDelays"].items():
        hw["CoreTemplates"]["Core"]["LayerCosts"][layer] = {
            "SyncII": int(latencies["SyncII"]) ,
            "SyncLat": int(latencies["SyncLat"]) ,
            "IntegrateII": int(latencies["IntegrateII"]) ,
            "IntegrateLat": int(latencies["IntegrateLat"]) 
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
                    "ConnectsTo": f"{x},{y}",
                    "IgnoreIdleCores": True,
                    "SyncDelay": int(m["SyncDelay"])
                })
            else:
                hw["Cores"].append({
                    "$Template": "Core",
                    "Name": f"core{coreNumber}",
                    "Priority": prio,
                    "ConnectsTo": f"{x},{y}"
                })
                coreNumber = coreNumber + 1
            prio = prio - 1
    json.dump(hw, open(hw_path, mode="w"), indent=4, sort_keys=False)


if __name__ == "__main__":
    expName = sys.argv[1]

    model_path = f"res/exp/{expName}/model.json"
    cost_path = f"res/exp/{expName}/cost.json"
    hw_path = f"res/exp/{expName}/hw.json"
    stats = Stats(model_path, cost_path)
    save_hw(model_path, hw_path, stats)
