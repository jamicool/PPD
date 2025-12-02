export interface PipelineNode {
  id: string;
  type: string;
  position: { x: number; y: number };
  properties: NodeProperties;
  connections: string[];
}

export interface NodeProperties {
  name?: string;
  flowRate?: number;
  pressure?: number;
  diameter?: number;
  status?: 'open' | 'closed' | 'active' | 'standby' | 'maintenance' | 'partial';
  endX?: number;
  endY?: number;
  length?: number;
  roughness?: number;
  material?: string;
  [key: string]: any;
}

export interface PipelineConnection {
  id: string;
  sourceId: string;
  targetId: string;
  properties: ConnectionProperties;
}

export interface ConnectionProperties {
  length?: number;
  diameter?: number;
  roughness?: number;
  flowRate?: number;
  [key: string]: any;
}

export interface PipelineProject {
  id: string;
  name: string;
  type: 'gas' | 'oil';
  nodes: PipelineNode[];
  connections: PipelineConnection[];
  createdAt: Date;
  updatedAt: Date;
}

export interface SimulationResult {
  projectId: string;
  taskId: string;
  userId: string;
  nodeResults: Record<string, any>;
  connectionResults: Record<string, any>;
  isSuccessful: boolean;
  errorMessage: string;
}

export interface ProjectSummary {
  id: string;
  name: string;
  type: 'gas' | 'oil';
  createdAt: Date;
  updatedAt: Date;
}
