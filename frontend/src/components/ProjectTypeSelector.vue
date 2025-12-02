<template>
  <div class="project-type-selector" v-if="!store.currentProject">
    <div class="selector-container">
      <h1>Pipeline Designer</h1>
      <p class="subtitle">Выберите тип проекта для начала работы</p>
      
      <div class="project-types">
        <div 
          v-for="projectType in projectTypes" 
          :key="projectType.id"
          class="project-type-card"
          @click="createProject(projectType.id)"
        >
          <div class="card-icon">{{ getIcon(projectType.id) }}</div>
          <h3>{{ projectType.name }}</h3>
          <p>{{ projectType.description }}</p>
        </div>
      </div>
      
      <div class="recent-projects" v-if="store.availableProjects.length > 0">
        <h3>Недавние проекты</h3>
        <div class="recent-list">
          <div 
            v-for="project in store.availableProjects" 
            :key="project.id"
            class="recent-project"
            @click="loadProject(project.id)"
          >
            <span class="project-name">{{ project.name }}</span>
            <span class="project-type">{{ getProjectTypeName(project.type) }}</span>
            <span class="project-date">{{ formatDate(project.updatedAt || project.createdAt) }}</span>
            <button 
              class="delete-project-btn"
              @click.stop="deleteProject(project.id, project.name)"
              title="Удалить проект"
            >
              ×
            </button>
          </div>
        </div>
      </div>

      <div class="no-projects" v-else-if="!isLoading">
        <p>Нет сохраненных проектов. Создайте новый проект.</p>
      </div>

      <div class="loading" v-if="isLoading">
        <p>Загрузка проектов...</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { usePipelineStore } from '@/stores/pipeline-store'
import { configService } from '@/services/config-service'
import { signalRService } from '@/services/signalr-service'
import { apiService } from '@/services/api-service'

const store = usePipelineStore()
const isLoading = ref(false)

interface ProjectType {
  id: string
  name: string
  description: string
}

const projectTypes = ref<ProjectType[]>([
  {
    id: 'gas',
    name: 'Газопровод',
    description: 'Проектирование систем транспортировки газа'
  },
  {
    id: 'oil',
    name: 'Нефтепровод',
    description: 'Проектирование систем транспортировки нефти'
  }
])

const getIcon = (type: string): string => {
  const icons: Record<string, string> = {
    gas: '⛽',
    oil: '🛢️'
  }
  return icons[type] || '📋'
}

const getProjectTypeName = (type: string): string => {
  const projectType = configService.getProjectType(type)
  return projectType?.name || type
}

const createProject = async (type: string) => {
  try {
    // Убедимся, что SignalR соединение установлено
    await signalRService.connect()
    
    const projectName = `Новый ${type === 'gas' ? 'газопровод' : 'нефтепровод'}`
    
    await store.createProject({
      name: projectName,
      type: type as 'gas' | 'oil',
      nodes: [],
      connections: []
    })
  } catch (error) {
    console.error('Failed to create project:', error)
    alert('Ошибка при создании проекта: ' + (error instanceof Error ? error.message : 'Неизвестная ошибка'))
  }
}

const loadProject = async (projectId: string) => {
  try {
    // Убедимся, что SignalR соединение установлено
    await signalRService.connect()
    
    isLoading.value = true
    await store.loadProject(projectId)
  } catch (error) {
    console.error('Failed to load project:', error)
    alert('Ошибка при загрузке проекта: ' + (error instanceof Error ? error.message : 'Неизвестная ошибка'))
  } finally {
    isLoading.value = false
  }
}

const deleteProject = async (projectId: string, projectName: string) => {
  if (!confirm(`Вы уверены, что хотите удалить проект "${projectName}"? Это действие нельзя отменить.`)) {
    return
  }

  try {
    const response = await fetch(`/api/pipeline/projects/${projectId}`, {
      method: 'DELETE'
    })

    if (!response.ok) {
      throw new Error(`Failed to delete project: ${response.status}`)
    }

    // Обновляем список проектов
    await store.loadProjects()
    
    console.log(`Project ${projectId} deleted successfully`)
  } catch (error) {
    console.error('Failed to delete project:', error)
    alert('Ошибка при удалении проекта: ' + (error instanceof Error ? error.message : 'Неизвестная ошибка'))
  }
}

const formatDate = (date: Date): string => {
  return new Date(date).toLocaleDateString('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  })
}

onMounted(async () => {
  try {
    isLoading.value = true
    await configService.loadConfig()
    await store.loadProjects()
    
    // Предварительно инициализируем SignalR соединение
    signalRService.connect().catch(error => {
      console.warn('SignalR connection failed:', error)
      // Не блокируем приложение если SignalR недоступен
    })
  } catch (error) {
    console.error('Failed to initialize:', error)
  } finally {
    isLoading.value = false
  }
})
</script>

<style scoped>
.project-type-selector {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 10000;
}

.selector-container {
  background: white;
  padding: 3rem;
  border-radius: 12px;
  box-shadow: 0 20px 40px rgba(0,0,0,0.1);
  max-width: 800px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
}

h1 {
  text-align: center;
  color: #2c3e50;
  margin-bottom: 0.5rem;
  font-size: 2.5rem;
}

.subtitle {
  text-align: center;
  color: #7f8c8d;
  margin-bottom: 2rem;
  font-size: 1.1rem;
}

.project-types {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1.5rem;
  margin-bottom: 3rem;
}

.project-type-card {
  background: #f8f9fa;
  border: 2px solid #e9ecef;
  border-radius: 8px;
  padding: 2rem;
  text-align: center;
  cursor: pointer;
  transition: all 0.3s ease;
}

.project-type-card:hover {
  border-color: #3498db;
  transform: translateY(-4px);
  box-shadow: 0 10px 20px rgba(0,0,0,0.1);
}

.card-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.project-type-card h3 {
  color: #2c3e50;
  margin-bottom: 0.5rem;
}

.project-type-card p {
  color: #7f8c8d;
  font-size: 0.9rem;
  line-height: 1.4;
}

.recent-projects h3 {
  color: #2c3e50;
  margin-bottom: 1rem;
  font-size: 1.2rem;
}

.recent-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.recent-project {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem;
  background: #f8f9fa;
  border-radius: 6px;
  cursor: pointer;
  transition: background-color 0.2s ease;
  gap: 1rem;
  position: relative;
}

.recent-project:hover {
  background: #e9ecef;
}

.project-name {
  color: #2c3e50;
  font-weight: 500;
  flex: 1;
}

.project-type {
  background: #3498db;
  color: white;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.8rem;
  font-weight: 500;
}

.project-date {
  color: #7f8c8d;
  font-size: 0.9rem;
  min-width: 80px;
  text-align: right;
}

.delete-project-btn {
  background: #e74c3c;
  color: white;
  border: none;
  border-radius: 50%;
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  font-size: 16px;
  font-weight: bold;
  transition: background-color 0.2s ease;
}

.delete-project-btn:hover {
  background: #c0392b;
  transform: scale(1.1);
}

.no-projects, .loading {
  text-align: center;
  padding: 2rem;
  color: #7f8c8d;
}

.loading {
  color: #3498db;
}
</style>
