from operator import mod
from exp import run_experiments
import subprocess
import os

runs = [
    ("hw-mesh1-1", "mapping1", 4),
    ("hw-mesh1-2", "mapping1", 5),
    ("hw-mesh1-3", "mapping1", 6),
    ("hw-mesh1-4", "mapping1", 7),
]
models = [
    # ("shd1", "shd-10"),
    # ("shd4", "shd-10"),
    # ("smnist3", "smnist-3"),
    ("smnist4", "smnist-4"),
    ("psmnist1", "psmnist-1"),
    # ("psmnist2", "psmnist-2"),
    # ("ssc2", "ssc-4"),
    # ("ssc3", "ssc-4")
]

run_experiments(runs, models)