import pandas as pd
import matplotlib.pyplot as plt

compute_delays = pd.read_csv(
    "C:/Users/stefan/Documents/Projects/Master thesis/SpikingDSE/Processor/res/multi-core/v1/compute-delays.csv")
compute_delays["delay"] = compute_delays["end"] - compute_delays["start"]
spike_delays = pd.read_csv(
    "C:/Users/stefan/Documents/Projects/Master thesis/SpikingDSE/Processor/res/multi-core/v1/spike-delays.csv")
spike_delays["delay"] = spike_delays["end"] - spike_delays["start"]

# Histogram of spike delays
plt.hist(spike_delays["delay"], log=True)       
plt.show()

# count more precisely
delay_counts = spike_delays.groupby("delay").count()["start"]
plt.plot(delay_counts)
plt.show()

# more precisely but per layer
fig, ax = plt.subplots(5, 1)
delay_counts = spike_delays.query("layer == 'h1-1' or layer == 'h1-2'").groupby("delay").count()["start"]
ax[0].set_title("to h1")
ax[0].plot(delay_counts)
delay_counts = spike_delays.query("layer == 'h2-1' or layer == 'h2-2'").groupby("delay").count()["start"]
ax[1].set_title("to h2")
ax[1].plot(delay_counts)
delay_counts = spike_delays.query("layer == 'h3-1' or layer == 'h3-2'").groupby("delay").count()["start"]
ax[2].set_title("to h3")
ax[2].plot(delay_counts)
delay_counts = spike_delays.query("layer == 'h4-1' or layer == 'h4-2'").groupby("delay").count()["start"]
ax[3].set_title("to h4")
ax[3].plot(delay_counts)
delay_counts = spike_delays.query("layer == 'output'").groupby("delay").count()["start"]
ax[4].set_title("to out")
ax[4].plot(delay_counts)
plt.show()

# analyze the amount of time that a router blocks
blockings = pd.read_csv(
    "C:/Users/stefan/Documents/Projects/Master thesis/SpikingDSE/Processor/res/multi-core/v1/blockings.csv")
b = blockings.where(blockings["router-x"] == 0).where(blockings["router-y"] == 0).groupby("snn-time").count()["hw-time"]