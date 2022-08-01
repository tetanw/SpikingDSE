from operator import mod
import subprocess
import sys

def run_experiments(runs, models, max_samples=2147483647):
    subprocess.run(['dotnet', 'build', '--configuration', 'Release'])

    for expName in runs:
        for dsName, dsFile in models:
            command = f"bin\Release\\net6.0\SpikingDSE.exe dataset-sim"\
                f" -s \"res/snn/snn-{dsName}.json\""\
                f" -h \"res/exp/{expName}/hw.json\""\
                f" -m \"res/exp/{expName}/mappings/{dsName}.json\""\
                f" -d \"res/dataset/{dsFile}.zip\""\
                f" --max-samples={max_samples}"\
                f" -o \"res/exp/{expName}/results/{dsName}\""
            print(f">> {command}")
            process = subprocess.run(command)



if __name__ == "__main__":
    runs = sys.argv[1].split(",")
    models = [
        ("best", "shd-10"),
        ("shd1", "shd-10"),
        ("shd4", "shd-10"),
        ("smnist3", "smnist-3"),
        ("smnist4", "smnist-4"),
        ("psmnist1", "psmnist-1"),
        ("psmnist2", "psmnist-2"),
        ("ssc2", "ssc-4"),
        ("ssc3", "ssc-4")
    ]
    run_experiments(runs, models)
