<script lang="ts">
    import type { Latency } from "./data";
    import { formatSI } from "./utils";
  
    export let latency: Latency;
  </script>
  
  <table>
    <tr>
      <td></td>
      <td>Latency (cycles)</td>
    </tr>
      <tr>
        <td>Input</td>
        <td>{formatSI(latency.Input)}</td>
      </tr>
      <tr>
        <td>Internal</td>
        <td>{formatSI(latency.Internal)}</td>
      </tr>
      <tr>
        <td>Output</td>
        <td>{formatSI(latency.Output)}</td>
      </tr>
      <tr>
        <td>Compute</td>
        <td>{formatSI(latency.Compute)}</td>
      </tr>
      <tr>
        <td>Total</td>
        <td>{formatSI(latency.Total)}</td>
      </tr>
  </table>
  