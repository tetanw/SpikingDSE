from torch.utils import data
from ssc_dataset import SSCZipDataset
import zipfile
from extract_inputs import *

seq_dim = 250
nr_items = 9981

test_dataset = SSCZipDataset("data/ssc-valid.zip")
test_loader = data.DataLoader(test_dataset, shuffle=False)
output = zipfile.ZipFile(
    "data/output.zip", "w", zipfile.ZIP_DEFLATED, compresslevel=9)

extract_inputs_1(test_loader, seq_dim, "inputs\ssc")
