import torch
import torch.nn as nn
import torch.nn.functional as F


class ActFun(torch.autograd.Function):
    @staticmethod
    def forward(ctx, input):  # input = membrane potential- threshold
        ctx.save_for_backward(input)
        return input.gt(0).float()  # is firing ???

    @staticmethod
    def backward(ctx, grad_output):  # approximate the gradients
        input, = ctx.saved_tensors
        grad_input = grad_output.clone()

        gamma = .5  # gradient scale
        lens = 0.5  # hyper-parameters of approximate function

        temp = torch.exp(-(input**2) / (2 * lens**2)) / torch.sqrt(
            2 * torch.tensor(torch.pi)) / lens
        return grad_input * temp.float() * gamma


class ALIFLayer(nn.Module):
    def __init__(self, size_in, size_out, dt=1.0, thr0=0.01, name=""):
        super().__init__()
        self.dt = dt
        self.thr0 = thr0
        self.name = name

        # decay constants
        self.tau_m = nn.Parameter(torch.Tensor(size_out))
        nn.init.constant_(self.tau_m, 10.0)
        self.tau_adp = nn.Parameter(torch.Tensor(size_out))
        nn.init.constant_(self.tau_adp, 100.0)

        # bias
        self.bias = nn.Parameter(torch.Tensor(size_out))
        nn.init.constant_(self.bias, 0.0)

        # recurrent weights
        self.rec = nn.Parameter(torch.Tensor(size_out, size_out))
        nn.init.xavier_uniform_(self.rec)

        # input weights
        self.input = nn.Parameter(torch.Tensor(size_in, size_out))
        nn.init.xavier_uniform_(self.input)

    def forward(self, mem, thr, prev_spikes, spikes):
        # decay constants
        alpha = torch.exp(-1. * self.dt / self.tau_m).cuda()
        ro = torch.exp(-1. * self.dt / self.tau_adp).cuda()

        # new threshold
        beta = 1.8
        thr = ro * thr + (1 - ro) * prev_spikes
        B = self.thr0 + beta * thr

        # new potential
        inputs = torch.matmul(prev_spikes, self.rec) + torch.matmul(spikes, self.input)
        mem = mem * alpha + (1 - alpha) * inputs - B * prev_spikes * self.dt

        # spike
        inputs = mem - B + self.bias
        spikes_new = ActFun.apply(inputs)

        return mem, thr, spikes_new


class OutputLayer(nn.Module):
    def __init__(self, size_in, size_out, dt=1.0):
        super().__init__()
        self.dt = dt

        # weights
        self.input = nn.Parameter(torch.Tensor(size_in, size_out))
        nn.init.xavier_uniform_(self.input)

        # decay constant
        self.tau_m = nn.Parameter(torch.zeros(size_out))
        nn.init.constant_(self.tau_m, 10.0)

    def forward(self, mem, spikes):
        inputs = torch.matmul(spikes, self.input)
        alpha = torch.exp(-1.0 * self.dt / self.tau_m)
        mem_new = mem * alpha + (1.0 - alpha) * inputs
        return mem_new


class SRNN(nn.Module):
    def __init__(self, input_size, hidden_size, output_size, thr0=0.01):
        super(SRNN, self).__init__()
        self.thr0 = thr0
        self.hidden_size = hidden_size
        self.input_size = input_size
        self.output_size = output_size

        sizes = [input_size] + hidden_size
        self.hidden = nn.ModuleList([ALIFLayer(
            sizes[i], sizes[i+1], thr0=thr0, name=f"h{i+1}") for i in range(0, len(hidden_size))])
        self.output = OutputLayer(sizes[-1], output_size)

    def forward(self, input):
        batch_size, seq_num, _ = input.shape

        # hidden layers
        spikes = [torch.zeros(batch_size, size).cuda() for size in self.hidden_size]
        mem = [torch.zeros(batch_size, size).cuda() for size in self.hidden_size]
        thr = [self.thr0 for _ in self.hidden_size]

        # output
        mem_output = torch.zeros(batch_size, self.output_size).cuda()
        sum_output = torch.zeros(batch_size, self.output_size).cuda()

        for ts in range(seq_num):
            # output
            mem_output = self.output(mem_output, spikes[-1])
            if ts > 0:
                sum_output = sum_output + F.softmax(mem_output, dim=1)

            # hidden layers
            for i in reversed(range(0, len(self.hidden_size))):
                # hidden 2
                forward_spikes = spikes[i - 1] if i >0 else input[:, ts, :]
                mem[i], thr[i], spikes[i] = self.hidden[i](
                    mem[i], thr[i], spikes[i], forward_spikes)

        return sum_output