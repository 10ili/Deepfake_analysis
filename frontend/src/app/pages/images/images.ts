import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { catchError, debounceTime, EMPTY, merge, startWith, Subject, switchMap } from 'rxjs';
import { ConfirmDialog } from '../../components/confirm-dialog/confirm-dialog';
import { RenameDialog } from '../../components/rename-dialog/rename-dialog';
import { ImageRecord } from '../../models/image';
import { FileSizePipe } from '../../pipes/file-size-pipe';
import { AuthService } from '../../services/auth-service';
import { ImageService } from '../../services/image-service';
import { errorMessage } from '../../utils/error-message';
import { sortItems } from '../../utils/sort-items';

const MAX_SIZE = 20 * 1024 * 1024;

@Component({
  selector: 'app-images',
  imports: [
    DatePipe,
    FileSizePipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './images.html',
  styleUrl: './images.scss',
})
export class Images {
  private imageService = inject(ImageService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private reload = new Subject<void>();

  isAdmin = inject(AuthService).isAdmin;
  form = inject(NonNullableFormBuilder).group({ search: '', extension: '' });

  images = signal<ImageRecord[]>([]);
  loading = signal(true);
  view = signal<'table' | 'cards'>('table');
  sort = signal<Sort>({ active: 'uploadedAt', direction: 'desc' });

  sortedImages = computed(() => sortItems(this.images(), this.sort()));
  columns = computed(() => {
    const columns = ['preview', 'fileName', 'sizeBytes', 'megapixels', 'uploadedAt'];
    return this.isAdmin() ? [...columns, 'actions'] : columns;
  });

  constructor() {
    merge(this.form.valueChanges.pipe(debounceTime(300)), this.reload)
      .pipe(
        startWith(null),
        switchMap(() => this.load()),
        takeUntilDestroyed(),
      )
      .subscribe((images) => {
        this.images.set(images);
        this.loading.set(false);
      });
  }

  fileUrl(image: ImageRecord) {
    return this.imageService.fileUrl(image.id);
  }

  upload(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    if (!/\.(jpe?g|png)$/i.test(file.name)) {
      this.showMessage('Можно загрузить только JPEG или PNG');
      return;
    }
    if (file.size > MAX_SIZE) {
      this.showMessage('Размер файла больше 20 МБ');
      return;
    }

    this.loading.set(true);
    this.imageService.upload(file).subscribe({
      next: () => this.onChanged('Изображение загружено'),
      error: (error) => this.onError(error),
    });
  }

  rename(image: ImageRecord) {
    this.dialog
      .open(RenameDialog, { data: image.fileName, width: '440px' })
      .afterClosed()
      .subscribe((fileName) => {
        if (fileName) {
          this.imageService.rename(image.id, fileName).subscribe({
            next: () => this.onChanged('Изображение переименовано'),
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
            next: () => this.onChanged('Изображение удалено'),
            error: (error) => this.onError(error),
          });
        }
      });
  }

  private load() {
    this.loading.set(true);
    const { search, extension } = this.form.getRawValue();
    return this.imageService.getAll(search.trim(), extension).pipe(
      catchError((error) => {
        this.onError(error);
        return EMPTY;
      }),
    );
  }

  private onChanged(message: string) {
    this.showMessage(message);
    this.reload.next();
  }

  private onError(error: HttpErrorResponse) {
    this.loading.set(false);
    this.showMessage(errorMessage(error));
  }

  private showMessage(message: string) {
    this.snackBar.open(message, 'OK', { duration: 4000 });
  }
}
