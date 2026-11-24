import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { ImageRecord } from '../models/image';

@Injectable({ providedIn: 'root' })
export class ImageService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/images`;

  getAll(search = '', extension = '') {
    return this.http.get<ImageRecord[]>(this.url, { params: { search, extension } });
  }

  getById(id: number) {
    return this.http.get<ImageRecord>(`${this.url}/${id}`);
  }

  fileUrl(id: number) {
    return `${this.url}/${id}/file`;
  }

  upload(file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ImageRecord>(this.url, form);
  }

  rename(id: number, fileName: string) {
    return this.http.put<ImageRecord>(`${this.url}/${id}`, { fileName });
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
