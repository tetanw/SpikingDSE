<script lang="ts">
  import {
    findPossibleTimesteps,
    findSim,
    findTimestep,
    Report,
    SimReport,
    TimestepReport,
  } from "./data";
  import EnergyTable from "./EnergyTable.svelte";

  export let report: Report;
  let selectedReport: SimReport;
  $: selectedReport = findSim(report);
</script>

{#if selectedReport}
  <EnergyTable energy={selectedReport.Energy} />
{/if}
