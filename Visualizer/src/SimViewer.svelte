<script lang="ts">
  import {
    findPossibleTimesteps,
    findSim,
    findTimestep,
    FullReport,
    SimReport,
    TimestepReport,
  } from "./data";
  import EnergyTable from "./EnergyTable.svelte";

  export let report: FullReport;
  let selectedReport: SimReport;
  $: selectedReport = findSim(report);
</script>

{#if selectedReport}
  <EnergyTable energy={selectedReport.Energy} />
{/if}
