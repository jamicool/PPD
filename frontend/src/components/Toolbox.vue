<template>
  <div class="toolbox">
    <div class="toolbox-header">
      <h3>Элементы</h3>
      <div class="search-box">
        <input 
          v-model="searchQuery" 
          placeholder="Поиск элементов..."
          type="text"
        >
      </div>
    </div>
    
    <div class="categories">
      <div 
        v-for="category in filteredCategories" 
        :key="category.category.name"
        class="category"
      >
        <h4 class="category-title">{{ category.category.name }}</h4>
        <div class="elements-grid">
          <div
            v-for="element in category.elements"
            :key="element.type"
            class="element-item"
            draggable="true"
            @dragstart="onDragStart($event, element.type)"
            @click="addElement(element.type)"
          >
            <div 
              class="element-icon"
              :style="{ backgroundColor: element.definition.color }"
            >
              {{ element.definition.icon }}
            </div>
            <span class="element-name">{{ element.definition.name }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { usePipelineStore } from '@/stores/pipeline-store'
import { configService } from '@/services/config-service'

const store = usePipelineStore()
const searchQuery = ref('')

const categories = computed(() => {
  if (!store.currentProject) return []
  return configService.getElementsByCategory(store.currentProject.type)
})

const filteredCategories = computed(() => {
  if (!searchQuery.value) return categories.value
  
  return categories.value.map(category => ({
    ...category,
    elements: category.elements.filter(element => 
      element.definition.name.toLowerCase().includes(searchQuery.value.toLowerCase()) ||
      element.type.toLowerCase().includes(searchQuery.value.toLowerCase())
    )
  })).filter(category => category.elements.length > 0)
})

const onDragStart = (event: DragEvent, elementType: string) => {
  if (event.dataTransfer) {
    event.dataTransfer.setData('text/plain', elementType)
    event.dataTransfer.effectAllowed = 'copy'
  }
}

const addElement = (elementType: string) => {
  if (!store.currentProject) return
  
  const definition = configService.getElementDefinition(elementType)
  if (!definition) return
  
  const defaultProps = configService.getDefaultProperties(elementType)
  
  const newNode = {
    type: elementType,
    position: { x: 100, y: 100 },
    properties: {
      ...defaultProps,
      name: configService.generateNodeName(elementType)
    },
    connections: []
  }
  
  store.addNode(newNode)
}

onMounted(async () => {
  await configService.loadConfig()
})
</script>

<style scoped>
.toolbox {
  width: 280px;
  background: white;
  border-right: 1px solid #bdc3c7;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.toolbox-header {
  padding: 1rem;
  border-bottom: 1px solid #ecf0f1;
}

.toolbox-header h3 {
  margin: 0 0 1rem 0;
  color: #2c3e50;
  font-size: 1.2rem;
}

.search-box input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #bdc3c7;
  border-radius: 4px;
  font-size: 0.9rem;
}

.categories {
  flex: 1;
  overflow-y: auto;
  padding: 0.5rem;
}

.category {
  margin-bottom: 1.5rem;
}

.category-title {
  margin: 0 0 0.5rem 0;
  color: #34495e;
  font-size: 0.9rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.elements-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem;
}

.element-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0.75rem 0.5rem;
  background: #f8f9fa;
  border: 1px solid #e9ecef;
  border-radius: 6px;
  cursor: grab;
  transition: all 0.2s ease;
  text-align: center;
}

.element-item:hover {
  border-color: #3498db;
  background: #e3f2fd;
  transform: translateY(-2px);
}

.element-item:active {
  cursor: grabbing;
}

.element-icon {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.2rem;
  margin-bottom: 0.5rem;
  color: white;
}

.element-name {
  font-size: 0.8rem;
  color: #2c3e50;
  font-weight: 500;
  line-height: 1.2;
}
</style>
