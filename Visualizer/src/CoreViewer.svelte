<script lang="ts">
  import {
    CoreReport,
    findCoreReport,
    findPossibleCores,
    findPossibleTimesteps,
    FullReport,
  } from "./data";
  import EnergyTable from "./EnergyTable.svelte";
  import LatencyTable from "./LatencyTable.svelte";

  export let report: FullReport;

  let selectedCore: CoreReport;
  let possibleTimesteps = [];
  let selectedTimestepID: number = -1;
  let possibleCores = [];
  let selectedCoreID: number = -1;
  $: {
    possibleTimesteps = findPossibleTimesteps(report);
    if (selectedTimestepID == -1 || possibleTimesteps.indexOf(selectedTimestepID) == -1)
    {
      selectedTimestepID = possibleTimesteps[0];
    }
  }
  $: {
    possibleCores = findPossibleCores(report, selectedTimestepID);
    if (selectedCoreID == -1 || possibleCores.indexOf(selectedCoreID) == -1) {
      selectedCoreID = possibleCores[0];
    }
  }
  $: selectedCore = findCoreReport(report, selectedCoreID, selectedTimestepID);
</script>

<div>
  <label for="timesteps">Timestep:</label>
  <select id="timesteps" bind:value={selectedTimestepID}>
    {#each possibleTimesteps as timestep}
      <option value={timestep}>{timestep}</option>
    {/each}
  </select>
</div>

<div>
  <label for="cores">Cores:</label>
  <select id="cores" bind:value={selectedCoreID}>
    {#each possibleCores as core}
      <option value={core}>{core}</option>
    {/each}
  </select>
</div>

{#if selectedCore}
  <div><b>CoreID:</b> {selectedCore.CoreID}</div>
  <div><b>TS:</b> {selectedCore.TS}</div>
  <div />
  <div><b>#NeuronMemReads:</b> {selectedCore.Memory.NeuronReads}</div>
  <div><b>#NeuronMemWrites:</b> {selectedCore.Memory.NeuronWrites}</div>
  <div><b>#SynMemReads:</b> {selectedCore.Memory.SynReads}</div>
  <div><b>#SynMemWrites:</b> {selectedCore.Memory.SynWrites}</div>
  <div />
  <div><b>Input spikes:</b>{selectedCore.Spikes.Input.join(", ")}</div>
  <div><b>Internal spikes:</b>{selectedCore.Spikes.Internal.join(", ")}</div>
  <div><b>Output spikes:</b>{selectedCore.Spikes.Output.join(", ")}</div>

  <EnergyTable energy={selectedCore.Energy} />
  <LatencyTable latency={selectedCore.Latency} />
{/if}
