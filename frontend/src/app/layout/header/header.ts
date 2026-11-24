import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth-service';

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive, MatButtonModule, MatIconModule, MatMenuModule],
  templateUrl: './header.html',
  styleUrl: './header.scss',
})
export class Header {
  private auth = inject(AuthService);
  private router = inject(Router);

  user = this.auth.user;
  isAdmin = this.auth.isAdmin;

  links = [
    { path: '/images', title: 'Изображения', icon: 'image' },
    { path: '/analyses', title: 'Анализы', icon: 'query_stats' },
    { path: '/detectors', title: 'Детекторы', icon: 'memory' },
    { path: '/reports', title: 'Отчёты', icon: 'description' },
  ];

  logout() {
    this.auth.logout();
    this.router.navigate(['/']);
  }
}
