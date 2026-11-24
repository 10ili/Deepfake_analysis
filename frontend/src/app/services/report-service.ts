import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { Verdict } from '../models/analysis';
import {
  AnalysisDetails,
  DetectorStatistics,
  ImageDisagreement,
  ImageSummary,
} from '../models/report';

@Injectable({ providedIn: 'root' })
export class ReportService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/reports`;

  getAnalyses(verdict?: Verdict) {
    return this.http.get<AnalysisDetails[]>(`${this.url}/analyses`, {
      params: verdict ? { verdict } : {},
    });
  }

  getDetectors() {
    return this.http.get<DetectorStatistics[]>(`${this.url}/detectors`);
  }

  getImages(onlyUnchecked = false) {
    return this.http.get<ImageSummary[]>(`${this.url}/images`, { params: { onlyUnchecked } });
  }

  getDisagreements() {
    return this.http.get<ImageDisagreement[]>(`${this.url}/disagreements`);
  }
}
