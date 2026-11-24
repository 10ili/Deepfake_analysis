import { DecimalPipe, PercentPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { DetectorModel } from '../../../models/detector';
import { DetectorStatistics } from '../../../models/report';
import { ReportService } from '../../../services/report-service';
import { errorMessage } from '../../../utils/error-message';
import { modelLabels } from '../../../utils/labels';
import { sortItems } from '../../../utils/sort-items';

@Component({
  selector: 'app-detectors-report',
  imports: [DecimalPipe, MatSortModule, MatTableModule, PercentPipe],
  templateUrl: './detectors-report.html',
  styleUrl: '../report.scss',
})
export class DetectorsReport {
  columns = [
    'detectorName',
    'model',
    'threshold',
    'analysesCount',
    'generatedCount',
    'realCount',
    'averageRealProbability',
    'averageDurationSeconds',
  ];

  items = signal<DetectorStatistics[]>([]);
  error = signal('');
  sort = signal<Sort>({ active: 'analysesCount', direction: 'desc' });
  rows = computed(() => sortItems(this.items(), this.sort()));

  constructor() {
    inject(ReportService)
      .getDetectors()
      .subscribe({
        next: (items) => this.items.set(items),
        error: (error) => this.error.set(errorMessage(error)),
      });
  }

  modelLabel(model: DetectorModel) {
    return modelLabels[model];
  }
}
