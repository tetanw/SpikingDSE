import torch
from torch.utils import data
import keras
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


_, (test_X, test_Y) = keras.datasets.mnist.load_data()
test_X, test_Y = transform(test_X, test_Y, 784, 8, 4)
test_dataset = data.TensorDataset(test_X, test_Y)
test_loader = data.DataLoader(test_dataset, shuffle=False)
size = test_X.shape[0]

device = torch.device("cuda:0" if torch.cuda.is_available() else "cpu")
print("device:", device)

model = torch.load("model\smnist1\model_smnist1_9_73.44.pth").to("cpu")
seq_dim = 28 * 28 // 4

extract_inputs_2(test_loader, model, seq_dim, "inputs\smnist")