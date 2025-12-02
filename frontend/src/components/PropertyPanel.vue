<template>
  <div class="property-panel">
    <div class="panel-header">
      <h3>Свойства</h3>
      <button 
        v-if="store.selectedElement" 
        @click="deleteSelectedElement"
        class="delete-btn"
      >
        Удалить
      </button>
    </div>
    
    <div class="panel-content">
      <div v-if="!store.selectedElement" class="no-selection">
        <p>Выберите элемент для редактирования свойств</p>
      </div>
      
      <div v-else-if="isNode(store.selectedElement) && store.selectedElement.type === 'pipe'" class="pipe-properties">
        <h4>Труба: {{ store.selectedElement.properties.name || 'Без имени' }}</h4>
        
        <div class="property-group">
          <label>Начало X</label>
          <input 
            type="number" 
            v-model.number="position.x"
            @change="updateNodePosition"
          >
        </div>
        
        <div class="property-group">
          <label>Начало Y</label>
          <input 
            type="number" 
            v-model.number="position.y"
            @change="updateNodePosition"
          >
        </div>
        
        <div class="property-group">
          <label>Конец X</label>
          <input 
            type="number" 
            v-model.number="pipeEnd.x"
            @change="updatePipeEnd"
          >
        </div>
        
        <div class="property-group">
          <label>Конец Y</label>
          <input 
            type="number" 
            v-model.number="pipeEnd.y"
            @change="updatePipeEnd"
          >
        </div>
        
        <div class="property-group">
          <label>Длина</label>
          <span class="readonly-value">{{ calculatePipeLength() }} м</span>
        </div>
        
        <div class="property-group">
          <label>Диаметр (мм)</label>
          <input 
            type="number" 
            v-model.number="properties.diameter"
            @change="updateProperties"
            min="1"
            max="1000"
          >
        </div>
        
        <div class="property-group">
          <label>Длина (м)</label>
          <input 
            type="number" 
            v-model.number="properties.length"
            @change="updateProperties"
            min="0.1"
            step="0.1"
          >
        </div>
        
        <div class="property-group">
          <label>Шероховатость (мм)</label>
          <input 
            type="number" 
            v-model.number="properties.roughness"
            @change="updateProperties"
            min="0"
            step="0.001"
          >
        </div>
        
        <div class="property-group">
          <label>Материал</label>
          <select v-model="properties.material" @change="updateProperties">
            <option value="steel">Сталь</option>
            <option value="copper">Медь</option>
            <option value="plastic">Пластик</option>
            <option value="composite">Композит</option>
          </select>
        </div>
        
        <div class="connection-controls">
          <div class="connection-buttons">
            <button @click="startConnection('start')" class="btn-primary" :disabled="store.connectionMode.active">
              Соединить начало
            </button>
            <button @click="startConnection('end')" class="btn-primary" :disabled="store.connectionMode.active">
              Соединить конец
            </button>
          </div>
          
          <div v-if="store.connectionMode.active" class="connection-hint">
            <p>Выберите целевой узел на схеме</p>
            <button @click="store.cancelConnection" class="btn-secondary">
              Отменить соединение
            </button>
          </div>
        </div>
      </div>
      
      <div v-else-if="isNode(store.selectedElement)" class="node-properties">
        <h4>{{ getElementName(store.selectedElement) }}</h4>
        
        <div class="property-group">
          <label>Позиция X</label>
          <input 
            type="number" 
            v-model.number="position.x"
            @change="updateNodePosition"
          >
        </div>
        
        <div class="property-group">
          <label>Позиция Y</label>
          <input 
            type="number" 
            v-model.number="position.y"
            @change="updateNodePosition"
          >
        </div>
        
        <div 
          v-for="(property, key) in elementDefinition?.propertySchema || {}" 
          :key="key"
          class="property-group"
        >
          <label>{{ property.label }}</label>
          
          <select 
            v-if="property.type === 'select'"
            v-model="properties[key]"
            @change="updateProperties"
          >
            <option 
              v-for="option in property.options" 
              :key="option.value"
              :value="option.value"
            >
              {{ option.label }}
            </option>
          </select>
          
          <input 
            v-else-if="property.type === 'number'"
            type="number" 
            v-model.number="properties[key]"
            :min="property.min"
            :max="property.max"
            :step="property.step"
            @change="updateProperties"
          >
          
          <input 
            v-else-if="property.type === 'boolean'"
            type="checkbox" 
            v-model="properties[key]"
            @change="updateProperties"
          >
          
          <input 
            v-else
            type="text" 
            v-model="properties[key]"
            @change="updateProperties"
          >
        </div>
        
        <div class="connection-controls">
          <button @click="startConnection('regular')" class="btn-primary" :disabled="store.connectionMode.active">
            Создать соединение
          </button>
          
          <div v-if="store.connectionMode.active" class="connection-hint">
            <p>Выберите целевой узел на схеме</p>
            <button @click="store.cancelConnection" class="btn-secondary">
              Отменить соединение
            </button>
          </div>
        </div>
      </div>
      
      <div v-else class="connection-properties">
        <h4>Соединение</h4>
        
        <div class="property-group">
          <label>Источник</label>
          <span class="readonly-value">{{ getNodeName(store.selectedElement.sourceId) }}</span>
        </div>
        
        <div class="property-group">
          <label>Цель</label>
          <span class="readonly-value">{{ getNodeName(store.selectedElement.targetId) }}</span>
        </div>
        
        <div 
          v-for="(property, key) in connectionPropertiesSchema" 
          :key="key"
          class="property-group"
        >
          <label>{{ property.label }}</label>
          <input 
            type="number" 
            v-model.number="connectionProperties[key]"
            @change="updateConnectionProperties"
          >
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { usePipelineStore } from '@/stores/pipeline-store'
import type { PipelineNode, PipelineConnection, NodeProperties } from '@/types/pipeline'
import { configService } from '@/services/config-service'

