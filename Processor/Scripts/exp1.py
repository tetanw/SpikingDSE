from re import sub
import subprocess
import os

runs = [
    ("hw-mesh1", "mapping1", 1),
    # ("hw-mesh2", "mapping2", 2),
    # ("hw-mesh3", "mapping3", 3)
]
models = [
    ("shd1", "shd-10"),
    ("shd4", "shd-10"),
    # ("smnist3", "smnist-3"),
    # ("smnist4", "sminst-4"),
    # ("psminst1", "psmnist-1"),
    # ("psmnist2", "psmnist-2"),
    # ("ssc2", "ssc-4"),
    # ("ssc3", "ssc-4")
]

subprocess.run(['dotnet', 'build', '--configuration', 'Release'])
for hwName, mappingName, expNr in runs:
    for dsName, dsFile in models:
        command = f"bin\Release\\net6.0\SpikingDSE.exe dataset-sim"\
            f" -s \"res/exp/snn-{dsName}.json\""\
            f" -h \"res/exp/{hwName}.json\""\
            f" -m \"res/exp/{mappingName}-{dsName}.json\""\
            f" -d \"res/dataset/{dsFile}.zip\""\
            f" -o \"res/results/{dsName}-{expNr}"
        print(f">> {command}")
        process = subprocess.run(command)
