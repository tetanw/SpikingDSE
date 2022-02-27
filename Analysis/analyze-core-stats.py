import pandas as pd
import matplotlib.pyplot as plt

stats = pd.read_csv(
    "C:/Users/stefan/Documents/Projects/Master thesis/SpikingDSE/Processor/res/multi-core/v1/core-stats.csv")

def plot_core_stats(x, y, columns, name, ax):
    ax.set_title(name)
    u = stats.query(f"core_x == {x} and core_y == {y}")
    for column in columns:
        ax.plot(u["ts"], u[column], label=column)

# utils
fig, ax = plt.subplots(5, 2)
plot_core_stats(0, 0, "util", "core1", ax[0][0])
plot_core_stats(0, 1, "util", "core2", ax[0][1])
plot_core_stats(1, 0, "util", "core3", ax[1][0])
plot_core_stats(1, 1, "util", "core4", ax[1][1])
plot_core_stats(2, 0, "util", "core5", ax[2][0])
plot_core_stats(2, 1, "util", "core6", ax[2][1])
plot_core_stats(3, 0, "util", "core7", ax[3][0])
plot_core_stats(3, 1, "util", "core8", ax[3][1])
plot_core_stats(4, 0, "util", "core9", ax[4][0])
plot_core_stats(4, 1, "util", "core10", ax[4][1])
plt.show()

# SOPs
fig, ax = plt.subplots(5, 2)
plot_core_stats(0, 1, "sops", "core2", ax[0][1])
plot_core_stats(1, 0, "sops", "core3", ax[1][0])
plot_core_stats(0, 0, "sops", "core1", ax[0][0])
plot_core_stats(1, 1, "sops", "core4", ax[1][1])
plot_core_stats(2, 0, "sops", "core5", ax[2][0])
plot_core_stats(2, 1, "sops", "core6", ax[2][1])
plot_core_stats(3, 0, "sops", "core7", ax[3][0])
plot_core_stats(3, 1, "sops", "core8", ax[3][1])
plot_core_stats(4, 0, "sops", "core9", ax[4][0])
plot_core_stats(4, 1, "sops", "core10", ax[4][1])
plt.show()

# Spikes consumed
fig, ax = plt.subplots(5, 2)
plot_core_stats(0, 1, "spikes_cons", "core2", ax[0][1])
plot_core_stats(1, 0, "spikes_cons", "core3", ax[1][0])
plot_core_stats(0, 0, "spikes_cons", "core1", ax[0][0])
plot_core_stats(1, 1, "spikes_cons", "core4", ax[1][1])
plot_core_stats(2, 0, "spikes_cons", "core5", ax[2][0])
plot_core_stats(2, 1, "spikes_cons", "core6", ax[2][1])
plot_core_stats(3, 0, "spikes_cons", "core7", ax[3][0])
plot_core_stats(3, 1, "spikes_cons", "core8", ax[3][1])
plot_core_stats(4, 0, "spikes_cons", "core9", ax[4][0])
plot_core_stats(4, 1, "spikes_cons", "core10", ax[4][1])
plt.show()

# Spikes produced
fig, ax = plt.subplots(5, 2)
plot_core_stats(0, 1, "spikes_prod", "core2", ax[0][1])
plot_core_stats(1, 0, "spikes_prod", "core3", ax[1][0])
plot_core_stats(0, 0, "spikes_prod", "core1", ax[0][0])
plot_core_stats(1, 1, "spikes_prod", "core4", ax[1][1])
plot_core_stats(2, 0, "spikes_prod", "core5", ax[2][0])
plot_core_stats(2, 1, "spikes_prod", "core6", ax[2][1])
plot_core_stats(3, 0, "spikes_prod", "core7", ax[3][0])
plot_core_stats(3, 1, "spikes_prod", "core8", ax[3][1])
plot_core_stats(4, 0, "spikes_prod", "core9", ax[4][0])
plot_core_stats(4, 1, "spikes_prod", "core10", ax[4][1])
plt.show()

# Spikes produced
fig, ax = plt.subplots(3, 2)
columns = ["late_spikes", "input_spikes_dropped", "core_spikes_dropped"]
plot_core_stats(0, 0, columns, "core1", ax[0][0])
plot_core_stats(0, 1, columns, "core2", ax[0][1])
plot_core_stats(1, 0, columns, "core3", ax[1][0])
plot_core_stats(1, 1, columns, "core4", ax[1][1])
plot_core_stats(2, 0, columns, "core5", ax[2][0])
plot_core_stats(2, 1, columns, "core6", ax[2][1])
# plot_core_stats(3, 0, stats, "core7", ax[3][0])
# plot_core_stats(3, 1, stats, "core8", ax[3][1])
# plot_core_stats(4, 0, stats, "core9", ax[4][0])
# plot_core_stats(4, 1, stats, "core10", ax[4][1])
fig.legend()
plt.show()