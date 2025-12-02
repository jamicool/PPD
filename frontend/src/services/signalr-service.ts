import { HubConnectionBuilder, HubConnection, HubConnectionState } from '@microsoft/signalr'
import type { SimulationResult } from '@/types/pipeline'

class SignalRService {
  private connection: HubConnection | null = null
  private eventHandlers: Map<string, Function> = new Map()

  async connect(): Promise<void> {
    this.connection = new HubConnectionBuilder()
      .withUrl('/simulationHub')
      .withAutomaticReconnect()
      .build()

    this.setupEventHandlers()
    
    try {
      await this.connection.start()
      console.log('SignalR Connected')
    } catch (err) {
      console.error('SignalR Connection Error: ', err)
    }
  }

  private setupEventHandlers(): void {
    if (!this.connection) return

    this.connection.on('SimulationQueued', (data: { taskId: string }) => {
      console.log(`Simulation queued with task ID: ${data.taskId}`)
      this.triggerEvent('SimulationQueued', data)
    })

    this.connection.on('SimulationProgress', (progress: number) => {
      this.triggerEvent('SimulationProgress', progress)
    })

    this.connection.on('SimulationCompleted', (result: SimulationResult) => {
      this.triggerEvent('SimulationCompleted', result)
    })

    this.connection.on('SimulationError', (error: string) => {
      console.error('Simulation error:', error)
      this.triggerEvent('SimulationError', error)
    })

    this.connection.on('SimulationStopped', (projectId: string) => {
      console.log(`Simulation stopped for project: ${projectId}`)
      this.triggerEvent('SimulationStopped', projectId)
    })
  }

  on(event: string, handler: Function): void {
    this.eventHandlers.set(event, handler)
  }

  private triggerEvent(event: string, data: any): void {
    const handler = this.eventHandlers.get(event)
    if (handler) {
      handler(data)
    }
  }

  async startSimulation(projectId: string, projectData: any): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('StartSimulation', projectId, projectData)
    }
  }

  async stopSimulation(projectId: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('StopSimulation', projectId)
    }
  }

  disconnect(): void {
    this.connection?.stop()
  }
}

export const signalRService = new SignalRService()
