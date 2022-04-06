import numpy as np
import torch
import torch.nn as nn
from torch.optim.lr_scheduler import StepLR
from torch.utils import data
import os
from models import *
import sys


def train(model, model_name, num_epochs, input_dim, seq_dim, train_loader, test_loader, device, criterion, scheduler, optimizer):
    dir_path = f"./model/{model_name}"
    if not os.path.isdir(dir_path):
        os.mkdir(dir_path)
    for epoch in range(num_epochs):
        for i, (images, labels) in enumerate(train_loader):
            images = images.view(-1, seq_dim,
                                 input_dim).requires_grad_().to(device)
            batch_size, _, _ = images.shape
            labels = labels.view(batch_size).long().to(device)
            # Clear gradients w.r.t. parameters
            optimizer.zero_grad()
            # Forward pass to get output/logits
            outputs, _, _ = model(images)
            # Calculate Loss: softmax --> cross entropy loss
            loss = criterion(outputs, labels)
            # Getting gradients w.r.t. parameters
            loss.backward()
            # Updating parameters
            optimizer.step()
        scheduler.step()
        accuracy = test(model, train_loader, device, input_dim, seq_dim)
        ts_acc = test(model, test_loader, device, input_dim, seq_dim)
        torch.save(
            model, f'{dir_path}/model_{model_name}_{epoch}_{str(ts_acc)}.pth')
        print('epoch: ', epoch, '. Loss: ', loss.item(),
              '. Tr Accuracy: ', accuracy, '. Ts Accuracy: ', ts_acc)


def test(model, dataloader, device, input_dim, seq_dim):
    correct = total = 0

    # Iterate through test dataset
    for images, labels in dataloader:
        images = images.view(-1, seq_dim, input_dim).to(device)
        batch_size, _, _ = images.shape
        labels = labels.view(batch_size).long().to(device)

        outputs, _, _ = model(images)
        _, predicted = torch.max(outputs.data, 1)
        total += labels.size(0)
        if torch.cuda.is_available():
            correct += (predicted.cpu() == labels.long().cpu()).sum()
        else:
            correct += (predicted == labels).sum()

    accuracy = 100. * correct.numpy() / total
    return accuracy
