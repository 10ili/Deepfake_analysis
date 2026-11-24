import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { ConfirmDialog } from '../../components/confirm-dialog/confirm-dialog';
import { Detector, DetectorModel } from '../../models/detector';
import { AuthService } from '../../services/auth-service';
import { DetectorService } from '../../services/detector-service';
import { errorMessage } from '../../utils/error-message';
import { modelLabels } from '../../utils/labels';
import { sortItems } from '../../utils/sort-items';

@Component({
  selector: 'app-detectors',
  imports: [
    DecimalPipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    RouterLink,
  ],
  templateUrl: './detectors.html',
  styleUrl: './detectors.scss',
})
export class Detectors {
  private detectorService = inject(DetectorService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  isAdmin = inject(AuthService).isAdmin;
  detectors = signal<Detector[]>([]);
  loading = signal(true);
  active = signal<boolean | undefined>(undefined);
  sort = signal<Sort>({ active: 'name', direction: 'asc' });

  sortedDetectors = computed(() => sortItems(this.detectors(), this.sort()));
  columns = computed(() => {
    const columns = ['name', 'model', 'threshold', 'hasHeatmap', 'isActive'];
    return this.isAdmin() ? [...columns, 'actions'] : columns;
  });

  constructor() {
    this.load();
  }

  modelLabel(model: DetectorModel) {
    return modelLabels[model];
  }

  setActive(active: boolean | undefined) {
    this.active.set(active);
    this.load();
  }

  delete(detector: Detector) {
    this.dialog
      .open(ConfirmDialog, { data: `Удалить детектор «${detector.name}»?` })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) {
          this.detectorService.delete(detector.id).subscribe({
            next: () => {
              this.showMessage('Детектор удалён');
              this.load();
            },
            error: (error) => this.onError(error),
          });
        }
      });
  }

  private load() {
    this.loading.set(true);
    this.detectorService.getAll(this.active()).subscribe({
      next: (detectors) => {
        this.detectors.set(detectors);
        this.loading.set(false);
      },
      error: (error) => this.onError(error),
    });
  }

  private onError(error: HttpErrorResponse) {
    this.loading.set(false);
    this.showMessage(errorMessage(error));
  }

  private showMessage(message: string) {
    this.snackBar.open(message, 'OK', { duration: 4000 });
  }
}
