<template>
  <div class="designer-view">
    <Toolbox />
    <PipelineEditor />
    <PropertyPanel />
  </div>
</template>

<script setup lang="ts">
import Toolbox from '@/components/Toolbox.vue'
import PipelineEditor from '@/components/PipelineEditor.vue'
import PropertyPanel from '@/components/PropertyPanel.vue'
</script>

<style scoped>
.designer-view {
  flex: 1;
  display: flex;
  height: 100%;
  width: 100%;
}
</style>
