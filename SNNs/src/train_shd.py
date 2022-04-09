import numpy as np
import torch
import torch.nn as nn
from torch.optim.lr_scheduler import StepLR
from torch.utils import data
import os
from models import *
from training import *
import sys

torch.manual_seed(0)

SHD = np.load("data/SHD_10ms.npz")

train_X = SHD["train_x"]
train_y = SHD["train_y"]

test_X = SHD["test_x"]
test_y = SHD["test_y"]

print('dataset shape: ', train_X.shape)
print('dataset shape: ', test_X.shape)

device = torch.device("cuda:0" if torch.cuda.is_available() else "cpu")
print("device:", device)

batch_size = 128

tensor_trainX = torch.Tensor(train_X)  # transform to torch tensor
tensor_trainY = torch.Tensor(train_y)
train_dataset = data.TensorDataset(tensor_trainX, tensor_trainY)
train_loader = data.DataLoader(
    train_dataset, batch_size=batch_size, shuffle=True)

tensor_testX = torch.Tensor(test_X)  # transform to torch tensor
tensor_testY = torch.Tensor(test_y)
test_dataset = data.TensorDataset(tensor_testX, tensor_testY)
test_loader = data.DataLoader(
    test_dataset, batch_size=batch_size, shuffle=False)

input_dim = 700
output_dim = 20
seq_dim = 100  # Number of steps to unroll

# training
model = SRNN2([
    ALIFLayer(input_dim, 512, tau_m=10.0, tau_adp=10.0),
    ALIFLayer(512, 256, tau_m=10.0, tau_adp=10.0),
    OutputLayer(256, output_dim, tau_m=10.0)
]).to(device)
criterion = nn.CrossEntropyLoss()
learning_rate = 1e-2  # 1e-2
optimizer = torch.optim.Adam(model.parameters(), lr=learning_rate)
scheduler = StepLR(optimizer, step_size=20, gamma=.5)
train(model, "shd-4", 30, input_dim, seq_dim, train_loader, test_loader, device, criterion, scheduler, optimizer)

# test
# model = torch.load("")
# accuracy = test(model)
# print('Final accuracy: ', accuracy)
