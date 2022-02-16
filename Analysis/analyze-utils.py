import pandas as pd
import matplotlib.pyplot as plt

utils = pd.read_csv(
    "C:/Users/stefan/Documents/Projects/Master thesis/SpikingDSE/Processor/res/multi-core/v1/util-core.csv")

def plot_core_util(x, y, name, ax):
    u = utils.query(f"core_x == {x} and core_y == {y}")["util"]
    ax.set_title(name)
    ax.plot(u)

fig, ax = plt.subplots(5, 2)
plot_core_util(0, 0, "core1", ax[0][0])
plot_core_util(0, 1, "core2", ax[0][1])
plot_core_util(1, 0, "core3", ax[1][0])
plot_core_util(1, 1, "core4", ax[1][1])
plot_core_util(2, 0, "core5", ax[2][0])
plot_core_util(2, 1, "core6", ax[2][1])
plot_core_util(3, 0, "core7", ax[3][0])
plot_core_util(3, 1, "core8", ax[3][1])
plot_core_util(4, 0, "core9", ax[4][0])
plot_core_util(4, 1, "core10", ax[4][1])
plt.show()