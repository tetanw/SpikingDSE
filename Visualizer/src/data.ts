export interface EnergyMetric {
  Leakage: number;
  Dynamic: number;
  Total: number;
}

export interface LatencyMetric {
  Cycles: number;
  Secs: number;
}

export interface Latency {
  Input: LatencyMetric;
  Internal: LatencyMetric;
  Output: LatencyMetric;
  Compute: LatencyMetric;
  Total: LatencyMetric;
  TotalSecs: LatencyMetric;
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
  Type: "PE";
  CoreID: number;
  TS: number;

  Spikes: SpikeMap;
  Memory: Memory;
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

export type Report = CoreReport | TimestepReport | SimReport | MappingReport;

export interface FullReport {
  Reports: Report[];
}

export function findCoreReport(
  report: FullReport,
  coreID: number,
  timestep: number
): CoreReport {
  const results = report.Reports.filter(
    (analysis) =>
      analysis.Type == "PE" &&
      analysis.CoreID == coreID &&
      analysis.TS == timestep
  );

  return results[0] as CoreReport;
}

export function coreReports(report: FullReport): CoreReport[] {
  return report.Reports.filter(e => e.Type == "PE") as CoreReport[];
}

export function findPossibleTimesteps(report: FullReport) {
  return coreReports(report).map(r => r.TS);
}

export function findPossibleCores(report: FullReport, timestep: number) {
  return coreReports(report).filter(r => r.TS == timestep).map(c => c.CoreID);
}

export function timestepReports(report: FullReport): TimestepReport[] {
  return report.Reports.filter(e => e.Type == "Timestep") as TimestepReport[];
}

export function findTimestep(report: FullReport, timestep: number) {
  return timestepReports(report).find(ts => ts.TS == timestep);
}

export function simReports(report: FullReport): SimReport[] {
  return report.Reports.filter(e => e.Type == "Sim") as SimReport[];
}

export function findSim(report: FullReport) {
  return simReports(report)[0];
}

export function mappingReports(report: FullReport): MappingReport[] {
  return report.Reports.filter(e => e.Type == "Mapping") as MappingReport[];
}

export function findMapping(report: FullReport) {
  return mappingReports(report)[0];
}