import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { Verdict } from '../../../models/analysis';
import { AnalysisDetails } from '../../../models/report';
import { ReportService } from '../../../services/report-service';
import { errorMessage } from '../../../utils/error-message';
import { verdictLabels } from '../../../utils/labels';
import { sortItems } from '../../../utils/sort-items';

@Component({
  selector: 'app-analyses-report',
  imports: [
    DatePipe,
    MatFormFieldModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    RouterLink,
  ],
  templateUrl: './analyses-report.html',
  styleUrl: '../report.scss',
})
export class AnalysesReport {
  private reportService = inject(ReportService);

  columns = ['analysisId', 'createdAt', 'fileName', 'detectorName', 'verdict', 'conclusion'];
  verdicts = Object.keys(verdictLabels) as Verdict[];
  verdictLabels = verdictLabels;

  items = signal<AnalysisDetails[]>([]);
  loaded = signal(false);
  error = signal('');
  sort = signal<Sort>({ active: 'createdAt', direction: 'desc' });
  rows = computed(() => sortItems(this.items(), this.sort()));

  constructor() {
    this.load();
  }

  verdictLabel(verdict: Verdict) {
    return verdictLabels[verdict];
  }

  load(verdict?: Verdict) {
    this.error.set('');
    this.reportService.getAnalyses(verdict).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loaded.set(true);
      },
      error: (error) => this.error.set(errorMessage(error)),
    });
  }
}
