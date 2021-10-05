<script lang="ts">
  import { findMapping, MappingReport, FullReport } from "./data";

  export let report: FullReport;
  let selectedReport: MappingReport;
  $: selectedReport = findMapping(report);
</script>

{#if selectedReport}
  <div><b>Mapping: </b></div>
  {#each selectedReport.Mapping as coreID, neuronID}
    <div>{neuronID} -> {coreID}</div>
  {/each}
{/if}
