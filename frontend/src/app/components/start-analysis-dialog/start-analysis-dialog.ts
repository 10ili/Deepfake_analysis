import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { Detector } from '../../models/detector';
import { ImageRecord } from '../../models/image';
import { AnalysisService } from '../../services/analysis-service';
import { DetectorService } from '../../services/detector-service';
import { ImageService } from '../../services/image-service';
import { errorMessage } from '../../utils/error-message';

@Component({
  selector: 'app-start-analysis-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    ReactiveFormsModule,
  ],
  templateUrl: './start-analysis-dialog.html',
  styleUrl: './start-analysis-dialog.scss',
})
export class StartAnalysisDialog {
  private analysisService = inject(AnalysisService);
  private dialogRef = inject(MatDialogRef<StartAnalysisDialog>);
  private router = inject(Router);

  images = signal<ImageRecord[]>([]);
  detectors = signal<Detector[]>([]);
  saving = signal(false);
  error = signal('');

  form = new FormGroup({
    imageId: new FormControl(inject<number | null>(MAT_DIALOG_DATA), Validators.required),
    detectorId: new FormControl<number | null>(null, Validators.required),
  });

  constructor() {
    inject(ImageService)
      .getAll()
      .subscribe({
        next: (images) => this.images.set(images),
        error: (error) => this.error.set(errorMessage(error)),
      });
    inject(DetectorService)
      .getAll(true)
      .subscribe({
        next: (detectors) => this.detectors.set(detectors),
        error: (error) => this.error.set(errorMessage(error)),
      });
  }

  start() {
    const { imageId, detectorId } = this.form.value;
    if (!imageId || !detectorId) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.analysisService.start({ imageId, detectorId }).subscribe({
      next: (analysis) => {
        this.dialogRef.close();
        this.router.navigate(['/analyses', analysis.id]);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(errorMessage(error));
        this.saving.set(false);
      },
    });
  }
}
