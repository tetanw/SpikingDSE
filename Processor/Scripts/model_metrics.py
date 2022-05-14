import pandas
from model_stats import *

exp = pandas.read_csv("res/exp/exp1/results/shd1/experiments.csv")

s = Stats("res/exp/exp1/model.json", "Scripts\cost.json")
print(s.compute_mem)
