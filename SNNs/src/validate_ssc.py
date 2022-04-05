import numpy as np
import torch
import torch.nn as nn
from torch.optim.lr_scheduler import StepLR
from torch.utils import data
import os
from models import *
from ssc_dataset import SSCZipDataset

torch.manual_seed(0)

device = torch.device("cuda:0" if torch.cuda.is_available() else "cpu")
print("device:", device)

test_dataset = SSCZipDataset("data/ssc-valid.zip")
test_loader = data.DataLoader(
    test_dataset, shuffle=False)

model = torch.load("model\ssc-test\model_ssc-test_0_57.17863941488829.pth")
model.to(device)

input_dim = 700
hidden_dim = [400, 400] 
output_dim = 35
seq_dim = 250

for images, labels in test_loader:
    images = images.view(-1, seq_dim, input_dim).to(device)
    output, _ = model(images)
    break