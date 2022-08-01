import subprocess
import sys

def run_mappings(runs, models, mapper):
    subprocess.run(['dotnet', 'build', '--configuration', 'Release'])

    for expName in runs:
        for dsName in models:
            command = f"bin\Release\\net6.0\SpikingDSE.exe mapping"\
                f" -s \"res/snn/snn-{dsName}.json\""\
                f" -h \"res/exp/{expName}/hw.json\""\
                f" -m {mapper}"\
                f" -o \"res/exp/{expName}/mappings/{dsName}.json"
            print(f">> {command}")
            process = subprocess.run(command)

if __name__ == "__main__":
    runs = sys.argv[1].split(",")
    models = [
        "best",
        "shd1",
        "shd4",
        "smnist3",
        "smnist4",
        "psmnist1",
        "psmnist2",
        "ssc2",
        "ssc3"
    ]

    run_mappings(runs, models, "FirstFit1")
