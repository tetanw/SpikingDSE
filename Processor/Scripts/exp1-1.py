from operator import mod
from exp import run_experiments
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
    # ("smnist4", "smnist-4"),
    # ("psmnist1", "psmnist-1"),
    # ("psmnist2", "psmnist-2"),
    # ("ssc2", "ssc-4"),
    # ("ssc3", "ssc-4")
]


run_experiments(runs, models)