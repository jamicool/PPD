import type { PipelineProject, SimulationResult, ProjectSummary } from '@/types/pipeline'

export interface ValidationResult {
  isValid: boolean
  errors: string[]
  warnings: string[]
}

class ApiService {
  private baseUrl = import.meta.env.VITE_API_URL || '/api'

  async getProjects(): Promise<ProjectSummary[]> {
    try {
      const response = await fetch(`${this.baseUrl}/pipeline/projects`)
      if (!response.ok) {
        throw new Error(`Failed to fetch projects: ${response.status}`)
      }
      const projects = await response.json()
      return projects.map((p: any) => ({
        ...p,
        createdAt: new Date(p.createdAt),
        updatedAt: new Date(p.updatedAt)
      }))
    } catch (error) {
      console.error('API Error fetching projects:', error)
      return []
    }
  }

  async getProject(projectId: string): Promise<PipelineProject> {
    const response = await fetch(`${this.baseUrl}/pipeline/projects/${projectId}`)
    if (!response.ok) {
      throw new Error(`Failed to fetch project: ${response.status}`)
    }
    const project = await response.json()
    return {
      ...project,
      createdAt: new Date(project.createdAt),
      updatedAt: new Date(project.updatedAt)
    }
  }

  async saveProject(project: PipelineProject): Promise<PipelineProject> {
    const url = project.id 
      ? `${this.baseUrl}/pipeline/projects/${project.id}`
      : `${this.baseUrl}/pipeline/projects`
    
    const method = project.id ? 'PUT' : 'POST'
    
    try {
      console.log(`Saving project with ${method} to ${url}`, project)
      
      const response = await fetch(url, {
        method,
        headers: { 
          'Content-Type': 'application/json',
          'Accept': 'application/json'
        },
        body: JSON.stringify(project)
      })
      
      if (!response.ok) {
        const errorText = await response.text()
        console.error(`Save failed with status ${response.status}:`, errorText)
        throw new Error(`Failed to save project: ${response.status} - ${errorText}`)
      }
      
      const savedProject = await response.json()
      console.log('Project saved successfully:', savedProject)
      return {
        ...savedProject,
        createdAt: new Date(savedProject.createdAt),
        updatedAt: new Date(savedProject.updatedAt)
      }
    } catch (error) {
      console.error('API Error saving project:', error)
      throw error
    }
  }

  async validateProject(project: PipelineProject): Promise<ValidationResult> {
    const response = await fetch(`${this.baseUrl}/pipeline/validate`, {
      method: 'POST',
      headers: { 
        'Content-Type': 'application/json',
        'Accept': 'application/json'
      },
      body: JSON.stringify(project)
    })
    
    if (!response.ok) {
      throw new Error(`Validation failed: ${response.status}`)
    }
    return await response.json()
  }

  async simulate(projectId: string, parameters: any = {}): Promise<SimulationResult> {
    const response = await fetch(`${this.baseUrl}/pipeline/simulate`, {
      method: 'POST',
      headers: { 
        'Content-Type': 'application/json',
        'Accept': 'application/json'
      },
      body: JSON.stringify({ projectId, parameters })
    })
    
    if (!response.ok) {
      throw new Error(`Simulation failed: ${response.status}`)
    }
    return await response.json()
  }

  async exportProject(projectId: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/pipeline/projects/${projectId}/export`, {
      method: 'POST'
    })
    
    if (!response.ok) {
      throw new Error(`Export failed: ${response.status}`)
    }
    
    const blob = await response.blob()
    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `project_${projectId}_${new Date().toISOString().split('T')[0]}.json`
    document.body.appendChild(a)
    a.click()
    window.URL.revokeObjectURL(url)
    document.body.removeChild(a)
  }

  async importProject(file: File): Promise<PipelineProject> {
    const formData = new FormData()
    formData.append('file', file)
    
    const response = await fetch(`${this.baseUrl}/pipeline/projects/import`, {
      method: 'POST',
      body: formData
    })
    
    if (!response.ok) {
      const errorText = await response.text()
      throw new Error(`Import failed: ${response.status} - ${errorText}`)
    }
    const project = await response.json()
    return {
      ...project,
      createdAt: new Date(project.createdAt),
      updatedAt: new Date(project.updatedAt)
    }
  }
}

export const apiService = new ApiService()
