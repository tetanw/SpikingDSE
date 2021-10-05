export interface EnergyMetric {
  Leakage: number;
  Dynamic: number;
  Total: number;
}

export interface Latency {
  Input: number;
  Internal: number;
  Output: number;
  Compute: number;
  Total: number;
  TotalSecs: number;
}

export interface Energy {
  Core: EnergyMetric;
  Router: EnergyMetric;
  Scheduler: EnergyMetric;
  Controller: EnergyMetric;
  NeuronMem: EnergyMetric;
  SynMem: EnergyMetric;
  Total: EnergyMetric;
}

export interface SpikeMap {
  Input: number[];
  Internal: number[];
  Output: number[];
}

export interface SpikeRoute {
  ID: number;
  Src: number;
  Dest: number[];
}

export interface Memory {
  NeuronReads: number;
  NeuronWrites: number;
  SynReads: number;
  SynWrites: number;
}

export interface CoreReport {
  CoreID: number;
  TS: number;

  Spikes: SpikeMap;
  Memory: Memory;
  Latency: Latency;
  Energy: Energy;
}

export interface TimestepReport {
  TS: number;
  Latency: Latency;
  Energy: Energy;
  SpikeRoutes: SpikeRoute[];
  Cores: CoreReport[];
}

export interface SimReport {
  Mapping: MappingReport;
  Latency: Latency;
  Energy: Energy;
  Timesteps: TimestepReport[];
}

export interface MappingReport {
  Mapping: number[];
}

export function findCoreReport(
  report: SimReport,
  coreID: number,
  timestep: number
): CoreReport {
  return report.Timesteps[timestep].Cores[coreID];
}

export function findPossibleTimesteps(report: SimReport) {
  return report.Timesteps.map(ts => ts.TS);
}

export function findPossibleCores(report: SimReport, timestep: number) {
  return report.Timesteps[timestep].Cores.map(c => c.CoreID);
}

export function findTimestep(report: SimReport, timestep: number) {
  return report.Timesteps[timestep];
}

export function findMapping(report: SimReport) {
  return report.Mapping;
}