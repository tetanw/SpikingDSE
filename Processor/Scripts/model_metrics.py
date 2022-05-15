import pandas
from model_stats import *


class Metrics():
    def __init__(self, stats: Stats, exp):
        self.exp = exp
        self.latency = exp["latency"] / 100_000_000
        self.static_energy = self.latency * stats.chip_static

        self.dyn_layer_read = 0.0
        self.dyn_layer_write = 0.0
        self.dyn_neuron_read = 0.0
        self.dyn_neuron_write = 0.0
        self.dyn_syn_read = 0.0
        self.dyn_syn_write = 0.0
        for c in self.cores():
            self.dyn_neuron_read += exp[f"{c}_neuronReads"] * \
                stats.neuron_mem_read
            self.dyn_neuron_write += exp[f"{c}_neuronWrites"] * \
                stats.neuron_mem_write
            self.dyn_layer_read += exp[f"{c}_layerReads"] * \
                stats.layer_mem_read
            self.dyn_layer_write += exp[f"{c}_layerWrites"] * \
                stats.layer_mem_write
            self.dyn_syn_read += exp[f"{c}_synapseReads"] * \
                stats.syn_mem_read
            self.dyn_syn_write += exp[f"{c}_synapseWrites"] * \
                stats.syn_mem_write
        self.dynamic_mem = self.dyn_neuron_read + self.dyn_layer_write + self.dyn_layer_read + \
            self.dyn_layer_write + self.dyn_syn_read + self.dyn_syn_write

        self.dynamic_alu = 0.0
        for c in self.cores():
            for l in self.layers(c):
                self.dynamic_alu += exp[f"{c}_{l}_integrates"] * \
                    s.layer_energies[l]["Integrate"]
                self.dynamic_alu += exp[f"{c}_{l}_integrates"] * \
                    s.layer_energies[l]["Integrate"]

        self.dynamic_router = 0.0
        for r in self.routers():
            self.dynamic_router += exp[f"{r}_nrHops"] * stats.router_dyn / 1E12

        self.total_energy = self.static_energy.sum() + self.dynamic_mem.sum() + \
            self.dynamic_alu.sum() + self.dynamic_router.sum()

        self.nr_samples = exp.shape[0]
        self.accuracy = (exp["predicted"] == exp["correct"]).sum() / self.nr_samples

    def routers(self):
        return ["router(0_0)", "router(0_1)", "router(0_2)", "router(0_3)", "router(1_0)", "router(1_1)", "router(1_2)", "router(1_3)"]

    def cores(self):
        return ["core1", "core2", "core3", "core4", "core5"]
        # return ["core1"]

    def layers(self, c):
        if c != "core5":
            return ["ALIF"]
        else:
            return ["output"]

    def print_summary(self):
        print(f"Total duration: {self.latency.sum()} s")
        print(f"Accuracy: {self.accuracy}")
        print(f"Energy:")
        print(f"  Static: {self.static_energy.sum()} J")
        print(f"  Dynamic:")
        print(f"    Core Mem: {self.dynamic_mem.sum()} J")
        print(f"      Layer read: {self.dyn_layer_read.sum()} J")
        print(f"      Layer write: {self.dyn_layer_write.sum()} J")
        print(f"      Neuron read: {self.dyn_neuron_read.sum()} J")
        print(f"      Neuron write: {self.dyn_neuron_write.sum()} J")
        print(f"      Synapse read: {self.dyn_syn_read.sum()} J")
        print(f"      Synapse write: {self.dyn_syn_write.sum()} J")
        print(f"    Core ALU: {self.dynamic_alu.sum()} J")
        print(f"    Router: {self.dynamic_router.sum()} J")
        print(f"  Total: {self.total_energy} J")


if __name__ == "__main__":
    exp = pandas.read_csv("res/exp/exp1/results/shd1/experiments.csv")

    s = Stats("res/exp/exp1/model.json", "Scripts\cost.json")
    s.print_summary()
    exp = pandas.read_csv("res/exp/exp1/results/shd1/experiments.csv")
    m = Metrics(s, exp)
    m.print_summary()
