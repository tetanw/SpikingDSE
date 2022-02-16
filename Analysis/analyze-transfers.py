import pandas as pd
import matplotlib.pyplot as plt

transfers = pd.read_csv(
    "C:/Users/stefan/Documents/Projects/Master thesis/SpikingDSE/Processor/res/multi-core/v1/transfers.csv")


def plot_router_traffic(x, y, name, ax):
    router_traffic = transfers.where(transfers["router-x"] == x).where(
        transfers["router-y"] == y).groupby(["snn-time"])["from"].count()
    ax.plot(router_traffic)
    ax.set_title(name)


fig, ax = plt.subplots(3, 2)
plot_router_traffic(0, 0, "core1", ax[0][0])
plot_router_traffic(0, 1, "core2", ax[0][1])
plot_router_traffic(1, 0, "core3", ax[1][0])
plot_router_traffic(1, 1, "core4", ax[1][1])
plot_router_traffic(2, 0, "core5", ax[2][0])
plt.show()


def count_spikes_from_dir(x, y, dir):
    return transfers \
        .where(transfers["router-x"] == x) \
        .where(transfers["router-y"] == y) \
        .where(transfers["from"] == dir) \
        .groupby(["snn-time"])["from"].count()


def plot_router_dirs(x, y, name, ax):
    spikes_north = count_spikes_from_dir(x, y, 0)
    spikes_east = count_spikes_from_dir(x, y, 1)
    spikes_south = count_spikes_from_dir(x, y, 2)
    spikes_west = count_spikes_from_dir(x, y, 3)
    spikes_local = count_spikes_from_dir(x, y, 4)
    ax.set_title(name)
    ax.plot(spikes_north, label="north")
    ax.plot(spikes_east, label="east")
    ax.plot(spikes_south, label="south")
    ax.plot(spikes_west, label="west")
    ax.plot(spikes_local, label="local")
    # ax.legend()


fig, ax = plt.subplots(3, 2)
plot_router_dirs(0, 0, "core1", ax[0][0])
plot_router_dirs(0, 1, "core2", ax[0][1])
plot_router_dirs(1, 0, "core3", ax[1][0])
plot_router_dirs(1, 1, "core4", ax[1][1])
plot_router_dirs(2, 0, "core5", ax[2][0])
handles, labels = ax[0][0].get_legend_handles_labels()
fig.legend(handles, labels)
plt.show()
