import { Sort } from '@angular/material/sort';

export function sortItems<T>(items: T[], sort: Sort): T[] {
  if (!sort.direction) {
    return items;
  }
  const key = sort.active as keyof T;
  const sign = sort.direction === 'asc' ? 1 : -1;
  return [...items].sort((a, b) => sign * compare(a[key], b[key]));
}

function compare(a: unknown, b: unknown): number {
  if (a === null || b === null) {
    return Number(a !== null) - Number(b !== null);
  }
  if (typeof a === 'string' && typeof b === 'string') {
    return a.localeCompare(b, 'ru');
  }
  return Number(a) - Number(b);
}
