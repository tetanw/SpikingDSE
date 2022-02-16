import pandas as pd
import matplotlib.pyplot as plt

counts = pd.read_csv(
    "C:/Users/stefan/Documents/Projects/Master thesis/SpikingDSE/Processor/res/multi-core/counts.csv")


def plot_counts(name, ax):
    c = counts.query(f"layer == '{name}'")
    ax.set_title(name)
    ax.plot(c["ts"], c["count"])

fig, ax = plt.subplots(3, 1)
plot_counts("i", ax[0])
plot_counts("h1", ax[1])
plot_counts("h2", ax[2])
# plot_counts("h3", ax[3])
# plot_counts("h4", ax[4])
plt.show()
