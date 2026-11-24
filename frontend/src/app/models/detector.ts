export type DetectorModel = 'EfficientNetB3' | 'ResNet50' | 'ResNet50CatBoost';

export interface Detector {
  id: number;
  name: string;
  model: DetectorModel;
  threshold: number;
  isActive: boolean;
  hasHeatmap: boolean;
}

export interface DetectorRequest {
  name: string;
  model: DetectorModel;
  threshold: number;
  isActive: boolean;
}
