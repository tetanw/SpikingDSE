<script lang="ts">
  import type { CoreReport, SimReport, TimestepReport } from "./data";
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
  let report: SimReport;
</script>

<main class="container">
  <h1>SpikingSNN Visualizer</h1>
  <div class="mb-3">
    <label for="report-file" class="form-label">Choose report file:</label>
    <input
      class="form-control"
      type="file"
      id="report-file"
      on:change={onFileChange}
    />
  </div>

  {#if report}
    <ul class="nav nav-tabs">
      {#each tabNames as name}
        <li class="nav-item" on:click={() => onTabSelected(name)}>
          {#if name == selectedTab}
            <a class="nav-link active">{name}</a>
          {:else}
            <a class="nav-link">{name}</a>
          {/if}
        </li>
      {/each}
    </ul>

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
