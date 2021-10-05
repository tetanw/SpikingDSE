<script lang="ts">
  import type { CoreReport, FullReport, TimestepReport } from "./data";
  import CoreViewer from "./CoreViewer.svelte";
  import TimestepViewer from "./TimestepViewer.svelte";
  import SimViewer from "./SimViewer.svelte";
  import MappingViewer from "./MappingViewer.svelte";

  function onFileChange(event) {
    const files = event.target.files;
    selectedFile = files[0];
    loadFile();
  }

  function loadFile() {
    const reader = new FileReader();
    reader.addEventListener("load", (event) => {
      const txt = event.target.result as string;
      const obj = JSON.parse(txt);

      report = obj;
    });
    reader.readAsText(selectedFile);
  }

  function onTimestepChanged() {}

  function onTabSelected(name) {
    selectedTab = name;
  }

  const CoreTab = "Core";
  const TimestepTab = "Timestep";
  const SimTab = "Sim";
  const MappingTab = "Mapping";
  const tabNames = [CoreTab, TimestepTab, SimTab, MappingTab];

  let selectedTab = tabNames[0];
  let selectedFile: File;
  let report: FullReport;
  let timesteps = ["-"];
</script>

<main>
  <label for="report-file">Choose the report file:</label>
  <input
    type="file"
    id="report-file"
    accept="application/json"
    on:change={onFileChange}
  />

  <div class="tab">
    {#each tabNames as name}
      <button class="tablinks" on:click={() => onTabSelected(name)}
        >{name}</button
      >
    {/each}
  </div>

  {#if report}
    {#if selectedTab == CoreTab}
      <CoreViewer {report} />
    {:else if selectedTab == TimestepTab}
      <TimestepViewer {report} />
    {:else if selectedTab == SimTab}
      <SimViewer {report} />
    {:else if selectedTab == MappingTab}
      <MappingViewer {report} />
    {/if}
  {/if}
</main>
