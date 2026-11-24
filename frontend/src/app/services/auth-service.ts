import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { LoginRequest, LoginResponse } from '../models/auth';

const STORAGE_KEY = 'user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);

  user = signal(this.loadUser());
  isAdmin = computed(() => this.user()?.role === 'Admin');

  login(request: LoginRequest) {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/login`, request).pipe(
      tap((user) => {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
        this.user.set(user);
      }),
    );
  }

  logout() {
    localStorage.removeItem(STORAGE_KEY);
    this.user.set(null);
  }

  private loadUser(): LoginResponse | null {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (!saved) {
      return null;
    }
    const user: LoginResponse = JSON.parse(saved);
    return new Date(user.expiresAt) > new Date() ? user : null;
  }
}
