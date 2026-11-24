import { Routes } from '@angular/router';
import { Analyses } from './pages/analyses/analyses';
import { Detectors } from './pages/detectors/detectors';
import { Images } from './pages/images/images';
import { NotFound } from './pages/not-found/not-found';
import { Reports } from './pages/reports/reports';

export const routes: Routes = [
  { path: '', redirectTo: 'images', pathMatch: 'full' },
  { path: 'images', component: Images, title: 'Изображения' },
  { path: 'analyses', component: Analyses, title: 'Анализы' },
  { path: 'detectors', component: Detectors, title: 'Детекторы' },
  { path: 'reports', component: Reports, title: 'Отчёты' },
  { path: '**', component: NotFound, title: 'Страница не найдена' },
];
