<script lang="ts">
  import type { Energy } from "./data";
  import { formatSI } from "./utils";

  export let energy: Energy;
</script>

<table>
  <tr>
    <td>Component</td>
    <td>Leakage</td>
    <td>Dynamic</td>
    <td>Total</td>
  </tr>
  {#each ["Scheduler", "Router", "Controller", "Core", "SynMem", "NeuronMem", "Total"] as compName}
    <tr>
      <td>{compName}</td>
      <td>{formatSI(energy[compName].Leakage)}J</td>
      <td>{formatSI(energy[compName].Dynamic)}J</td>
      <td>{formatSI(energy[compName].Total)}J</td>
    </tr>
  {/each}
</table>
