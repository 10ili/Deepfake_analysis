export type Verdict = 'Real' | 'Generated';
export type AnalysisStatus = 'Queued' | 'Running' | 'Completed' | 'Failed';
export type ImageRegion = 'Top' | 'Center' | 'Bottom' | 'Left' | 'Right';
export type AnomalyType = 'GeometryAndContours' | 'TextureAndNoise';

export interface AnalysisResult {
  id: number;
  imageId: number;
  detectorId: number;
  status: AnalysisStatus;
  error: string | null;
  realProbability: number | null;
  generatedProbability: number | null;
  verdict: Verdict | null;
  hasHeatmap: boolean;
  entropy: number | null;
  concentration: number | null;
  stdDeviation: number | null;
  criticalRegion: ImageRegion | null;
  anomalyType: AnomalyType | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  durationSeconds: number | null;
}

export interface AnalysisFilter {
  imageId: number | null;
  detectorId: number | null;
  verdict: Verdict | null;
  status: AnalysisStatus | null;
}

export interface AnalysisRequest {
  imageId: number;
  detectorId: number;
}
