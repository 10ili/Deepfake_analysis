import { AnalysisStatus, AnomalyType, ImageRegion, Verdict } from '../models/analysis';
import { DetectorModel } from '../models/detector';

export const modelLabels: Record<DetectorModel, string> = {
  EfficientNetB3: 'EfficientNet-B3',
  ResNet50: 'ResNet50',
  ResNet50CatBoost: 'ResNet50 + CatBoost',
};

export const verdictLabels: Record<Verdict, string> = {
  Real: 'Настоящее',
  Generated: 'Сгенерированное',
};

export const statusLabels: Record<AnalysisStatus, string> = {
  Queued: 'В очереди',
  Running: 'Выполняется',
  Completed: 'Завершён',
  Failed: 'Ошибка',
};

export const regionLabels: Record<ImageRegion, string> = {
  Top: 'Верхняя часть',
  Center: 'Центр',
  Bottom: 'Нижняя часть',
  Left: 'Слева',
  Right: 'Справа',
};

export const anomalyLabels: Record<AnomalyType, string> = {
  GeometryAndContours: 'Нарушение геометрии и контуров',
  TextureAndNoise: 'Неестественная текстура и шум',
};