const store = usePipelineStore()

const position = ref({ x: 0, y: 0 })
const pipeEnd = ref({ x: 0, y: 0 })
const properties = ref<NodeProperties>({})
const connectionProperties = ref<Record<string, number>>({})

const elementDefinition = computed(() => {
  if (!store.selectedElement || !isNode(store.selectedElement)) return null
  return configService.getElementDefinition(store.selectedElement.type)
})

const connectionPropertiesSchema = {
  length: { label: 'Длина (м)', type: 'number', min: 0 },
  diameter: { label: 'Диаметр (мм)', type: 'number', min: 0 },
  roughness: { label: 'Шероховатость', type: 'number', min: 0, step: 0.001 }
}

const isNode = (element: any): element is PipelineNode => {
  return 'type' in element && 'position' in element
}

const getElementName = (element: PipelineNode | PipelineConnection): string => {
  if (isNode(element)) {
    return element.properties.name || element.type
  }
  return 'Соединение'
}

const getNodeName = (nodeId: string): string => {
  const node = store.currentProject?.nodes.find(n => n.id === nodeId)
  return node ? (node.properties.name || node.type) : nodeId
}

const deleteSelectedElement = () => {
  if (store.selectedElement) {
    store.deleteElement(store.selectedElement.id)
  }
}

const startConnection = (connectionType: 'start' | 'end' | 'regular') => {
  if (store.selectedElement && isNode(store.selectedElement)) {
    store.startConnection(store.selectedElement.id, connectionType)
  }
}

const updateNodePosition = () => {
  if (store.selectedElement && isNode(store.selectedElement)) {
    store.updateNodePosition(store.selectedElement.id, position.value)
  }
}

const updateProperties = () => {
  if (store.selectedElement && isNode(store.selectedElement)) {
    store.updateNodeProperties(store.selectedElement.id, properties.value)
  }
}

const updateConnectionProperties = () => {
  // Implementation for updating connection properties would go here
  console.log('Connection properties updated:', connectionProperties.value)
}

const calculatePipeLength = (): number => {
  if (!store.selectedElement || !isNode(store.selectedElement) || store.selectedElement.type !== 'pipe') {
    return 0
  }
  const dx = pipeEnd.value.x - position.value.x
  const dy = pipeEnd.value.y - position.value.y
  return Math.round(Math.sqrt(dx * dx + dy * dy))
}

const updatePipeEnd = () => {
  if (store.selectedElement && isNode(store.selectedElement) && store.selectedElement.type === 'pipe') {
    const updatedProperties = {
      ...store.selectedElement.properties,
      endX: pipeEnd.value.x,
      endY: pipeEnd.value.y
    }
    store.updateNodeProperties(store.selectedElement.id, updatedProperties)
  }
}

// Watch for selected element changes
watch(() => store.selectedElement, (newElement) => {
  if (newElement && isNode(newElement)) {
    position.value = { ...newElement.position }
    properties.value = { ...newElement.properties }
    
    if (newElement.type === 'pipe') {
      pipeEnd.value = {
        x: newElement.properties.endX || newElement.position.x + 100,
        y: newElement.properties.endY || newElement.position.y
      }
    }
  } else if (newElement && !isNode(newElement)) {
    connectionProperties.value = { ...newElement.properties }
  }
}, { immediate: true })
</script>

<style scoped>
.property-panel {
  width: 300px;
  background: white;
  border-left: 1px solid #bdc3c7;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.panel-header {
  padding: 1rem;
  border-bottom: 1px solid #ecf0f1;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.panel-header h3 {
  margin: 0;
  color: #2c3e50;
  font-size: 1.2rem;
}

.delete-btn {
  background: #e74c3c;
  color: white;
  border: none;
  padding: 0.25rem 0.75rem;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.8rem;
}

.delete-btn:hover {
  background: #c0392b;
}

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 1rem;
}

.no-selection {
  text-align: center;
  color: #7f8c8d;
  padding: 2rem 1rem;
}

.no-selection p {
  margin: 0;
}

.node-properties h4,
.connection-properties h4,
.pipe-properties h4 {
  margin: 0 0 1rem 0;
  color: #2c3e50;
  font-size: 1.1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid #ecf0f1;
}

.property-group {
  margin-bottom: 1rem;
}

.property-group label {
  display: block;
  margin-bottom: 0.25rem;
  color: #34495e;
  font-size: 0.9rem;
  font-weight: 500;
}

.property-group input,
.property-group select {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #bdc3c7;
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
}

.property-group input:focus,
.property-group select:focus {
  outline: none;
  border-color: #3498db;
}

.readonly-value {
  display: block;
  padding: 0.5rem;
  background: #f8f9fa;
  border: 1px solid #e9ecef;
  border-radius: 4px;
  color: #495057;
  font-size: 0.9rem;
}

.connection-controls {
  margin-top: 1.5rem;
  padding-top: 1rem;
  border-top: 1px solid #ecf0f1;
}

.connection-buttons {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.btn-primary {
  background: #3498db;
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
}

.btn-primary:hover:not(:disabled) {
  background: #2980b9;
}

.btn-primary:disabled {
  background: #bdc3c7;
  cursor: not-allowed;
}

.btn-secondary {
  background: #95a5a6;
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
}

.btn-secondary:hover {
  background: #7f8c8d;
}

.connection-hint {
  margin-top: 1rem;
  padding: 0.75rem;
  background: #fff3cd;
  border: 1px solid #ffeaa7;
  border-radius: 4px;
  color: #856404;
}

.connection-hint p {
  margin: 0 0 0.5rem 0;
  font-size: 0.9rem;
}
</style>
