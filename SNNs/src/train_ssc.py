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

batch_size = 256

train_dataset = SSCZipDataset("data/ssc-train.zip")
train_loader = data.DataLoader(
    train_dataset, batch_size=batch_size, shuffle=True)

test_dataset = SSCZipDataset("data/ssc-valid.zip")
test_loader = data.DataLoader(
    test_dataset, batch_size=batch_size, shuffle=False)

'''
STEP 4: INSTANTIATE MODEL CLASS
'''
input_dim = 700
hidden_dim = [400, 400] 
output_dim = 35
seq_dim = 250

# model = SRNN(input_dim, hidden_dim, output_dim)
model = torch.load("model\ssc-test\model_ssc-test_0_6747.049393848311.pth")
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
            # print(outputs, labels)
            loss = criterion(outputs, labels.flatten())
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
        labels = labels.flatten()

        outputs = model(images)
        _, predicted = torch.max(outputs.data, 1)
        total += labels.size(0)
        if torch.cuda.is_available():
            correct += (predicted.cpu() == labels.long().cpu()).sum()
        else:
            correct += (predicted == labels).sum()

    accuracy = 100. * correct.numpy() / total
    return accuracy

acc = train(model, "ssc-test", 4)
accuracy = test(model)
print('Final accuracy: ', accuracy)
