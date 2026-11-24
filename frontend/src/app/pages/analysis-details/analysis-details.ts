import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { exhaustMap, takeWhile, timer } from 'rxjs';
import { ConfirmDialog } from '../../components/confirm-dialog/confirm-dialog';
import { AnalysisResult } from '../../models/analysis';
import { Detector } from '../../models/detector';
import { ImageRecord } from '../../models/image';
import { AnalysisService } from '../../services/analysis-service';
import { AuthService } from '../../services/auth-service';
import { DetectorService } from '../../services/detector-service';
import { ImageService } from '../../services/image-service';
import { errorMessage } from '../../utils/error-message';
import { anomalyLabels, regionLabels, statusLabels, verdictLabels } from '../../utils/labels';

@Component({
  selector: 'app-analysis-details',
  imports: [
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatProgressBarModule,
    PercentPipe,
    RouterLink,
  ],
  templateUrl: './analysis-details.html',
  styleUrl: './analysis-details.scss',
})
export class AnalysisDetails {
  private analysisService = inject(AnalysisService);
  private imageService = inject(ImageService);
  private detectorService = inject(DetectorService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);
  private id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  isAdmin = inject(AuthService).isAdmin;
  anomalyLabels = anomalyLabels;
  regionLabels = regionLabels;
  statusLabels = statusLabels;
  verdictLabels = verdictLabels;

  analysis = signal<AnalysisResult | null>(null);
  image = signal<ImageRecord | null>(null);
  detector = signal<Detector | null>(null);
  error = signal('');
  picture = signal<'original' | 'overlay' | 'heatmap'>('original');

  pending = computed(() => this.isPending(this.analysis()));
  pictureUrl = computed(() => {
    const analysis = this.analysis();
    if (!analysis) {
      return '';
    }
    if (this.picture() === 'original') {
      return this.imageService.fileUrl(analysis.imageId);
    }
    return this.analysisService.heatmapUrl(analysis.id, this.picture() === 'overlay');
  });

  constructor() {
    timer(0, 2000)
      .pipe(
        exhaustMap(() => this.analysisService.getById(this.id)),
        takeWhile((analysis) => this.isPending(analysis), true),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (analysis) => {
          if (!this.analysis()) {
            this.loadNames(analysis);
          }
          this.analysis.set(analysis);
        },
        error: (error) => this.error.set(errorMessage(error)),
      });
  }

  delete() {
    this.dialog
      .open(ConfirmDialog, { data: `Удалить анализ №${this.id}?` })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) {
          this.analysisService.delete(this.id).subscribe({
            next: () => {
              this.showMessage('Анализ удалён');
              this.router.navigate(['/analyses']);
            },
            error: (error: HttpErrorResponse) => this.showMessage(errorMessage(error)),
          });
        }
      });
  }

  private isPending(analysis: AnalysisResult | null) {
    return analysis?.status === 'Queued' || analysis?.status === 'Running';
  }

  private loadNames(analysis: AnalysisResult) {
    this.imageService.getById(analysis.imageId).subscribe((image) => this.image.set(image));
    this.detectorService
      .getById(analysis.detectorId)
      .subscribe((detector) => this.detector.set(detector));
  }

  private showMessage(message: string) {
    this.snackBar.open(message, 'OK', { duration: 4000 });
  }
}
