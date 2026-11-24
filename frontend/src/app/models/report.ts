import { Verdict } from './analysis';
import { DetectorModel } from './detector';

export interface AnalysisDetails {
  analysisId: number;
  createdAt: string;
  imageId: number;
  fileName: string;
  detectorId: number;
  detectorName: string;
  realProbability: number;
  verdict: Verdict;
  conclusion: string;
}

export interface DetectorStatistics {
  detectorId: number;
  detectorName: string;
  model: DetectorModel;
  isActive: boolean;
  threshold: number;
  analysesCount: number;
  generatedCount: number;
  realCount: number;
  averageRealProbability: number | null;
  averageDurationSeconds: number | null;
}

export interface ImageSummary {
  imageId: number;
  fileName: string;
  megapixels: number;
  analysesCount: number;
  lastVerdict: Verdict | null;
  lastDetectorName: string | null;
  lastAnalyzedAt: string | null;
}

export interface DetectorVerdict {
  detectorName: string;
  realProbability: number;
  verdict: Verdict;
}

export interface ImageDisagreement {
  imageId: number;
  fileName: string;
  verdicts: DetectorVerdict[];
}
