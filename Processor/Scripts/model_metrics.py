import sys
import pandas
from model_costs import *

class Metrics():
    def __init__(self, cost: Costs, exp):
        self.exp = exp
        self.cost = cost

        self.cores = [f"core{core_id}" for core_id in range(
            0, cost.size) if f"core{core_id}_neuronReads" in exp]
        self.routers = [f"router({x}_{y})" for x in range(0, cost.width) for y in range(
            0, cost.height) if f"router({x}_{y})_nrHops" in exp]
        self.latency = exp["latency"] * 1E-12 # pS to s
        self.nr_active_cores = len(self.cores)
        self.static_energy = self.latency * cost.core_static * self.nr_active_cores

        self.dyn_layer_read = 0.0
        self.dyn_layer_write = 0.0
        self.dyn_neuron_read = 0.0
        self.dyn_neuron_write = 0.0
        self.dyn_syn_read = 0.0
        self.dyn_syn_write = 0.0
        self.dyn_compute_pop = 0.0
        self.dyn_compute_push = 0.0
        self.dyn_output_pop = 0.0
        self.dyn_output_push = 0.0
        self.nr_faults = 0
        self.sparsity = {}
        for c in self.cores:
            self.dyn_neuron_read += exp[f"{c}_neuronReads"] * \
                cost.neuron_mem_read
            self.dyn_neuron_write += exp[f"{c}_neuronWrites"] * \
                cost.neuron_mem_write
            self.dyn_layer_read += exp[f"{c}_layerReads"] * \
                cost.layer_mem_read
            self.dyn_layer_write += exp[f"{c}_layerWrites"] * \
                cost.layer_mem_write
            self.dyn_syn_read += exp[f"{c}_synapseReads"] * \
                cost.syn_mem_read
            self.dyn_syn_write += exp[f"{c}_synapseWrites"] * \
                cost.syn_mem_write
            self.dyn_compute_pop += exp[f"{c}_computePops"] * \
                cost.compute_buf_pops
            self.dyn_compute_push += exp[f"{c}_computePushes"] * \
                cost.compute_buf_pushes
            self.dyn_output_pop += exp[f"{c}_outputPops"] * \
                cost.output_buf_pops
            self.dyn_output_push += exp[f"{c}_outputPushes"] * \
                cost.output_buf_pushes
            self.sparsity[c] = exp[f"{c}_sparsity"].mean()
            self.nr_faults += exp[f"{c}_faultySpikes"].sum()
        self.total_sparsity = sum([value for value in self.sparsity.values()]) / len(self.cores)
        self.dynamic_mem = self.dyn_neuron_read + self.dyn_neuron_write + self.dyn_layer_read + \
            self.dyn_layer_write + self.dyn_syn_read + self.dyn_syn_write

        self.dynamic_alu = {}
        self.dynamic_alu_total = 0.0
        self.alu_util = 0.0
        self.recv_util = 0.0
        self.snd_util = 0.0
        for c in self.cores:
            for op in self.ops(c):
                nr_ops = exp[f"{c}_ops_{op}"].sum()
                energy_per_op = self.cost.alu_costs[op]["Dynamic"]
                energy = nr_ops * energy_per_op
                self.dynamic_alu_total += energy
                if op in self.dynamic_alu:
                    self.dynamic_alu[op] += energy
                else:
                    self.dynamic_alu[op] = energy
            self.alu_util += exp[f"{c}_alu_util"].mean()
            self.recv_util += exp[f"{c}_recv_util"].mean()
            self.snd_util += exp[f"{c}_snd_util"].mean()
        self.alu_util /= len(self.cores)
        self.recv_util /= len(self.cores)
        self.snd_util /= len(self.cores)

        self.dynamic_router = 0.0
        for r in self.routers:
            energy = exp[f"{r}_nrHops"] * cost.link_dyn_packet + exp[f"{r}_nrPacketSwitches"] * cost.router_dyn_packet
            self.dynamic_router += energy

        # determine average latency if it exists in the results
        if f"{self.routers[0]}_averageLat" in exp:
            self.averageLat = sum([exp[f"{r}_averageLat"].mean() for r in self.routers]) / len(self.routers)
        else:
            self.averageLat = float('NaN')

        self.total_energy = self.static_energy.sum() + self.dynamic_mem.sum() + \
            self.dynamic_alu_total + self.dynamic_router.sum()
        self.total_power = self.total_energy.sum() / self.latency.sum()

        self.nr_samples = exp.shape[0]
        self.accuracy = (exp["predicted"] == exp["correct"]
                         ).sum() / self.nr_samples
        self.nr_sops = 0
        for c in self.cores:
            self.nr_sops += exp[f"{c}_sops"].sum()
        self.sop_energy = self.total_energy / self.nr_sops

        self.inferences_per_second = self.nr_samples / self.latency.sum()
        self.sops_per_second = self.nr_sops / self.latency.sum()
        self.delay_per_inference = 1.0 / self.inferences_per_second
        self.throughput_eff = self.sops_per_second / (self.cost.chip_area / 1_000_000.0)

        # SOP energy is J/SOP
        # Throughput Eff is SOP/s/mm^2
        self.eat = self.throughput_eff / self.sop_energy # SOP^2/s/mm^2/J

    def layers(self, c):
        layers = []
        if f"{c}_ALIF_syncs" in exp:
            layers.append("ALIF")
        if f"{c}_output_syncs" in exp:
            layers.append("output")
        return layers

    def ops(self, c):
        cols = self.exp.columns.values.tolist()
        cols = [col.lstrip(f"{c}_ops_")
                for col in cols if col.startswith(f"{c}_ops_")]
        return cols

    def print_summary(self):
        # self.stats.print_summary()
        print(f"NrFaults: {self.nr_faults}")
        print(f"Sparity: {self.total_sparsity:.4f}")
        print(
            f"Total duration: {self.latency.sum():.2f} s ({self.inferences_per_second:.2f} inferences/s)")
        print(f"Total SOPs: {self.nr_sops.sum():,.2f}")
        print(f"Throughput:")
        print(f"  SOP: {self.sops_per_second:,.0f} SOP/s")
        print(f"Accuracy: {self.accuracy:.4f} ({self.accuracy*100:.2f}%)")
        print(f"Energy:")
        print(f"  Synaptic energy: {self.sop_energy * 1E12:.2f} pJ")
        print(
            f"  Static: {self.static_energy.sum():.3f} J ({c.core_static * self.nr_active_cores * 1E6:,.3f} uW)")
        print(f"  Dynamic:")
        print(f"    Core Mem: {self.dynamic_mem.sum():.3f} J")
        print(f"      Layer read: {self.dyn_layer_read.sum():.3f} J")
        print(f"      Layer write: {self.dyn_layer_write.sum():.3f} J")
        print(f"      Neuron read: {self.dyn_neuron_read.sum():.3f} J")
        print(f"      Neuron write: {self.dyn_neuron_write.sum():.3f} J")
        print(f"      Synapse read: {self.dyn_syn_read.sum():.3f} J")
        print(f"      Synapse write: {self.dyn_syn_write.sum():.3f} J")
        print(f"      Compute pop: {self.dyn_compute_pop.sum():.3f} J")
        print(f"      Compute push: {self.dyn_compute_push.sum():.3f} J")
        print(f"      Output pop: {self.dyn_output_pop.sum():.3f} J")
        print(f"      Output push: {self.dyn_output_push.sum():.3f} J")
        print(f"    Core ALU: {self.dynamic_alu_total:.3f} J")
        for op, op_energy in self.dynamic_alu.items():
            print(f"      {op}: {op_energy:.3f} J")
        print(f"    Router: {self.dynamic_router.sum():.3f} J")
        print(
            f"  Total: {self.total_energy:.3f} J ({self.total_power*1E3:.2f} mW)")
        print(f"EAT: {self.eat*1E-12}")
        print(f"  Energy: {self.sop_energy*1E12} pJ / SOP")
        print(f"  Throughput Eff: {self.throughput_eff:,} SOP/s/mm2")


if __name__ == "__main__":
    expName = sys.argv[1]
    modelName = sys.argv[2]

    exp = pandas.read_csv(
        f"res/exp/{expName}/results/{modelName}/experiments.csv")
    c = Costs(f"res/exp/{expName}/model.json")
    m = Metrics(c, exp)
    m.print_summary()
