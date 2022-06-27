import json
import math
from multiprocessing.sharedctypes import Value
import sys
from orderings import matrix_colMajor, matrix_diagonal

from model_costs import Costs


def save_hw(model_path: str, hw_path: str, costs: Costs):
    f = open(model_path)
    m = json.load(f)

    width = m["NoC"]["Width"]
    height = m["NoC"]["Height"]
    size = width * height

    hw = {"Global": {}, "CoreTemplates": {}}
    hw["NoC"] = {
        "Type": "XYMesh",
        "Width": width,
        "Height": height,
        "InputSize": m["NoC"]["InputSize"],
        "OutputSize": m["NoC"]["OutputSize"],
        "SwitchDelay": m["NoC"]["SwitchDelay"],
        "TransferDelay": int(costs.router_transfer_delay),
        "InputDelay": m["NoC"]["InputDelay"],
        "OutputDelay": m["NoC"]["OutputDelay"]
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
        "ShowALUStats": True
    }
    hw["CoreTemplates"]["Core"]["LayerCosts"] = {}
    for layer, latencies in m["LayerDelays"].items():
        hw["CoreTemplates"]["Core"]["LayerCosts"][layer] = {
            "SyncII": int(latencies["SyncII"]),
            "SyncLat": int(latencies["SyncLat"]),
            "IntegrateII": int(latencies["IntegrateII"]),
            "IntegrateLat": int(latencies["IntegrateLat"])
        }

    hw["Cores"] = []
    coreNumber = 0
    if "CoreOrdering" not in m:
        ordering = matrix_colMajor(width, height)
    elif m["CoreOrdering"] == "ColumnMajor":
        ordering = matrix_colMajor(width, height)
    elif m["CoreOrdering"] == "Diagonal":
        ordering = matrix_diagonal(width, height)
    else:
        raise ValueError(f"Unknown ordering type: {m['CoreOrdering']}")
    for x in range(width):
        for y in range(height):
            prio = size - ordering[y][x]
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
                    "IgnoreIdleCores": True
                })
            else:
                hw["Cores"].append({
                    "$Template": "Core",
                    "Name": f"core{coreNumber}",
                    "Priority": prio,
                    "ConnectsTo": f"{x},{y}"
                })
                coreNumber = coreNumber + 1
    json.dump(hw, open(hw_path, mode="w"), indent=4, sort_keys=False)


if __name__ == "__main__":
    expNames = sys.argv[1].split(",")

    for expName in expNames:
        model_path = f"res/exp/{expName}/model.json"
        cost_path = f"res/exp/{expName}/cost.json"
        hw_path = f"res/exp/{expName}/hw.json"
        costs = Costs(model_path)
        save_hw(model_path, hw_path, costs)
