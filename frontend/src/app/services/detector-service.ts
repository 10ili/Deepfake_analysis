import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { Detector, DetectorRequest } from '../models/detector';

@Injectable({ providedIn: 'root' })
export class DetectorService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/detectors`;

  getAll(active?: boolean) {
    return this.http.get<Detector[]>(this.url, {
      params: active === undefined ? {} : { active },
    });
  }

  getById(id: number) {
    return this.http.get<Detector>(`${this.url}/${id}`);
  }

  create(request: DetectorRequest) {
    return this.http.post<Detector>(this.url, request);
  }

  update(id: number, request: DetectorRequest) {
    return this.http.put<Detector>(`${this.url}/${id}`, request);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
