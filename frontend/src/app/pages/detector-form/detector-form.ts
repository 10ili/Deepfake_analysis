import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DetectorModel } from '../../models/detector';
import { DetectorService } from '../../services/detector-service';
import { errorMessage } from '../../utils/error-message';
import { modelLabels } from '../../utils/labels';

@Component({
  selector: 'app-detector-form',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './detector-form.html',
  styleUrl: './detector-form.scss',
})
export class DetectorForm {
  private detectorService = inject(DetectorService);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);

  id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));
  models = Object.keys(modelLabels) as DetectorModel[];
  modelLabels = modelLabels;

  form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/\S/)]],
    model: ['EfficientNetB3' as DetectorModel, Validators.required],
    threshold: [0.5, [Validators.required, Validators.min(0.01), Validators.max(0.99)]],
    isActive: true,
  });

  saving = signal(false);
  error = signal('');

  constructor() {
    if (this.id) {
      this.detectorService.getById(this.id).subscribe({
        next: (detector) => this.form.patchValue(detector),
        error: (error) => this.error.set(errorMessage(error)),
      });
    }
  }

  save() {
    if (this.form.invalid) {
      return;
    }

    this.saving.set(true);
    this.error.set('');
    const request = this.form.getRawValue();
    const response = this.id
      ? this.detectorService.update(this.id, request)
      : this.detectorService.create(request);

    response.subscribe({
      next: () => {
        this.snackBar.open('Детектор сохранён', 'OK', { duration: 4000 });
        this.router.navigate(['/detectors']);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(errorMessage(error));
        this.saving.set(false);
      },
    });
  }
}
