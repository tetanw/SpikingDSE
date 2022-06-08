import sys
import pandas
from model_costs import *
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

    print("model,energy,throughput,accuracy,sparsity,area,faults")
    for modelName in models:
        exp = pandas.read_csv(
            f"res/exp/{expName}/results/{modelName}/experiments.csv")
        c = Costs(f"res/exp/{expName}/model.json")
        m = Metrics(c, exp)
        
        parts = []
        parts.append(modelName)
        parts.append(f"{m.total_energy:.2f}")
        parts.append(f"{m.inferences_per_second:.2f}")
        parts.append(f"{m.accuracy:.3f}")
        parts.append(f"{m.total_sparsity:.2f}")
        parts.append(f"{c.chip_area*1E-6:.2f}")
        parts.append(f"{m.nr_faults}")
        print(",".join(parts))