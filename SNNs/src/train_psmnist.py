import keras
import torch
import torch.nn as nn
from torch.optim.lr_scheduler import StepLR
from torch.utils import data
import os
from models import *
from training import *
import sys

torch.manual_seed(0)

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
hidden_dim = [40, 256, 128]
output_dim = 10
size = 28 * 28
stride = 2
seq_dim = size // stride

(train_X, train_Y), (test_X, test_Y) = keras.datasets.mnist.load_data()
psmnist = np.load("data/psmnist.npz")
train_X = psmnist["train_x"]
train_Y = psmnist["train_y"]
test_X = psmnist["test_x"]
test_Y = psmnist["test_y"]
train_X = train_X[:10000]
train_Y = train_Y[:10000]
train_X, train_Y = transform(train_X, train_Y, size, input_dim, stride)
test_X, test_Y = transform(test_X, test_Y, size, input_dim, stride)
print('dataset shape: ', train_X.shape)
print('dataset shape: ', test_X.shape)

device = torch.device("cuda:0" if torch.cuda.is_available() else "cpu")
print("device:", device)

batch_size = 128

train_dataset = data.TensorDataset(train_X, train_Y)
train_loader = data.DataLoader(
    train_dataset, batch_size=batch_size, shuffle=True)

test_dataset = data.TensorDataset(test_X, test_Y)
test_loader = data.DataLoader(
    test_dataset, batch_size=batch_size, shuffle=False)

model = SRNN(input_dim, hidden_dim, output_dim, tau_m=4.0, tau_adp=25.0).to(device)
criterion = nn.CrossEntropyLoss()
learning_rate = 1e-2  # 1e-2
optimizer = torch.optim.Adam(model.parameters(), lr=learning_rate)
scheduler = StepLR(optimizer, step_size=10, gamma=.5)
train(model, "psmnist-1", 10, input_dim, seq_dim, train_loader, test_loader, device, criterion, scheduler, optimizer)

# test
# model = torch.load("model\smnist1\model_smnist1_9_73.44.pth")
# accuracy = test(model)
# print('Final accuracy: ', accuracy)