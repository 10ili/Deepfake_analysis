import { HttpErrorResponse } from '@angular/common/http';

export function errorMessage(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'Сервер недоступен';
  }
  if (error.status === 403) {
    return 'Недостаточно прав';
  }
  return error.error?.detail ?? 'Что-то пошло не так';
}
