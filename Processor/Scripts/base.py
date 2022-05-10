import subprocess

def run_experiments(runs, models, max_samples=2147483647):
    subprocess.run(['dotnet', 'build', '--configuration', 'Release'])

    for expName in runs:
        for dsName, dsFile in models:
            command = f"bin\Release\\net6.0\SpikingDSE.exe dataset-sim"\
                f" -s \"res/snn/snn-{dsName}.json\""\
                f" -h \"res/exp/{expName}/hw.json\""\
                f" -m \"res/exp/{expName}/mappings/{dsName}.json\""\
                f" -d \"res/dataset/{dsFile}.zip\""\
                f" --max-samples {max_samples}\""\
                f" -o \"res/exp/{expName}/results/{dsName}"
            print(f">> {command}")
            process = subprocess.run(command)

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