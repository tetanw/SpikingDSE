from statistics import mode
import numpy as np
from torch.utils import data
from ssc_dataset import SSCZipDataset
import zipfile
import sys

seq_dim = 250
nr_items = 9981

test_dataset = SSCZipDataset("data/ssc-valid.zip")
test_loader = data.DataLoader(test_dataset, shuffle=False)
output = zipfile.ZipFile(
    "data/output.zip", "w", zipfile.ZIP_DEFLATED, compresslevel=9)

for step, (x, y) in enumerate(test_loader):
    if step > nr_items:
        break

    if step % 50 == 0:
        print(f"Sample {step}")

    with output.open("inputs/shd/input_" + str(step) + ".trace", mode="w") as trace_file:
        out = int(y.numpy()[0])
        trace_file.write(f"{out}\n".encode())
        for ts in range(0, seq_dim):
            spikes = x[0, ts, :].nonzero(as_tuple=True)[0].numpy()
            spike_str = [str(spike) for spike in spikes]
            trace_file.write(f"{ts},{','.join(spike_str)}\n".encode())
