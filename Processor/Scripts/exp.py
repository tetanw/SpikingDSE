import subprocess

def run_experiments(runs, models):
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

def run_mappings(runs, models, mapper):
    subprocess.run(['dotnet', 'build', '--configuration', 'Release'])

    for hwName, mappingName in runs:
        for dsName in models:
            command = f"bin\Release\\net6.0\SpikingDSE.exe mapping"\
                f" -s \"res/exp/snn-{dsName}.json\""\
                f" -h \"res/exp/{hwName}.json\""\
                f" -m {mapper}"\
                f" -o \"res/exp/{mappingName}-{dsName}.json"
            print(f">> {command}")
            process = subprocess.run(command)