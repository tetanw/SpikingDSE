import sys
import pandas
from model_costs import *
from model_metrics import *

if __name__ == "__main__":
    metric = sys.argv[1]
    scale = float(sys.argv[2])
    expNames = sys.argv[3].split(",")

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

    print("," + ",".join(models))
    for expName in expNames:
        results = [expName]
        for modelName in models:
            exp = pandas.read_csv(
                f"res/exp/{expName}/results/{modelName}/experiments.csv")
            c = Costs(f"res/exp/{expName}/model.json")
            m = Metrics(c, exp)
            results.append(f"{getattr(m, metric)*scale:.2f}")
        print(",".join(results))