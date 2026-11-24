import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'fileSize' })
export class FileSizePipe implements PipeTransform {
  transform(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} Б`;
    }
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(0)} КБ`;
    }
    return `${(bytes / 1024 / 1024).toFixed(1).replace('.', ',')} МБ`;
  }
}
