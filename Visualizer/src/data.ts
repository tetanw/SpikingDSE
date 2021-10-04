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

export interface CoreReport {
  Type: "PE";
  CoreID: number;
  TS: number;
  NrSOPs: number;
  NeuronMemReads: number;
  NeuronMemWrites: number;
  SynMemReads: number;
  SynMemWrites: number;

  Spikes: SpikeMap;
  Latency: Latency;
  Energy: Energy;
}

export interface TimestepReport {
  Type: "Timestep";
  TS: number;
  Latency: Latency;
  Energy: Energy;
  SpikeRoutes: SpikeRoute[];
}

export interface SimReport {
  Type: "Sim";
  Latency: Latency;
  Energy: Energy;
}

export interface MappingReport {
  Type: "Mapping";
  Mapping: number[];
}

export type Anylisis = CoreReport | TimestepReport | SimReport | MappingReport;

export interface Report {
  Analyses: Anylisis[];
}

export function findCoreReport(
  report: Report,
  coreID: number,
  timestep: number
): CoreReport {
  const results = report.Analyses.filter(
    (analysis) =>
      analysis.Type == "PE" &&
      analysis.CoreID == coreID &&
      analysis.TS == timestep
  );

  return results[0] as CoreReport;
}

export function coreReports(report: Report): CoreReport[] {
  return report.Analyses.filter(e => e.Type == "PE") as CoreReport[];
}

export function findPossibleTimesteps(report: Report) {
  return coreReports(report).map(r => r.TS);
}

export function findPossibleCores(report: Report, timestep: number) {
  return coreReports(report).filter(r => r.TS == timestep).map(c => c.CoreID);
}

export function timestepReports(report: Report): TimestepReport[] {
  return report.Analyses.filter(e => e.Type == "Timestep") as TimestepReport[];
}

export function findTimestep(report: Report, timestep: number) {
  return timestepReports(report).find(ts => ts.TS == timestep);
}

export function simReports(report: Report): SimReport[] {
  return report.Analyses.filter(e => e.Type == "Sim") as SimReport[];
}

export function findSim(report: Report) {
  return simReports(report)[0];
}

export function mappingReports(report: Report): MappingReport[] {
  return report.Analyses.filter(e => e.Type == "Mapping") as MappingReport[];
}

export function findMapping(report: Report) {
  return mappingReports(report)[0];
}