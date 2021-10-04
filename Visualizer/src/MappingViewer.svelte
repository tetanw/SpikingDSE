<script lang="ts">
  import { findMapping, MappingReport, Report } from "./data";

  export let report: Report;
  let selectedReport: MappingReport;
  $: selectedReport = findMapping(report);
</script>

{#if selectedReport}
  <div><b>Mapping: </b></div>
  {#each selectedReport.Mapping as coreID, neuronID}
    <div>{neuronID} -> {coreID}</div>
  {/each}
{/if}
