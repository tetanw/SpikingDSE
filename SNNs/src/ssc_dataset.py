from torch.utils.data import Dataset
import torch
import numpy as np
import zipfile

class SSCZipDataset(Dataset):
    # Characterizes a dataset for PyTorch
    def __init__(self, path, transform=None):
        self.archive = zipfile.ZipFile(path, mode="r")
        self.data_paths = self.archive.namelist()
        self.transform = transform

    def __len__(self):
        return len(self.data_paths)

    def __getitem__(self, index):
        file = self.archive.open(self.data_paths[index])
        x = torch.from_numpy(np.load(file)).float()
        y_ = self.data_paths[index].split('_')[-1]
        y_ = int(y_.split('.')[0])
        y_tmp= np.array([int(y_)])
        y = torch.from_numpy(y_tmp).float()
        if self.transform:
            x = self.transform(x)
        return x, y

