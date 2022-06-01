import sys
import pandas
from model_stats import *
from model_metrics import *

if __name__ == "__main__":
    expName = sys.argv[1]

    models = [
        "shd1",
        "shd4",
        "smnist3",
        "smnist4",
        "psmnist1",
        "psmnist2",
        "ssc2",
        "ssc3"
    ]

    cost_path = f"res/exp/{expName}/cost.json"

    print("model,energy,duration,area")
    for modelName in models:
        exp = pandas.read_csv(
            f"res/exp/{expName}/results/{modelName}/experiments.csv")
        s = Stats(f"res/exp/{expName}/model.json", cost_path)
        m = Metrics(s, cost_path, exp)
        
        parts = []
        parts.append(modelName)
        parts.append(f"{m.total_energy:2f}")
        parts.append(f"{m.latency.sum():2f}")
        parts.append(f"{s.chip_area*1E-6:2f}")
        print(",".join(parts))