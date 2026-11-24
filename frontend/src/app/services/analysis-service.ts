import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { AnalysisFilter, AnalysisRequest, AnalysisResult } from '../models/analysis';

@Injectable({ providedIn: 'root' })
export class AnalysisService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/analyses`;

  getAll(filter: AnalysisFilter) {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value) {
        params = params.set(key, value);
      }
    }
    return this.http.get<AnalysisResult[]>(this.url, { params });
  }

  getById(id: number) {
    return this.http.get<AnalysisResult>(`${this.url}/${id}`);
  }

  heatmapUrl(id: number, overlay = false) {
    return `${this.url}/${id}/heatmap?overlay=${overlay}`;
  }

  start(request: AnalysisRequest) {
    return this.http.post<AnalysisResult>(this.url, request);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
