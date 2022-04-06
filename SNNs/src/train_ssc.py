import numpy as np
import torch
import torch.nn as nn
from torch.optim.lr_scheduler import StepLR
from torch.utils import data
import os
from models import *
from training import *
from ssc_dataset import SSCZipDataset

torch.manual_seed(0)

device = torch.device("cuda:0" if torch.cuda.is_available() else "cpu")
print("device:", device)

batch_size = 128

train_dataset = SSCZipDataset("data/ssc-train.zip")
train_loader = data.DataLoader(
    train_dataset, batch_size=batch_size, shuffle=True)

test_dataset = SSCZipDataset("data/ssc-valid.zip")
test_loader = data.DataLoader(
    test_dataset, batch_size=batch_size, shuffle=False)

input_dim = 700
hidden_dim = [400, 400] 
output_dim = 35
seq_dim = 250

# train
model = SRNN(input_dim, hidden_dim, output_dim).to(device)
criterion = nn.CrossEntropyLoss()
learning_rate = 1e-2  # 1e-2
optimizer = torch.optim.Adam(model.parameters(), lr=learning_rate)
scheduler = StepLR(optimizer, step_size=10, gamma=.5)
train(model, "debug-1", 30, input_dim, seq_dim, train_loader, test_loader, device, criterion, scheduler, optimizer)

# test
# accuracy = test(model)
# print('Final accuracy: ', accuracy)
