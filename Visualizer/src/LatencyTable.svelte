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
        <td>{latency.Input}</td>
      </tr>
      <tr>
        <td>Internal</td>
        <td>{latency.Internal}</td>
      </tr>
      <tr>
        <td>Output</td>
        <td>{latency.Output}</td>
      </tr>
      <tr>
        <td>Compute</td>
        <td>{latency.Compute}</td>
      </tr>
      <tr>
        <td>Total</td>
        <td>{latency.Total}</td>
      </tr>
  </table>
  