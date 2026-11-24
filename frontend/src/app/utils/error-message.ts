import { HttpErrorResponse } from '@angular/common/http';

export function errorMessage(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'Сервер недоступен';
  }
  if (error.error?.detail) {
    return error.error.detail;
  }
  if (error.status === 401) {
    return 'Нужно войти в систему';
  }
  if (error.status === 403) {
    return 'Недостаточно прав';
  }
  return 'Что-то пошло не так';
}
