<template>
  <div class="pipeline-editor">
    <div class="editor-container">
      <div class="canvas-container">
        <svg 
          class="pipeline-svg" 
          :width="svgWidth" 
          :height="svgHeight" 
          @click="onSvgClick"
          @mousemove="onSvgMouseMove"
          @mouseup="onSvgMouseUp"
        >
          <!-- Временная линия соединения -->
          <path
            v-if="tempConnection"
            :d="tempConnection"
            stroke="#e74c3c"
            stroke-width="2"
            fill="none"
            stroke-dasharray="5,5"
          />
          
          <!-- Постоянные соединения -->
          <path
            v-for="connection in store.currentProject?.connections || []"
            :key="connection.id"
            :d="getConnectionPath(connection)"
            stroke="#3498db"
            stroke-width="3"
            fill="none"
            marker-end="url(#arrowhead)"
            @click.stop="selectElement(connection)"
            :class="{ selected: store.selectedElement?.id === connection.id }"
          />
          
          <!-- Узлы (кроме труб) -->
          <g v-for="node in nonPipeNodes" :key="node.id">
            <circle
              :cx="node.position.x"
              :cy="node.position.y"
              r="20"
              :fill="getNodeColor(node)"
              stroke="#2c3e50"
              stroke-width="2"
              @mousedown="startNodeDrag(node, $event)"
              @click.stop="onNodeClick(node)"
              :class="{ 
                selected: store.selectedElement?.id === node.id,
                'connection-source': store.connectionMode.active && store.connectionMode.sourceNodeId === node.id
              }"
            />
            <!-- Метки узлов -->
            <text
              :x="node.position.x"
              :y="node.position.y - 30"
              text-anchor="middle"
              fill="#2c3e50"
              font-size="12"
              font-weight="bold"
            >
              {{ node.properties.name || node.type }}
            </text>
          </g>
          
          <!-- Трубы как линии с точками для растягивания -->
          <g v-for="pipe in pipeNodes" :key="pipe.id">
            <!-- Основная линия трубы -->
            <line
              :x1="pipe.position.x"
              :y1="pipe.position.y"
              :x2="pipe.properties.endX || pipe.position.x + 100"
              :y2="pipe.properties.endY || pipe.position.y"
              stroke="#3498db"
              :stroke-width="getPipeStrokeWidth(pipe)"
              @click.stop="selectElement(pipe)"
              :class="{ selected: store.selectedElement?.id === pipe.id }"
            />
            
            <!-- Начальная точка трубы -->
            <circle
              :cx="pipe.position.x"
              :cy="pipe.position.y"
              r="8"
              :fill="getConnectionPointColor(pipe.id, 'start')"
              stroke="#2c3e50"
              stroke-width="2"
              @mousedown="startPipeDrag(pipe, 'start', $event)"
              @click.stop="onPipeConnectionPointClick(pipe, 'start')"
              class="pipe-handle connection-point"
              :class="{ 
                'connection-source': store.connectionMode.active && 
                  store.connectionMode.sourceNodeId === pipe.id && 
                  store.connectionMode.connectionType === 'start'
              }"
            />
            
            <!-- Конечная точка трубы -->
            <circle
              :cx="pipe.properties.endX || pipe.position.x + 100"
              :cy="pipe.properties.endY || pipe.position.y"
              r="8"
              :fill="getConnectionPointColor(pipe.id, 'end')"
              stroke="#2c3e50"
              stroke-width="2"
              @mousedown="startPipeDrag(pipe, 'end', $event)"
              @click.stop="onPipeConnectionPointClick(pipe, 'end')"
              class="pipe-handle connection-point"
              :class="{ 
                'connection-source': store.connectionMode.active && 
                  store.connectionMode.sourceNodeId === pipe.id && 
                  store.connectionMode.connectionType === 'end'
              }"
            />
            
            <!-- Метка трубы -->
            <text
              :x="(pipe.position.x + (pipe.properties.endX || pipe.position.x + 100)) / 2"
              :y="(pipe.position.y + (pipe.properties.endY || pipe.position.y)) / 2 - 15"
              text-anchor="middle"
              fill="#2c3e50"
              font-size="11"
              font-weight="bold"
            >
              {{ pipe.properties.name || 'Труба' }}
            </text>
            
            <!-- Размеры трубы -->
            <text
              :x="(pipe.position.x + (pipe.properties.endX || pipe.position.x + 100)) / 2"
              :y="(pipe.position.y + (pipe.properties.endY || pipe.position.y)) / 2"
              text-anchor="middle"
              fill="#7f8c8d"
              font-size="10"
            >
              {{ calculatePipeLength(pipe) }}м
            </text>
          </g>
          
          <!-- Маркер для стрелок -->
          <defs>
            <marker
              id="arrowhead"
              markerWidth="10"
              markerHeight="7"
              refX="9"
              refY="3.5"
              orient="auto"
            >
              <polygon points="0 0, 10 3.5, 0 7" fill="#3498db"/>
            </marker>
          </defs>
        </svg>
      </div>
      
      <div class="simulation-controls" v-if="store.currentProject">
        <button 
          @click="startSimulation" 
          :disabled="store.isSimulationRunning"
          class="btn-primary"
        >
          {{ store.isSimulationRunning ? 'Запуск...' : 'Запустить симуляцию' }}
        </button>
        
        <button 
          @click="store.stopSimulation" 
          :disabled="!store.isSimulationRunning"
          class="btn-secondary"
        >
          Остановить
        </button>
        
        <div class="connection-mode-indicator" v-if="store.connectionMode.active">
          <span>Режим соединения: выберите целевой узел</span>
          <button @click="store.cancelConnection" class="btn-secondary">Отмена</button>
        </div>
        
        <div class="progress-container" v-if="store.isSimulationRunning">
          <div class="progress-bar">
            <div 
              class="progress-fill" 
              :style="{ width: store.simulationProgress + '%' }"
            ></div>
          </div>
          <span class="progress-text">{{ store.simulationProgress }}%</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { usePipelineStore } from '@/stores/pipeline-store'
