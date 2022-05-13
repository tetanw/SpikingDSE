import json

f = open("res/exp/exp1/model.json")
m = json.load(f)

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