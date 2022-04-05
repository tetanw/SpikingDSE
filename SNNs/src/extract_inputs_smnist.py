import numpy as np
import torch
import torch.nn as nn
from torch.optim.lr_scheduler import StepLR
import math
import sys
import torch.nn.functional as F
from torch.utils import data
import keras


def transform(x, y, size, input_size, stride):
    nr_steps = size // stride
    inputs = []
    x = x.reshape(-1, size)
    for i in range(nr_steps):
        start_idx = i*stride
        if start_idx < (size - input_size):
            input = x[:, start_idx:start_idx +
                      input_size].reshape(-1, 1, input_size)
        else:
            input = x[:, -input_size:].reshape(-1, 1, input_size)
        inputs.append(torch.from_numpy(input))
    return torch.cat(inputs, dim=1).float() / 255.0, torch.tensor(y).long()


_, (test_X, test_Y) = keras.datasets.mnist.load_data()
test_X, test_Y = transform(test_X, test_Y, 784, 8, 4)
test_dataset = data.TensorDataset(test_X, test_Y)
test_loader = data.DataLoader(test_dataset, shuffle=False)
size = test_X.shape[0]

device = torch.device("cuda:0" if torch.cuda.is_available() else "cpu")
print("device:", device)

model = torch.load("model\smnist1\model_smnist1_9_73.44.pth").to("cpu")
nr_timesteps = 28 * 28 // 4

for step, (x, y) in enumerate(test_loader):
    _, spikes, _ = model(x.to("cpu"))

    with open("inputs/smnist/input_" + str(step) + ".trace", "w") as input_file:
        correct = int(y.numpy()[0])
        input_file.write(str(correct) + "\n")
        input_file.write("0,\n")
        for ts in range(1, nr_timesteps):
            spikes_ts = spikes[ts - 1][0].flatten().nonzero().flatten().numpy()
            spike_str = [str(spike) for spike in spikes_ts]
            input_file.write(str(ts) + "," + ",".join(spike_str) + "\n")