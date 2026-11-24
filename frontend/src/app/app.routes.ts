import { Routes } from '@angular/router';
import { authGuard } from './guards/auth-guard';

export const routes: Routes = [
  { path: '', redirectTo: 'images', pathMatch: 'full' },
  {
    path: 'login',
    title: 'Вход',
    loadComponent: () => import('./pages/login/login').then((m) => m.Login),
  },
  {
    path: 'images',
    title: 'Изображения',
    loadComponent: () => import('./pages/images/images').then((m) => m.Images),
  },
  {
    path: 'images/:id',
    title: 'Изображение',
    loadComponent: () => import('./pages/image-details/image-details').then((m) => m.ImageDetails),
  },
  {
    path: 'analyses',
    title: 'Анализы',
    loadComponent: () => import('./pages/analyses/analyses').then((m) => m.Analyses),
  },
  {
    path: 'detectors',
    title: 'Детекторы',
    loadComponent: () => import('./pages/detectors/detectors').then((m) => m.Detectors),
  },
  {
    path: 'reports',
    title: 'Отчёты',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/reports/reports').then((m) => m.Reports),
  },
  {
    path: '**',
    title: 'Страница не найдена',
    loadComponent: () => import('./pages/not-found/not-found').then((m) => m.NotFound),
  },
];
