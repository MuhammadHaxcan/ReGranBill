export function parseLocalDate(value: string | Date | null | undefined): Date {
  if (value instanceof Date) {
    return new Date(value.getFullYear(), value.getMonth(), value.getDate());
  }

  if (typeof value === 'string') {
    const match = value.trim().match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (match) {
      return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
    }
  }

  const parsed = value ? new Date(value) : new Date(NaN);
  if (Number.isNaN(parsed.getTime())) {
    return new Date(NaN);
  }

  return new Date(parsed.getFullYear(), parsed.getMonth(), parsed.getDate());
}

export function formatDateDdMmYyyy(value: string | Date | null | undefined): string {
  const date = parseLocalDate(value);
  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  }).format(date);
}

export function formatDateDisplay(value: string | Date | null | undefined): string {
  const date = parseLocalDate(value);
  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric'
  }).format(date);
}

export function toDateInputValue(value: string | Date | null | undefined): string {
  const date = parseLocalDate(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
