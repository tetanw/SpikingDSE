<script lang="ts">
  import { findMapping, MappingReport } from "./data";

  export let report: FullReport;
  let selectedReport: MappingReport;
  $: selectedReport = findMapping(report);
</script>

{#if selectedReport}
  {#each selectedReport.Mapping as coreID, neuronID}
    <div>{neuronID} -> {coreID}</div>
  {/each}
{/if}
