<script lang="ts">
  import { SimReport } from "./data";
  import EnergyTable from "./EnergyTable.svelte";

  export let report: SimReport;
</script>

{#if report}
  <EnergyTable energy={report.Energy} />
{/if}
