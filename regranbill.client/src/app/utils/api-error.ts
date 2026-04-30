export function getApiErrorMessage(error: unknown, fallback: string): string {
  const message = readApiErrorMessage(error);
  return message ? message : fallback;
}

function readApiErrorMessage(error: unknown): string | null {
  if (typeof error === 'string') {
    const trimmed = error.trim();
    return trimmed || null;
  }

  if (Array.isArray(error)) {
    for (const item of error) {
      const message = readApiErrorMessage(item);
      if (message) {
        return message;
      }
    }

    return null;
  }

  if (!error || typeof error !== 'object') {
    return null;
  }

  const candidate = error as {
    message?: unknown;
    error?: unknown;
    errors?: unknown;
  };

  if (typeof candidate.message === 'string' && candidate.message.trim()) {
    return candidate.message.trim();
  }

  const nestedError = readApiErrorMessage(candidate.error);
  if (nestedError) {
    return nestedError;
  }

  return readApiErrorMessage(candidate.errors);
}
