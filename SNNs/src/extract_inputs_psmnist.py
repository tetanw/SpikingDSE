import torch
from torch.utils import data
import numpy as np
from extract_inputs import *


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

input_dim = 8
output_dim = 10
size = 28 * 28
stride = 4
seq_dim = size // stride

psmnist = np.load("data/psmnist.npz")
test_X = psmnist["test_x"]
test_Y = psmnist["test_y"]
test_X, test_Y = transform(test_X, test_Y, size, input_dim, stride)
test_dataset = data.TensorDataset(test_X, test_Y)
test_loader = data.DataLoader(test_dataset, shuffle=False)
print('dataset shape: ', test_X.shape)

device = torch.device("cuda:0" if torch.cuda.is_available() else "cpu")
print("device:", device)

model = torch.load("model\psmnist-1\model_psmnist-1_9_69.47.pth").to("cpu")
extract_inputs_2(test_loader, model, seq_dim, "inputs\psmnist-1")