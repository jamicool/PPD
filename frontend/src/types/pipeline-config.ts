export interface PipelineElementConfig {
  version: string;
  projectTypes: Record<string, ProjectTypeDefinition>;
  elements: Record<string, PipelineElementDefinition>;
  categories: Record<string, CategoryDefinition>;
}

export interface ProjectTypeDefinition {
  name: string;
  description: string;
  elements: string[];
}

export interface PipelineElementDefinition {
  name: string;
  category: string;
  icon: string;
  color: string;
  geometry: GeometryDefinition;
  defaultProperties: Record<string, any>;
  propertySchema: PropertySchema;
}

export interface GeometryDefinition {
  type: 'circle' | 'rectangle' | 'line';
  radius?: number;
  width?: number;
  height?: number;
  length?: number;
}

export interface PropertySchema {
  [key: string]: PropertyDefinition;
}

export interface PropertyDefinition {
  type: 'string' | 'number' | 'select' | 'boolean';
  label: string;
  required?: boolean;
  min?: number;
  max?: number;
  step?: number;
  options?: SelectOption[];
}

export interface SelectOption {
  value: string;
  label: string;
}

export interface CategoryDefinition {
  name: string;
  order: number;
}
