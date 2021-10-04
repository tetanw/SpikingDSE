<script lang="ts">
  import {
    findPossibleTimesteps,
    findTimestep,
    Report,
    TimestepReport,
  } from "./data";
  import EnergyTable from "./EnergyTable.svelte";

  export let report: Report;
  let possibleTimesteps = [];
  let selectedTimestepID: number = -1;
  let selectedTimestep: TimestepReport;
  $: {
    possibleTimesteps = findPossibleTimesteps(report);
    if (
      selectedTimestepID == -1 ||
      possibleTimesteps.indexOf(selectedTimestepID) == -1
    ) {
      selectedTimestepID = possibleTimesteps[0];
    }
  }
  $: selectedTimestep = findTimestep(report, selectedTimestepID);
</script>

<div>
  <label for="timesteps">Timestep:</label>
  <select id="timesteps" bind:value={selectedTimestepID}>
    {#each possibleTimesteps as timestep}
      <option value={timestep}>{timestep}</option>
    {/each}
  </select>
</div>

{#if selectedTimestep}
  <EnergyTable energy={selectedTimestep.Energy} />

  {#each selectedTimestep.SpikeRoutes as route}
    <div>
      [{route.ID}] {route.Src} -> &lbrace{route.Dest.join(", ")}&rbrace
    </div>
  {/each}
{/if}
