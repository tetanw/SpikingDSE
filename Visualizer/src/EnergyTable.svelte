<script lang="ts">
  import type { Energy } from "./data";
  import { formatSI } from "./utils";

  export let energy: Energy;
  export let time: number;
</script>

<table class="table table-striped table-hover">
  <caption>Energy usage by component</caption>
  <thead>
    <tr>
      <td>Component</td>
      <td>Leakage</td>
      <td>Dynamic</td>
      <td>Total</td>
    </tr>
  </thead>
  <tbody>
    {#each ["Scheduler", "Router", "Controller", "Core", "SynMem", "NeuronMem", "Total"] as compName}
      <tr>
        <td>{compName}</td>
        <td>{formatSI(energy[compName].Leakage)}J ({formatSI(energy[compName].Leakage / time)}W)</td>
        <td>{formatSI(energy[compName].Dynamic)}J ({formatSI(energy[compName].Dynamic / time)}W)</td>
        <td>{formatSI(energy[compName].Total)}J ({formatSI(energy[compName].Total / time)}W)</td>
      </tr>
    {/each}
  </tbody>
</table>
