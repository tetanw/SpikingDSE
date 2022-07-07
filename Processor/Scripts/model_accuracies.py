import sys
import pandas
from model_costs import *
from model_metrics import *


if __name__ == "__main__":
    expNames = sys.argv[1].split(",")

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

    # ideal_accuracies = {
    #     "shd1": 0.8441,
    #     "shd4": 0.8542,
    #     "smnist3": 0.9619,
    #     "smnist4": 0.9695,
    #     "psmnist1": 0.9117,
    #     "psmnist2":0.9305,
    #     "ssc2": 0.7313,
    #     "ssc3": 0.664,
    # }

    ideal_accuracies = {
        "shd1": 0.844522968,
        "shd4": 0.854240283,
        "smnist3": 0.9617,
        "smnist4": 0.9699,
        "psmnist1": 0.9205,
        "psmnist2": 0.9315,
        "ssc2": 0.73128945,
        "ssc3": 0.663560765
    }

    accuracies = {
        "shd1": [],
        "shd4": [],
        "smnist3": [],
        "smnist4": [],
        "psmnist1": [],
        "psmnist2": [],
        "ssc2": [],
        "ssc3": []
    }

    faulties = {
        "shd1": [],
        "shd4": [],
        "smnist3": [],
        "smnist4": [],
        "psmnist1": [],
        "psmnist2": [],
        "ssc2": [],
        "ssc3": []
    }

    for expName in expNames:
        for modelName in models:
            exp = pandas.read_csv(
                f"res/exp/{expName}/results/{modelName}/experiments.csv")
            c = Costs(f"res/exp/{expName}/model.json")
            m = Metrics(c, exp)

            accuracies[modelName].append(m.accuracy)
            faulties[modelName].append(m.nr_faults)

    print(",".join(expNames))
    for modelName in models:
        accs = [f"{value}" for value in accuracies[modelName]]
        print(",".join(accs))

    print()
    print(",".join(expNames))
    for modelName in models:
        ideal = ideal_accuracies[modelName]
        diffs = [f"{value-ideal}" for value in accuracies[modelName]]
        print(",".join(diffs))

    print()
    print(",".join(expNames))
    for modelName in models:
        fs = [f"{value}" for value in faulties[modelName]]
        print(",".join(fs))