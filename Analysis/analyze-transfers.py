import pandas as pd
import matplotlib.pyplot as plt

transfers = pd.read_csv("C:/Users/stefan/Documents/Projects/Master thesis/SpikingDSE/Processor/res/multi-core/v1/transfers.csv")

def plot_router_traffic(x, y, ax):
    router_traffic = transfers.where(transfers["router-x"] == x).where(transfers["router-y"] == y).groupby(["snn-time"])["from"].count()
    ax.plot(router_traffic)

fig, ax = plt.subplots(2, 2)
plot_router_traffic(0, 0, ax[0][0])
plot_router_traffic(1, 0, ax[1][0])
plot_router_traffic(1, 1, ax[1][1])
plot_router_traffic(0, 1, ax[0][1])
plt.show()