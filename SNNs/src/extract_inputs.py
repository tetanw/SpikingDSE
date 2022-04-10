import os

# extract spikes from the input spike traces
def extract_inputs_1(loader, seq_dim, path):
    if not os.path.isdir(path):
        os.makedirs(path)

    for step, (x, y) in enumerate(loader):

        if step % 50 == 0:
            print(f"Sample: {step}")

        with open(f"{path}\input_" + str(step) + ".trace", "w") as input_file:
            out = int(y.numpy()[0])
            input_file.write(str(out) + "\n")
            for ts in range(0, seq_dim):
                spikes = x[0, ts, :].nonzero(as_tuple=True)[0].numpy()
                spike_str = [str(spike) for spike in spikes]
                input_file.write(str(ts) + "," + ",".join(spike_str) + "\n")

# extract spikes from the spikes from the first layer of the execution
def extract_inputs_2(loader, model, seq_dim, path):
    if not os.path.isdir(path):
        os.makedirs(path)

    for step, (x, y) in enumerate(loader):
        _, spikes, _ = model(x.to("cpu"))

        if step % 50 == 0:
            print(f"Sample: {step}")

        with open(f"{path}/input_" + str(step) + ".trace", "w") as input_file:
            correct = int(y.numpy()[0])
            input_file.write(str(correct) + "\n")
            input_file.write("0,\n")
            for ts in range(1, seq_dim):
                spikes_ts = spikes[ts - 1][0].flatten().nonzero().flatten().numpy()
                spike_str = [str(spike) for spike in spikes_ts]
                input_file.write(str(ts) + "," + ",".join(spike_str) + "\n")