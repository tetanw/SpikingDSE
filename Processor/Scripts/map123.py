from exp import run_mappings

runs = [
    ("hw-mesh1", "mapping1"),
    ("hw-mesh2", "mapping2"),
    ("hw-mesh3", "mapping3"),
]
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

run_mappings(runs, models, "FirstFit")