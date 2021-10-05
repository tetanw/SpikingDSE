<script lang="ts">
  import type { Latency } from "./data";
  import { formatNumber, formatSI } from "./utils";

  export let frequency: number;
  export let latency: Latency;

  function toSecs(cycles) {
    return cycles / frequency;
  }
</script>

<table class="table table-striped table-hover">
  <caption>Latency by component</caption>
  <thead>
    <tr>
      <td />
      <td>Latency (cycles)</td>
      <td>Latency (secs)</td>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>Input</td>
      <td>{formatNumber(latency.Input)}</td>
      <td>{formatSI(toSecs(latency.Input))}s</td>
    </tr>
    <tr>
      <td>Internal</td>
      <td>{formatNumber(latency.Internal)}</td>
      <td>{formatSI(toSecs(latency.Internal))}s</td>
    </tr>
    <tr>
      <td>Output</td>
      <td>{formatNumber(latency.Output)}</td>
      <td>{formatSI(toSecs(latency.Output))}s</td>
    </tr>
    <tr>
      <td>Compute</td>
      <td>{formatNumber(latency.Compute)}</td>
      <td>{formatSI(toSecs(latency.Compute))}s</td>
    </tr>
    <tr>
      <td>Total</td>
      <td>{formatNumber(latency.Total)}</td>
      <td>{formatSI(toSecs(latency.Total))}s</td>
    </tr>
  </tbody>
</table>
