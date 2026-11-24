import { DetectorModel } from '../models/detector';

export const modelLabels: Record<DetectorModel, string> = {
  EfficientNetB3: 'EfficientNet-B3',
  ResNet50: 'ResNet50',
  ResNet50CatBoost: 'ResNet50 + CatBoost',
};