import type { PipelineNode, PipelineConnection } from '@/types/pipeline'

const store = usePipelineStore()

const svgWidth = 1200
const svgHeight = 800

const tempConnection = ref<string | null>(null)
const dragState = ref<{
  isDragging: boolean
  node: PipelineNode | null
  startX: number
  startY: number
  type: 'node' | 'pipe-start' | 'pipe-end'
}>({
  isDragging: false,
  node: null,
  startX: 0,
  startY: 0,
  type: 'node'
})

const mousePosition = ref<{x: number, y: number}>({x: 0, y: 0})

// Разделяем узлы на трубы и остальные
const pipeNodes = computed(() => {
  return store.currentProject?.nodes.filter(node => node.type === 'pipe') || []
})

const nonPipeNodes = computed(() => {
  return store.currentProject?.nodes.filter(node => node.type !== 'pipe') || []
})

const getNodeColor = (node: PipelineNode): string => {
  const colors: Record<string, string> = {
    pump: '#e74c3c',
    valve: '#f39c12',
    tank: '#27ae60',
    compressor: '#9b59b6',
    regulator: '#1abc9c',
    filter: '#95a5a6',
    heater: '#e67e22',
    separator: '#3498db',
    meter: '#2ecc71'
  }
  return colors[node.type] || '#95a5a6'
}

const getConnectionPointColor = (pipeId: string, pointType: 'start' | 'end'): string => {
  if (store.connectionMode.active && store.connectionMode.sourceNodeId === pipeId) {
    if (store.connectionMode.connectionType === pointType) {
      return '#e74c3c'
    }
  }
  
  // Проверяем, есть ли уже соединения для этой точки
  const connections = store.currentProject?.connections || []
  const hasStartConnection = connections.some(conn => 
    conn.sourceId === pipeId || conn.targetId === pipeId
  )
  
  return pointType === 'start' ? '#27ae60' : '#3498db'
}

const getPipeStrokeWidth = (pipe: PipelineNode): number => {
  // Толщина линии зависит от диаметра трубы
  const diameter = pipe.properties.diameter || 100
  return Math.max(2, Math.min(8, diameter / 20))
}

const calculatePipeLength = (pipe: PipelineNode): number => {
  const startX = pipe.position.x
  const startY = pipe.position.y
  const endX = pipe.properties.endX || startX + 100
  const endY = pipe.properties.endY || startY
  
  const dx = endX - startX
  const dy = endY - startY
  return Math.round(Math.sqrt(dx * dx + dy * dy))
}

