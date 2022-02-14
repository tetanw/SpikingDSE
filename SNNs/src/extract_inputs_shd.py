import numpy as np
import torch
import torch.nn as nn
from torch.optim.lr_scheduler import StepLR
import math
import sys
import torch.nn.functional as F
from torch.utils import data
import struct

test_X = np.load('data/testX_10ms.npy')
test_y = np.load('data/testY_10ms.npy').astype(np.float)

print('dataset shape: ', test_X.shape)

tensor_testX = torch.Tensor(test_X)  # transform to torch tensor
tensor_testY = torch.Tensor(test_y)
test_dataset = data.TensorDataset(tensor_testX, tensor_testY)
test_loader = data.DataLoader(test_dataset, shuffle=False)
for step, (x, y) in enumerate(test_loader):
    if step > 2264:
        break

    with open("inputs/shd/input_" + str(step) + ".trace", "w") as input_file:
        out = int(y.numpy()[0])
        input_file.write(str(out) + "\n")
        for ts in range(0, 99):
            spikes = x[0, ts, :].nonzero(as_tuple=True)[0].numpy()
            spike_str = [str(spike) for spike in spikes]
            input_file.write(str(ts) + "," + ",".join(spike_str) + "\n")
