import { defineStore } from 'pinia'
import { ref, reactive } from 'vue'
import type { PipelineNode, PipelineConnection, PipelineProject, SimulationResult } from '@/types/pipeline'
import { apiService } from '@/services/api-service'
import { signalRService } from '@/services/signalr-service'

export interface NodeProperties {
  name?: string
  flowRate?: number
  pressure?: number
  diameter?: number
  status?: 'open' | 'closed' | 'active' | 'standby' | 'maintenance' | 'partial'
  endX?: number
  endY?: number
  length?: number
  roughness?: number
  material?: string
  [key: string]: any
}

export interface ProjectSummary {
  id: string
  name: string
  type: 'gas' | 'oil'
  createdAt: Date
  updatedAt: Date
}

// Функция для генерации уникального ID
const generateId = (): string => {
  return `element_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`
}

export const usePipelineStore = defineStore('pipeline', () => {
  const currentProject = ref<PipelineProject | null>(null)
  const selectedElement = ref<PipelineNode | PipelineConnection | null>(null)
  const isSimulationRunning = ref(false)
  const simulationProgress = ref(0)
  const availableProjects = ref<ProjectSummary[]>([])
  
  const connectionMode = ref<{
    active: boolean; 
    sourceNodeId: string | null;
    connectionType: 'start' | 'end' | 'regular' | null;
    tempEnd?: { x: number; y: number };
  }>({
    active: false,
    sourceNodeId: null,
    connectionType: null
  })

  const initializeSignalR = () => {
    signalRService.on('SimulationProgress', (progress: number) => {
      updateSimulationProgress(progress)
    })

    signalRService.on('SimulationCompleted', (result: SimulationResult) => {
      handleSimulationResult(result)
    })

    signalRService.on('SimulationError', (error: string) => {
      console.error('Simulation error:', error)
      isSimulationRunning.value = false
    })

    signalRService.connect()
  }

  const addNode = (node: Omit<PipelineNode, 'id'>) => {
    if (!currentProject.value) return
    
    const newNode: PipelineNode = {
      ...node,
      id: generateId()
    }
    
    currentProject.value.nodes.push(newNode)
    saveProject()
  }
  
  const updateNodePosition = (nodeId: string, position: { x: number; y: number }) => {
    const node = currentProject.value?.nodes.find(n => n.id === nodeId)
    if (node) {
      node.position = position
      saveProject()
    }
  }
  
  const updateNodeProperties = (nodeId: string, properties: Partial<NodeProperties>) => {
    const node = currentProject.value?.nodes.find(n => n.id === nodeId)
    if (node) {
      node.properties = { ...node.properties, ...properties }
      saveProject()
    }
  }
  
  const addConnection = (sourceId: string, targetId: string) => {
    if (!currentProject.value) return
    
    const existingConnection = currentProject.value.connections.find(
      conn => conn.sourceId === sourceId && conn.targetId === targetId
    )
    
    if (existingConnection) return
    
    const connection: PipelineConnection = {
      id: generateId(),
      sourceId,
      targetId,
      properties: {}
    }
    
    currentProject.value.connections.push(connection)
    saveProject()
  }
  
  const deleteElement = (elementId: string) => {
    if (!currentProject.value) return
    
    currentProject.value.nodes = currentProject.value.nodes.filter(n => n.id !== elementId)
    currentProject.value.connections = currentProject.value.connections.filter(
      c => c.sourceId !== elementId && c.targetId !== elementId
    )
    
    if (selectedElement.value?.id === elementId) {
      selectedElement.value = null
    }
    
    saveProject()
  }
  
  const startConnection = (nodeId: string, connectionType: 'start' | 'end' | 'regular' = 'regular') => {
    connectionMode.value = {
      active: true,
      sourceNodeId: nodeId,
      connectionType
    }
  }
  
  const finishConnection = (targetNodeId: string) => {
    if (connectionMode.value.active && connectionMode.value.sourceNodeId) {
      addConnection(connectionMode.value.sourceNodeId, targetNodeId)
      cancelConnection()
    }
  }
  
  const cancelConnection = () => {
    connectionMode.value = {
      active: false,
      sourceNodeId: null,
      connectionType: null
    }
  }
  
  const loadProject = async (projectId: string) => {
    try {
      const project = await apiService.getProject(projectId)
      currentProject.value = project
      return project
    } catch (error) {
      console.error('Failed to load project:', error)
      throw error
    }
  }

  const loadProjects = async () => {
    try {
      availableProjects.value = await apiService.getProjects()
    } catch (error) {
      console.error('Failed to load projects list:', error)
      availableProjects.value = []
    }
  }
  
  const createProject = async (projectData: Partial<PipelineProject>) => {
    try {
      const newProject: PipelineProject = {
        id: generateId(),
        name: projectData.name || 'New Project',
        type: projectData.type || 'gas',
        nodes: projectData.nodes || [],
        connections: projectData.connections || [],
        createdAt: new Date(),
        updatedAt: new Date()
      }
      
      const savedProject = await apiService.saveProject(newProject)
      currentProject.value = savedProject
      
      await loadProjects()
      
      return savedProject
    } catch (error) {
      console.error('Failed to create project:', error)
      throw error
    }
  }

  const saveProject = async () => {
    if (!currentProject.value) return

    try {
      const savedProject = await apiService.saveProject(currentProject.value)
      currentProject.value = savedProject
      await loadProjects()
      return savedProject
    } catch (error) {
      console.error('Failed to save project:', error)
      throw error
    }
  }
  
  const validateProject = async () => {
    if (!currentProject.value) return
    
    try {
      return await apiService.validateProject(currentProject.value)
    } catch (error) {
      console.error('Validation failed:', error)
      return { isValid: false, errors: ['Validation error'], warnings: [] }
    }
  }
  
  const startSimulation = async () => {
    if (!currentProject.value) return
    
    try {
      isSimulationRunning.value = true
      simulationProgress.value = 0
      
      await signalRService.startSimulation(currentProject.value.id, currentProject.value)
    } catch (error) {
      console.error('Failed to start simulation:', error)
      isSimulationRunning.value = false
    }
  }
  
  const stopSimulation = async () => {
    if (!currentProject.value) return
    
    try {
      await signalRService.stopSimulation(currentProject.value.id)
      isSimulationRunning.value = false
    } catch (error) {
      console.error('Failed to stop simulation:', error)
    }
  }
  
  const updateSimulationProgress = (progress: number) => {
    simulationProgress.value = progress
  }
  
  const handleSimulationResult = (result: SimulationResult) => {
    isSimulationRunning.value = false
    simulationProgress.value = 100
    
    if (result.isSuccessful) {
      console.log('Simulation completed successfully:', result)
    } else {
      console.error('Simulation failed:', result.errorMessage)
    }
  }
  
  const selectElement = (element: PipelineNode | PipelineConnection | null) => {
    selectedElement.value = element
  }
  
  const exportProject = async () => {
    if (!currentProject.value) return
    
    try {
      await apiService.exportProject(currentProject.value.id)
    } catch (error) {
      console.error('Failed to export project:', error)
    }
  }

  const importProject = async (file: File) => {
    try {
      const project = await apiService.importProject(file)
      currentProject.value = project
      await loadProjects()
      console.log('Project imported successfully')
      return project
    } catch (error) {
      console.error('Failed to import project:', error)
      throw error
    }
  }

  initializeSignalR()
  
  return {
    currentProject,
    selectedElement,
    isSimulationRunning,
    simulationProgress,
    availableProjects,
    connectionMode,
    addNode,
    updateNodePosition,
    updateNodeProperties,
    addConnection,
    deleteElement,
    startConnection,
    finishConnection,
    cancelConnection,
    loadProject,
    loadProjects,
    createProject,
    saveProject,
    validateProject,
    startSimulation,
    stopSimulation,
    selectElement,
    exportProject,
    importProject
  }
})
