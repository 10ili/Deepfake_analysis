import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { Verdict } from '../../../models/analysis';
import { ImageSummary } from '../../../models/report';
import { ReportService } from '../../../services/report-service';
import { errorMessage } from '../../../utils/error-message';
import { verdictLabels } from '../../../utils/labels';
import { sortItems } from '../../../utils/sort-items';

@Component({
  selector: 'app-images-report',
  imports: [DatePipe, DecimalPipe, MatCheckboxModule, MatSortModule, MatTableModule, RouterLink],
  templateUrl: './images-report.html',
  styleUrl: '../report.scss',
})
export class ImagesReport {
  private reportService = inject(ReportService);

  columns = [
    'fileName',
    'megapixels',
    'analysesCount',
    'lastVerdict',
    'lastDetectorName',
    'lastAnalyzedAt',
  ];

  items = signal<ImageSummary[]>([]);
  loaded = signal(false);
  error = signal('');
  sort = signal<Sort>({ active: 'analysesCount', direction: 'desc' });
  rows = computed(() => sortItems(this.items(), this.sort()));

  constructor() {
    this.load(false);
  }

  verdictLabel(verdict: Verdict) {
    return verdictLabels[verdict];
  }

  load(onlyUnchecked: boolean) {
    this.error.set('');
    this.reportService.getImages(onlyUnchecked).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loaded.set(true);
      },
      error: (error) => this.error.set(errorMessage(error)),
    });
  }
}
