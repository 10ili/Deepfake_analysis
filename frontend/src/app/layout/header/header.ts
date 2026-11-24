import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive, MatButtonModule, MatIconModule, MatMenuModule],
  templateUrl: './header.html',
  styleUrl: './header.scss',
})
export class Header {
  links = [
    { path: '/images', title: 'Изображения', icon: 'image' },
    { path: '/analyses', title: 'Анализы', icon: 'query_stats' },
    { path: '/detectors', title: 'Детекторы', icon: 'memory' },
    { path: '/reports', title: 'Отчёты', icon: 'description' },
  ];
}
