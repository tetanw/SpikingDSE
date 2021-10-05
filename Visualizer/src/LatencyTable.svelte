<script lang="ts">
    import type { Latency } from "./data";
    import { formatSI } from "./utils";
  
    export let latency: Latency;
  </script>
  
  <table>
    <tr>
      <td></td>
      <td>Latency (cycles)</td>
      <td>Latency (secs)</td>
    </tr>
      <tr>
        <td>Input</td>
        <td>{formatSI(latency.Input.Cycles)}</td>
        <td>{formatSI(latency.Input.Secs)}S</td>
      </tr>
      <tr>
        <td>Internal</td>
        <td>{formatSI(latency.Internal.Cycles)}</td>
        <td>{formatSI(latency.Internal.Secs)}S</td>
      </tr>
      <tr>
        <td>Output</td>
        <td>{formatSI(latency.Output.Cycles)}</td>
        <td>{formatSI(latency.Output.Secs)}S</td>
      </tr>
      <tr>
        <td>Compute</td>
        <td>{formatSI(latency.Compute.Cycles)}</td>
        <td>{formatSI(latency.Compute.Secs)}S</td>
      </tr>
      <tr>
        <td>Total</td>
        <td>{formatSI(latency.Total.Cycles)}</td>
        <td>{formatSI(latency.Total.Secs)}S</td>
      </tr>
  </table>
  