import numpy as np
import torch
from torch.utils import data
from extract_inputs import *

SHD = np.load("data/SHD_10ms.npz")

test_X = SHD["test_x"]
test_Y = SHD["test_y"]

print('dataset shape: ', test_X.shape)

tensor_testX = torch.Tensor(test_X)  # transform to torch tensor
tensor_testY = torch.Tensor(test_Y)
test_dataset = data.TensorDataset(tensor_testX, tensor_testY)
test_loader = data.DataLoader(test_dataset, shuffle=False)

extract_inputs_1(test_loader, 100, "inputs\shd")