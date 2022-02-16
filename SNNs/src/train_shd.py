import numpy as np
import torch
import torch.nn as nn
from torch.optim.lr_scheduler import StepLR
from torch.utils import data
import os
from models import *

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

'''
STEP 4: INSTANTIATE MODEL CLASS
'''
input_dim = 700
hidden_dim = [128, 128, 128, 128]  # 128
output_dim = 20
seq_dim = 100  # Number of steps to unroll

model = SRNN(input_dim, hidden_dim, output_dim)

model.to(device)
criterion = nn.CrossEntropyLoss()
learning_rate = 1e-2  # 1e-2

optimizer = torch.optim.Adam(model.parameters(), lr=learning_rate)
scheduler = StepLR(optimizer, step_size=10, gamma=.5)

def train(model, model_name, num_epochs=150):
    dir_path = f"./model/{model_name}"
    if not os.path.isdir(dir_path):
        os.mkdir(dir_path)
    for epoch in range(num_epochs):
        for i, (images, labels) in enumerate(train_loader):
            images = images.view(-1, seq_dim,
                                 input_dim).requires_grad_().to(device)
            labels = labels.long().to(device)
            # Clear gradients w.r.t. parameters
            optimizer.zero_grad()
            # Forward pass to get output/logits
            outputs = model(images)
            # Calculate Loss: softmax --> cross entropy loss
            loss = criterion(outputs, labels)
            # Getting gradients w.r.t. parameters
            loss.backward()
            # Updating parameters
            optimizer.step()
        scheduler.step()
        accuracy = test(model, train_loader)
        ts_acc = test(model)
        torch.save(model, f'{dir_path}/model_{model_name}_{epoch}_{str(ts_acc)}.pth')
        print('epoch: ', epoch, '. Loss: ', loss.item(),
              '. Tr Accuracy: ', accuracy, '. Ts Accuracy: ', ts_acc)


def test(model, dataloader=test_loader):
    correct = total = 0

    # Iterate through test dataset
    for images, labels in dataloader:
        images = images.view(-1, seq_dim, input_dim).to(device)

        outputs = model(images)
        _, predicted = torch.max(outputs.data, 1)
        total += labels.size(0)
        if torch.cuda.is_available():
            correct += (predicted.cpu() == labels.long().cpu()).sum()
        else:
            correct += (predicted == labels).sum()

    accuracy = 100. * correct.numpy() / total
    return accuracy

acc = train(model, "super-big", 50)
accuracy = test(model)
print('Final accuracy: ', accuracy)
