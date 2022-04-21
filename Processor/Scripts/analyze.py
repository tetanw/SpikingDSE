import pandas as pd
import matplotlib.pyplot as plt

runs = [
    ("hw-mesh1", "mapping1", 1),
    ("hw-mesh1", "mapping1", 2),
    ("hw-mesh1", "mapping1", 3),
]
models = [
    ("shd1", "shd-10"),
    ("shd4", "shd-10"),
    ("smnist3", "smnist-3"),
    ("smnist4", "smnist-4"),
    ("psmnist1", "psmnist-1"),
    ("psmnist2", "psmnist-2"),
    ("ssc2", "ssc-4"),
    ("ssc3", "ssc-4")
]

print("," + ",".join([modelName for modelName, _ in models]))
for hwName, mappingName, expNr in runs:
    means = []
    for dsName, dsFile in models:
        data = pd.read_csv(f"res/results/{dsName}-{expNr}/experiments.csv")
        # print(f"{dsName}-{expNr}:")
        # print(f"  Latency: Mean {data['latency'].mean()}, Min {data['latency'].min()}, Max {data['latency'].max()}")
        # print(f"  Hops: Mean {data['nrHops'].mean()}, Min {data['nrHops'].min()}, Max {data['nrHops'].max()}")
        # print(f"  SOPs/cycle: Mean {synIntensity.mean()}")
        # print(f"{dsName}-{expNr}:")
        synIntensity = data['nrSOPs'] / data['latency']
        means.append(f"{synIntensity.mean()}")
    print(f"{expNr}," + ",".join(means))
