import numpy as np
import pandas as pd
import torch
import os
from models import *

def save_alif_layer(layer, dest_dir, input_name, layer_name):
    data = np.transpose(layer.input.detach().cpu().numpy())
    pd.DataFrame(data).to_csv(f"{dest_dir}/weights_{input_name}_2_{layer_name}.csv")

    data = np.transpose(layer.rec.detach().cpu().numpy())
    pd.DataFrame(data).to_csv(f"{dest_dir}/weights_{layer_name}_2_{layer_name}.csv")

    data = layer.tau_m.detach().cpu().numpy()
    pd.DataFrame(data).to_csv(f"{dest_dir}/tau_m_{layer_name}.csv")

    data = layer.tau_adp.detach().cpu().numpy()
    pd.DataFrame(data).to_csv(f"{dest_dir}/tau_adp_{layer_name}.csv")

    data = layer.bias.detach().cpu().numpy()
    pd.DataFrame(data).to_csv(f"{dest_dir}/bias_{layer_name}.csv")

def save_output_layer(layer, dest_dir, input_name, layer_name):
    data = np.transpose(layer.input.detach().cpu().numpy())
    pd.DataFrame(data).to_csv(f"{dest_dir}/weights_{input_name}_2_{layer_name}.csv")

    data = layer.tau_m.detach().cpu().numpy()
    pd.DataFrame(data).to_csv(f"{dest_dir}/tau_m_{layer_name}.csv")

model = torch.load('model\ssc-test\model_ssc-test_3_68.4400360685302.pth')
dest_dir = f"./extracted/ssc-test"
if not os.path.isdir(dest_dir):
    os.mkdir(dest_dir)

save_alif_layer(model.hidden[0], dest_dir, "i", "h1")
save_alif_layer(model.hidden[1], dest_dir, "h1", "h2")
# save_alif_layer(model.hidden[2], dest_dir, "h2", "h3")
save_output_layer(model.output, dest_dir, "h2", "o")