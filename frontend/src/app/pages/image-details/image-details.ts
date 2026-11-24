import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmDialog } from '../../components/confirm-dialog/confirm-dialog';
import { RenameDialog } from '../../components/rename-dialog/rename-dialog';
import { StartAnalysisDialog } from '../../components/start-analysis-dialog/start-analysis-dialog';
import { ImageRecord } from '../../models/image';
import { FileSizePipe } from '../../pipes/file-size-pipe';
import { AuthService } from '../../services/auth-service';
import { ImageService } from '../../services/image-service';
import { errorMessage } from '../../utils/error-message';

@Component({
  selector: 'app-image-details',
  imports: [DatePipe, DecimalPipe, FileSizePipe, MatButtonModule, MatIconModule, RouterLink],
  templateUrl: './image-details.html',
  styleUrl: './image-details.scss',
})
export class ImageDetails {
  private imageService = inject(ImageService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);
  private id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  isAdmin = inject(AuthService).isAdmin;
  image = signal<ImageRecord | null>(null);
  error = signal('');
  fileUrl = this.imageService.fileUrl(this.id);

  constructor() {
    this.imageService.getById(this.id).subscribe({
      next: (image) => this.image.set(image),
      error: (error) => this.error.set(errorMessage(error)),
    });
  }

  analyze(image: ImageRecord) {
    this.dialog.open(StartAnalysisDialog, { data: image.id, width: '440px' });
  }

  rename(image: ImageRecord) {
    this.dialog
      .open(RenameDialog, { data: image.fileName, width: '440px' })
      .afterClosed()
      .subscribe((fileName) => {
        if (fileName) {
          this.imageService.rename(image.id, fileName).subscribe({
            next: (renamed) => {
              this.image.set(renamed);
              this.showMessage('Изображение переименовано');
            },
            error: (error) => this.onError(error),
          });
        }
      });
  }

  delete(image: ImageRecord) {
    this.dialog
      .open(ConfirmDialog, { data: `Удалить «${image.fileName}» вместе с его анализами?` })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) {
          this.imageService.delete(image.id).subscribe({
            next: () => {
              this.showMessage('Изображение удалено');
              this.router.navigate(['/images']);
            },
            error: (error) => this.onError(error),
          });
        }
      });
  }

  private onError(error: HttpErrorResponse) {
    this.showMessage(errorMessage(error));
  }

  private showMessage(message: string) {
    this.snackBar.open(message, 'OK', { duration: 4000 });
  }
}
