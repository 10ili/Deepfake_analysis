import { DatePipe, PercentPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, EMPTY, merge, startWith, Subject, switchMap } from 'rxjs';
import { ConfirmDialog } from '../../components/confirm-dialog/confirm-dialog';
import { StartAnalysisDialog } from '../../components/start-analysis-dialog/start-analysis-dialog';
import { AnalysisResult, AnalysisStatus, Verdict } from '../../models/analysis';
import { Detector } from '../../models/detector';
import { ImageRecord } from '../../models/image';
import { AnalysisService } from '../../services/analysis-service';
import { AuthService } from '../../services/auth-service';
import { DetectorService } from '../../services/detector-service';
import { ImageService } from '../../services/image-service';
import { errorMessage } from '../../utils/error-message';
import { statusLabels, verdictLabels } from '../../utils/labels';
import { sortItems } from '../../utils/sort-items';

@Component({
  selector: 'app-analyses',
  imports: [
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    PercentPipe,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './analyses.html',
  styleUrl: './analyses.scss',
})
export class Analyses {
  private analysisService = inject(AnalysisService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private imageId = Number(inject(ActivatedRoute).snapshot.queryParamMap.get('imageId'));

  isAdmin = inject(AuthService).isAdmin;
  reload = new Subject<void>();

  verdicts = Object.keys(verdictLabels) as Verdict[];
  verdictLabels = verdictLabels;
  statuses = Object.keys(statusLabels) as AnalysisStatus[];
  statusLabels = statusLabels;

  form = new FormGroup({
    imageId: new FormControl<number | null>(this.imageId || null),
    detectorId: new FormControl<number | null>(null),
    verdict: new FormControl<Verdict | null>(null),
    status: new FormControl<AnalysisStatus | null>(null),
  });

  analyses = signal<AnalysisResult[]>([]);
  images = signal<ImageRecord[]>([]);
  detectors = signal<Detector[]>([]);
  loading = signal(true);
  sort = signal<Sort>({ active: 'createdAt', direction: 'desc' });

  rows = computed(() => {
    const rows = this.analyses().map((analysis) => ({
      ...analysis,
      fileName: this.images().find((i) => i.id === analysis.imageId)?.fileName ?? '',
      detectorName: this.detectors().find((d) => d.id === analysis.detectorId)?.name ?? '',
    }));
    return sortItems(rows, this.sort());
  });

  columns = computed(() => {
    const columns = [
      'id',
      'fileName',
      'detectorName',
      'status',
      'verdict',
      'generatedProbability',
      'createdAt',
    ];
    return this.isAdmin() ? [...columns, 'actions'] : columns;
  });

  constructor() {
    inject(ImageService)
      .getAll()
      .subscribe({
        next: (images) => this.images.set(images),
        error: (error) => this.onError(error),
      });
    inject(DetectorService)
      .getAll()
      .subscribe({
        next: (detectors) => this.detectors.set(detectors),
        error: (error) => this.onError(error),
      });

    merge(this.form.valueChanges, this.reload)
      .pipe(
        startWith(null),
        switchMap(() => this.load()),
        takeUntilDestroyed(),
      )
      .subscribe((analyses) => {
        this.analyses.set(analyses);
        this.loading.set(false);
      });
  }

  statusLabel(status: AnalysisStatus) {
    return statusLabels[status];
  }

  verdictLabel(verdict: Verdict) {
    return verdictLabels[verdict];
  }

  start() {
    this.dialog.open(StartAnalysisDialog, { data: this.form.value.imageId, width: '440px' });
  }

  delete(analysis: AnalysisResult) {
    this.dialog
      .open(ConfirmDialog, { data: `Удалить анализ №${analysis.id}?` })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) {
          this.analysisService.delete(analysis.id).subscribe({
            next: () => {
              this.showMessage('Анализ удалён');
              this.reload.next();
            },
            error: (error) => this.onError(error),
          });
        }
      });
  }

  private load() {
    this.loading.set(true);
    return this.analysisService.getAll(this.form.getRawValue()).pipe(
      catchError((error) => {
        this.onError(error);
        return EMPTY;
      }),
    );
  }

  private onError(error: HttpErrorResponse) {
    this.loading.set(false);
    this.showMessage(errorMessage(error));
  }

  private showMessage(message: string) {
    this.snackBar.open(message, 'OK', { duration: 4000 });
  }
}
