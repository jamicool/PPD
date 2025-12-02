<template>
  <div id="app">
    <ProjectTypeSelector />
    
    <header class="app-header" v-if="store.currentProject">
      <div class="project-name-section">
        <input 
          v-model="projectName" 
          @blur="updateProjectName"
          @keyup.enter="updateProjectName"
          class="project-name-input"
          placeholder="Введите имя проекта"
        />
        <span class="project-type-badge">{{ getProjectTypeName() }}</span>
      </div>
      <div class="header-controls">
        <button @click="createNewProject" class="btn-secondary">Новый проект</button>
        <button @click="saveProject" class="btn-primary">Сохранить</button>
        <button @click="exportProject" class="btn-secondary">Экспорт в файл</button>
        <button @click="importProject" class="btn-secondary">Импорт из файла</button>
        <button @click="deleteCurrentProject" class="btn-danger">Удалить проект</button>
      </div>
    </header>
    
    <main class="app-main" v-if="store.currentProject">
      <RouterView />
    </main>
    
    <footer class="app-footer" v-if="store.currentProject">
      <p>&copy; 2024 Pipeline Designer. Проект: {{ store.currentProject.name }}</p>
    </footer>

    <!-- File input for import -->
    <input 
      type="file" 
      ref="fileInput" 
      @change="handleFileImport" 
      accept=".json" 
      style="display: none" 
    />
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { usePipelineStore } from './stores/pipeline-store'
import ProjectTypeSelector from './components/ProjectTypeSelector.vue'
import { configService } from './services/config-service'

const store = usePipelineStore()
const fileInput = ref<HTMLInputElement>()
const projectName = ref('')

// Watch for project changes to update the name input
watch(() => store.currentProject, (newProject) => {
  if (newProject) {
    projectName.value = newProject.name
  }
}, { immediate: true })

const updateProjectName = () => {
  if (store.currentProject && projectName.value.trim()) {
    store.currentProject.name = projectName.value.trim()
    saveProject()
  }
}

const createNewProject = () => {
  store.currentProject = null
}

const saveProject = async () => {
  if (store.currentProject) {
    try {
      await store.saveProject()
      console.log('Project saved successfully')
    } catch (error) {
      console.error('Failed to save project:', error)
      alert('Ошибка при сохранении проекта: ' + (error instanceof Error ? error.message : 'Неизвестная ошибка'))
    }
  }
}

const exportProject = async () => {
  if (store.currentProject) {
    try {
      await store.exportProject()
    } catch (error) {
      console.error('Failed to export project:', error)
      alert('Ошибка при экспорте проекта: ' + (error instanceof Error ? error.message : 'Неизвестная ошибка'))
    }
  }
}

const importProject = () => {
  fileInput.value?.click()
}

const deleteCurrentProject = async () => {
  if (!store.currentProject) return
  
  const projectName = store.currentProject.name
  if (!confirm(`Вы уверены, что хотите удалить проект "${projectName}"? Это действие нельзя отменить.`)) {
    return
  }

  try {
    const response = await fetch(`/api/pipeline/projects/${store.currentProject.id}`, {
      method: 'DELETE'
    })

    if (!response.ok) {
      throw new Error(`Failed to delete project: ${response.status}`)
    }

    // Закрываем проект после удаления
    store.currentProject = null
    
    // Обновляем список проектов
    await store.loadProjects()
    
    console.log(`Project ${store.currentProject?.id} deleted successfully`)
  } catch (error) {
    console.error('Failed to delete project:', error)
    alert('Ошибка при удалении проекта: ' + (error instanceof Error ? error.message : 'Неизвестная ошибка'))
  }
}

const handleFileImport = async (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (file) {
    try {
      await store.importProject(file)
      target.value = '' // Reset input
    } catch (error) {
      console.error('Failed to import project:', error)
      alert('Ошибка при импорте проекта: ' + (error instanceof Error ? error.message : 'Неизвестная ошибка'))
    }
  }
}

const getProjectTypeName = () => {
  if (!store.currentProject) return ''
  const projectType = configService.getProjectType(store.currentProject.type)
  return projectType?.name || store.currentProject.type
}
</script>

<style>
.app-header {
  background: #2c3e50;
  color: white;
  padding: 1rem 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  z-index: 1000;
  position: relative;
}

.project-name-section {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.project-name-input {
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.3);
  color: white;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  font-size: 1.5rem;
  font-weight: bold;
  min-width: 300px;
}

.project-name-input:focus {
  outline: none;
  border-color: #3498db;
  background: rgba(255, 255, 255, 0.15);
}

.project-name-input::placeholder {
  color: rgba(255, 255, 255, 0.6);
}

.project-type-badge {
  background: #3498db;
  color: white;
  padding: 0.25rem 0.75rem;
  border-radius: 20px;
  font-size: 0.9rem;
  font-weight: 500;
}

.header-controls {
  display: flex;
  gap: 1rem;
}

.btn-primary {
  background: #3498db;
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.2s ease;
}

.btn-primary:hover {
  background: #2980b9;
}

.btn-secondary {
  background: #95a5a6;
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.2s ease;
}

.btn-secondary:hover {
  background: #7f8c8d;
}

.btn-danger {
  background: #e74c3c;
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.2s ease;
}

.btn-danger:hover {
  background: #c0392b;
}

.app-main {
  flex: 1;
  display: flex;
  overflow: hidden;
}

.app-footer {
  background: #34495e;
  color: #bdc3c7;
  text-align: center;
  padding: 1rem;
  font-size: 0.9rem;
  z-index: 1000;
  position: relative;
}

#app {
  display: flex;
  flex-direction: column;
  height: 100vh;
  width: 100vw;
}
</style>