const getConnectionPath = (connection: PipelineConnection): string => {
  const sourceNode = store.currentProject?.nodes.find(n => n.id === connection.sourceId)
  const targetNode = store.currentProject?.nodes.find(n => n.id === connection.targetId)
  
  if (!sourceNode || !targetNode) return ''
  
  return createCurvedPath(sourceNode.position, targetNode.position)
}

const createCurvedPath = (start: {x: number, y: number}, end: {x: number, y: number}): string => {
  const dx = end.x - start.x
  const dy = end.y - start.y
  
  // Создаем изогнутый путь
  const controlX1 = start.x + dx * 0.5
  const controlY1 = start.y
  const controlX2 = start.x + dx * 0.5
  const controlY2 = end.y
  
  return `M ${start.x} ${start.y} C ${controlX1} ${controlY1}, ${controlX2} ${controlY2}, ${end.x} ${end.y}`
}

const selectElement = (element: PipelineNode | PipelineConnection) => {
  store.selectElement(element)
}

const onNodeClick = (node: PipelineNode) => {
  if (store.connectionMode.active) {
    // Если в режиме соединения, завершаем соединение
    store.finishConnection(node.id)
  } else {
    // Иначе просто выбираем узел
    store.selectElement(node)
  }
}

const onPipeConnectionPointClick = (pipe: PipelineNode, pointType: 'start' | 'end') => {
  if (store.connectionMode.active) {
    // Если в режиме соединения, завершаем соединение
    store.finishConnection(pipe.id)
  } else {
    // Иначе начинаем новое соединение с выбранной точки трубы
    store.startConnection(pipe.id, pointType)
  }
}

const startNodeDrag = (node: PipelineNode, event: MouseEvent) => {
  event.preventDefault()
  dragState.value = {
    isDragging: true,
    node,
    startX: event.clientX - node.position.x,
    startY: event.clientY - node.position.y,
    type: 'node'
  }
}

const startPipeDrag = (pipe: PipelineNode, handleType: 'start' | 'end', event: MouseEvent) => {
  event.preventDefault()
  event.stopPropagation()
  
  const position = handleType === 'start' 
    ? pipe.position 
    : { x: pipe.properties.endX || pipe.position.x + 100, y: pipe.properties.endY || pipe.position.y }
  
  dragState.value = {
    isDragging: true,
    node: pipe,
    startX: event.clientX - position.x,
    startY: event.clientY - position.y,
    type: handleType === 'start' ? 'pipe-start' : 'pipe-end'
  }
}

const onSvgMouseMove = (event: MouseEvent) => {
  const svg = event.currentTarget as SVGSVGElement
  const point = svg.createSVGPoint()
  point.x = event.clientX
  point.y = event.clientY
  const svgPoint = point.matrixTransform(svg.getScreenCTM()?.inverse())
  
  mousePosition.value = { x: svgPoint.x, y: svgPoint.y }
  
  // Обновляем временное соединение если в режиме соединения
  if (store.connectionMode.active && store.connectionMode.sourceNodeId) {
    const sourceNode = store.currentProject?.nodes.find(n => n.id === store.connectionMode.sourceNodeId)
    if (sourceNode) {
      let sourcePoint = sourceNode.position
      
      // Если соединяемся с конкретной точкой трубы, используем соответствующую позицию
      if (store.connectionMode.connectionType === 'start' && sourceNode.type === 'pipe') {
        sourcePoint = sourceNode.position
      } else if (store.connectionMode.connectionType === 'end' && sourceNode.type === 'pipe') {
        sourcePoint = {
          x: sourceNode.properties.endX || sourceNode.position.x + 100,
          y: sourceNode.properties.endY || sourceNode.position.y
        }
      }
      
      tempConnection.value = createCurvedPath(sourcePoint, mousePosition.value)
    }
  }
  
  // Перетаскивание узла или трубы
  if (dragState.value.isDragging && dragState.value.node) {
    const newX = event.clientX - dragState.value.startX
    const newY = event.clientY - dragState.value.startY
    
    // Ограничиваем позицию в пределах SVG
    const boundedX = Math.max(20, Math.min(svgWidth - 20, newX))
    const boundedY = Math.max(20, Math.min(svgHeight - 20, newY))
    
    if (dragState.value.type === 'node') {
      store.updateNodePosition(dragState.value.node.id, { x: boundedX, y: boundedY })
    } else if (dragState.value.type === 'pipe-start') {
      // Обновляем начальную позицию трубы
      const updatedProperties = {
        ...dragState.value.node.properties,
        endX: dragState.value.node.properties.endX || dragState.value.node.position.x + 100,
        endY: dragState.value.node.properties.endY || dragState.value.node.position.y
      }
      store.updateNodeProperties(dragState.value.node.id, updatedProperties)
      store.updateNodePosition(dragState.value.node.id, { x: boundedX, y: boundedY })
    } else if (dragState.value.type === 'pipe-end') {
      // Обновляем конечную позицию трубы
      const updatedProperties = {
        ...dragState.value.node.properties,
        endX: boundedX,
        endY: boundedY
      }
      store.updateNodeProperties(dragState.value.node.id, updatedProperties)
    }
  }
}

