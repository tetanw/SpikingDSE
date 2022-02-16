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

# count more precisely
delay_counts = spike_delays.groupby("delay").count()["start"]
plt.plot(delay_counts)
plt.show()

# analyze the amount of time that a router blocks
blockings = pd.read_csv(
    "C:/Users/stefan/Documents/Projects/Master thesis/SpikingDSE/Processor/res/multi-core/v1/blockings.csv")
b = blockings.where(blockings["router-x"] == 0).where(blockings["router-y"] == 0).groupby("snn-time").count()["hw-time"]