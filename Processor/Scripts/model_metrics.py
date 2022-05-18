import pandas
from model_stats import *


class Metrics():
    def __init__(self, stats: Stats, exp):
        self.exp = exp
        self.cost = json.load(open("Scripts/cost.json"))

        self.cores = [f"core{core_id}" for core_id in range(
            1, stats.size) if f"core{core_id}_neuronReads" in exp]
        self.routers = [f"router({x}_{y})" for x in range(0, stats.width) for y in range(
            0, stats.height) if f"router({x}_{y})_nrHops" in exp]
        self.latency = exp["latency"] * 1E-12
        nr_active_cores = len(self.cores)
        self.static_energy = self.latency * stats.core_static * nr_active_cores

        self.dyn_layer_read = 0.0
        self.dyn_layer_write = 0.0
        self.dyn_neuron_read = 0.0
        self.dyn_neuron_write = 0.0
        self.dyn_syn_read = 0.0
        self.dyn_syn_write = 0.0
        for c in self.cores:
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

        self.dynamic_alu = {}
        self.dynamic_alu_total = 0.0
        for c in self.cores:
            for op in self.ops(c):
                energy = exp[f"{c}_ops_{op}"] * self.cost["ALU"][op]["Dynamic"]
                self.dynamic_alu_total += energy
                if op in self.dynamic_alu:
                    self.dynamic_alu[op] += energy
                else:
                    self.dynamic_alu[op] = energy

        self.dynamic_router = 0.0
        for r in self.routers:
            self.dynamic_router += exp[f"{r}_nrHops"] * stats.router_dyn

        self.total_energy = self.static_energy.sum() + self.dynamic_mem.sum() + \
            self.dynamic_alu_total.sum() + self.dynamic_router.sum()

        self.nr_samples = exp.shape[0]
        self.accuracy = (exp["predicted"] == exp["correct"]
                         ).sum() / self.nr_samples

    def layers(self, c):
        layers = []
        if f"{c}_ALIF_syncs" in exp:
            layers.append("ALIF")
        if f"{c}_output_syncs" in exp:
            layers.append("output")
        return layers

    def ops(self, c):
        cols = self.exp.columns.values.tolist()
        cols = [col.removeprefix(f"{c}_ops_")
                for col in cols if col.startswith(f"{c}_ops_")]
        return cols

    def print_summary(self):
        print(f"Total duration: {self.latency.sum()} s")
        print(f"Accuracy: {self.accuracy} ({self.accuracy*100}%)")
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
        print(f"    Core ALU: {self.dynamic_alu_total.sum()} J")
        for op, op_energy in self.dynamic_alu.items():
            print(f"      {op}: {op_energy.sum()} J")
        print(f"    Router: {self.dynamic_router.sum()} J")
        print(f"  Total: {self.total_energy} J")


if __name__ == "__main__":
    exp = pandas.read_csv("res/exp/exp1/results/ssc2/experiments.csv")

    s = Stats("res/exp/exp1/model.json", "Scripts\cost.json")
    s.print_summary()
    exp = pandas.read_csv("res/exp/exp1/results/ssc2/experiments.csv")
    m = Metrics(s, exp)
    m.print_summary()
