from operator import mod
from base import run_experiments

runs = [
    "exp1"
]
models = [
    # ("best", "shd-10"),
    ("shd1", "shd-10"),
    # ("shd4", "shd-10"),
    # ("smnist3", "smnist-3"),
    # ("smnist4", "smnist-4"),
    # ("psmnist1", "psmnist-1"),
    # ("psmnist2", "psmnist-2"),
    # ("ssc2", "ssc-4"),
    # ("ssc3", "ssc-4")
]

run_experiments(runs, models, max_samples=100)