const onSvgMouseUp = () => {
  dragState.value.isDragging = false
  dragState.value.node = null
}

const onSvgClick = (event: MouseEvent) => {
  const svg = event.currentTarget as SVGSVGElement
  
  // Проверяем, кликнули ли на пустое место
  if (event.target === svg) {
    if (store.connectionMode.active) {
      store.cancelConnection()
    }
    store.selectElement(null)
  }
}

const startSimulation = async () => {
  const validation = await store.validateProject()
  if (validation.isValid) {
    await store.startSimulation()
  } else {
    alert('Проект содержит ошибки: ' + validation.errors.join(', '))
  }
}

// Следим за режимом соединения и сбрасываем временное соединение при отмене
import { watch } from 'vue'
watch(() => store.connectionMode.active, (newVal) => {
  if (!newVal) {
    tempConnection.value = null
  }
})

onMounted(() => {
  document.addEventListener('mouseup', onSvgMouseUp)
})

onUnmounted(() => {
  document.removeEventListener('mouseup', onSvgMouseUp)
})
</script>

<style scoped>
.pipeline-editor {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: #ecf0f1;
}

.editor-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  position: relative;
}

.canvas-container {
  flex: 1;
  position: relative;
  overflow: hidden;
}

.pipeline-svg {
  background: white;
  cursor: crosshair;
}

.pipeline-svg circle:not(.pipe-handle) {
  cursor: pointer;
  transition: all 0.2s ease;
}

.pipeline-svg circle:not(.pipe-handle):hover {
  stroke-width: 3;
  r: 22;
}

.pipeline-svg circle.connection-point {
  cursor: crosshair;
  opacity: 0.7;
  transition: all 0.2s ease;
}

.pipeline-svg circle.connection-point:hover {
  opacity: 1;
  r: 10;
}

.pipeline-svg circle.pipe-handle {
  cursor: move;
  opacity: 0;
  transition: opacity 0.2s ease;
}

.pipeline-svg line:hover + circle.pipe-handle,
.pipeline-svg circle.pipe-handle:hover {
  opacity: 1;
}

.pipeline-svg line {
  cursor: pointer;
  transition: all 0.2s ease;
}

.pipeline-svg line:hover {
  stroke-width: 4;
}

.pipeline-svg circle.selected {
  stroke: #e74c3c;
  stroke-width: 4;
}

.pipeline-svg circle.connection-source {
  stroke: #e74c3c;
  stroke-width: 4;
  animation: pulse 1s infinite;
}

.pipeline-svg line.selected {
  stroke: #e74c3c;
  stroke-width: 4;
}

@keyframes pulse {
  0% { stroke-width: 4; }
  50% { stroke-width: 6; }
  100% { stroke-width: 4; }
}

.pipeline-svg path {
  cursor: pointer;
  transition: all 0.2s ease;
}

.pipeline-svg path:hover {
  stroke-width: 4;
}

.pipeline-svg path.selected {
  stroke: #e74c3c;
}

.simulation-controls {
  background: white;
  padding: 1rem;
  border-top: 1px solid #bdc3c7;
  display: flex;
  align-items: center;
  gap: 1rem;
}

.connection-mode-indicator {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0.5rem 1rem;
  background: #fff3cd;
  border: 1px solid #ffeaa7;
  border-radius: 4px;
  color: #856404;
}

.progress-container {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex: 1;
}

.progress-bar {
  flex: 1;
  height: 8px;
  background: #ecf0f1;
  border-radius: 4px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: #27ae60;
  transition: width 0.3s ease;
}

.progress-text {
  font-size: 0.9rem;
  color: #7f8c8d;
  min-width: 40px;
}

.btn-primary:disabled, .btn-secondary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
