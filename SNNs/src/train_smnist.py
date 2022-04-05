import keras
import torch
import torch.nn as nn
from torch.optim.lr_scheduler import StepLR
from torch.utils import data
import os
from models import *
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
stride = 4
seq_dim = size // stride

(train_X, train_Y), (test_X, test_Y) = keras.datasets.mnist.load_data()
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

'''
STEP 4: INSTANTIATE MODEL CLASS
'''


model = SRNN(input_dim, hidden_dim, output_dim, tau_m=4.0, tau_adp=25.0)

model.to(device)
criterion = nn.CrossEntropyLoss()
learning_rate = 1e-2  # 1e-2

optimizer = torch.optim.Adam(model.parameters(), lr=learning_rate)
scheduler = StepLR(optimizer, step_size=10, gamma=.5)


def train(model, model_name, num_epochs):
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
            outputs, _, _ = model(images)
            # Calculate Loss: softmax --> cross entropy loss
            loss = criterion(outputs, labels)
            # Getting gradients w.r.t. parameters
            loss.backward()
            # Updating parameters
            optimizer.step()
        scheduler.step()
        accuracy = test(model, train_loader)
        ts_acc = test(model)
        torch.save(
            model, f'{dir_path}/model_{model_name}_{epoch}_{str(ts_acc)}.pth')
        print('epoch: ', epoch, '. Loss: ', loss.item(),
              '. Tr Accuracy: ', accuracy, '. Ts Accuracy: ', ts_acc)


def test(model, dataloader=test_loader):
    correct = total = 0

    # Iterate through test dataset
    for images, labels in dataloader:
        images = images.view(-1, seq_dim, input_dim).to(device)

        outputs, _, _ = model(images)
        _, predicted = torch.max(outputs.data, 1)
        total += labels.size(0)
        if torch.cuda.is_available():
            correct += (predicted.cpu() == labels.long().cpu()).sum()
        else:
            correct += (predicted == labels).sum()

    accuracy = 100. * correct.numpy() / total
    return accuracy


# train
acc = train(model, "smnist1", 10)

# test
# model = torch.load("model\smnist1\model_smnist1_9_73.44.pth")
# accuracy = test(model)
# print('Final accuracy: ', accuracy)

# mem traces
# model = torch.load("model\smnist1\model_smnist1_9_73.44.pth").to(device)
# for images, labels in test_loader:
#     images = images.view(-1, seq_dim, input_dim).to(device)

#     _, _, mem_trace = model(images)
#     mem_trace = mem_trace
#     mems = []
#     for ts in range(seq_dim):
#         mem = mem_trace[ts][1][0]
#         mem = torch.unsqueeze(mem, dim=0)
#         mems.append(mem)

#     mems = torch.cat(mems, dim=0)
#     pd.DataFrame(mems.data.cpu().numpy()).to_csv('mem_h1.csv')
#     sys.exit(0)
