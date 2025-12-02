import type { 
  PipelineElementConfig, 
  PipelineElementDefinition,
  CategoryDefinition,
  ProjectTypeDefinition 
} from '@/types/pipeline-config';

class ConfigService {
  private config: PipelineElementConfig | null = null;

  async loadConfig(): Promise<PipelineElementConfig> {
    if (this.config) {
      return this.config;
    }

    try {
      const response = await fetch('/config/pipeline-elements.json');
      this.config = await response.json();
      return this.config;
    } catch (error) {
      console.error('Failed to load pipeline elements config:', error);
      throw error;
    }
  }

  getProjectTypes(): Record<string, ProjectTypeDefinition> {
    return this.config?.projectTypes || {};
  }

  getProjectType(type: string): ProjectTypeDefinition | null {
    return this.config?.projectTypes[type] || null;
  }

  getElementsForProjectType(projectType: string): string[] {
    return this.config?.projectTypes[projectType]?.elements || [];
  }

  getElementDefinition(type: string): PipelineElementDefinition | null {
    return this.config?.elements[type] || null;
  }

  getElementsByCategory(projectType?: string): Array<{
    category: CategoryDefinition;
    elements: Array<{ type: string; definition: PipelineElementDefinition }>;
  }> {
    if (!this.config) return [];

    const categories = this.getCategories();
    const allElements = this.getAllElements();
    
    let elements = allElements;
    if (projectType) {
      const allowedElements = this.getElementsForProjectType(projectType);
      elements = Object.fromEntries(
        Object.entries(allElements).filter(([type]) => allowedElements.includes(type))
      );
    }

    const grouped: Record<string, Array<{ type: string; definition: PipelineElementDefinition }>> = {};

    Object.entries(elements).forEach(([type, definition]) => {
      const category = definition.category;
      if (!grouped[category]) {
        grouped[category] = [];
      }
      grouped[category].push({ type, definition });
    });

    return Object.entries(grouped)
      .map(([categoryId, categoryElements]) => ({
        category: categories[categoryId],
        elements: categoryElements
      }))
      .sort((a, b) => a.category.order - b.category.order);
  }

  getAllElements(): Record<string, PipelineElementDefinition> {
    return this.config?.elements || {};
  }

  getCategories(): Record<string, CategoryDefinition> {
    return this.config?.categories || {};
  }

  getDefaultProperties(type: string): Record<string, any> {
    const definition = this.getElementDefinition(type);
    return definition ? { ...definition.defaultProperties } : {};
  }

  generateNodeName(type: string): string {
    const definition = this.getElementDefinition(type);
    const baseName = definition?.name || type;
    const timestamp = Date.now().toString().slice(-4);
    return `${baseName}_${timestamp}`;
  }
}

export const configService = new ConfigService();
