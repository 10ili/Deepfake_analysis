import { PercentPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { RouterLink } from '@angular/router';
import { ImageDisagreement } from '../../../models/report';
import { ImageService } from '../../../services/image-service';
import { ReportService } from '../../../services/report-service';
import { errorMessage } from '../../../utils/error-message';
import { verdictLabels } from '../../../utils/labels';

@Component({
  selector: 'app-disagreements-report',
  imports: [MatCardModule, PercentPipe, RouterLink],
  templateUrl: './disagreements-report.html',
  styleUrls: ['../report.scss', './disagreements-report.scss'],
})
export class DisagreementsReport {
  private imageService = inject(ImageService);

  verdictLabels = verdictLabels;
  items = signal<ImageDisagreement[]>([]);
  loaded = signal(false);
  error = signal('');

  constructor() {
    inject(ReportService)
      .getDisagreements()
      .subscribe({
        next: (items) => {
          this.items.set(items);
          this.loaded.set(true);
        },
        error: (error) => this.error.set(errorMessage(error)),
      });
  }

  fileUrl(item: ImageDisagreement) {
    return this.imageService.fileUrl(item.imageId);
  }
}
