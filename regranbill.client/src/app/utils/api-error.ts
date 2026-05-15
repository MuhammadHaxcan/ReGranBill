/**
 * Extract a user-facing message from an API error.
 *
 * Backend wire format (from ExceptionHandlingMiddleware):
 *   { "statusCode": <int>, "message": "<text>" }
 *
 * Angular wraps this in an HttpErrorResponse where:
 *   - error.error    -> the parsed JSON body above (the SERVER message)
 *   - error.message  -> a generic Angular string like "Http failure response for /api/x: 409 Conflict"
 *   - error.statusText -> "Conflict", "Bad Request", etc.
 *
 * Always prefer the inner body. Fall back to the wrapper only if the body is empty
 * or unparseable, and even then skip Angular's generic "Http failure response..." prefix
 * which is never useful to a user.
 */
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
      if (message) return message;
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
    statusText?: unknown;
    title?: unknown;
    detail?: unknown;
  };

  // 1. Server response body (HttpErrorResponse.error) — the real message.
  const nestedError = readApiErrorMessage(candidate.error);
  if (nestedError) return nestedError;

  // 2. ProblemDetails-style validation envelope `{ errors: { field: [msgs] } }`.
  const nestedErrors = readApiErrorMessage(candidate.errors);
  if (nestedErrors) return nestedErrors;

  // 3. ProblemDetails `detail` / `title` fields.
  if (typeof candidate.detail === 'string' && candidate.detail.trim()) {
    return candidate.detail.trim();
  }
  if (typeof candidate.title === 'string' && candidate.title.trim()
      && candidate.title !== 'One or more validation errors occurred.') {
    return candidate.title.trim();
  }

  // 4. The candidate's own `message`, but skip Angular's generic HTTP-failure prefix.
  if (typeof candidate.message === 'string') {
    const trimmed = candidate.message.trim();
    if (trimmed && !trimmed.startsWith('Http failure response')) {
      return trimmed;
    }
  }

  // 5. Last-resort: `statusText` (e.g., "Conflict", "Bad Request"). Skip useless values.
  if (typeof candidate.statusText === 'string') {
    const trimmed = candidate.statusText.trim();
    if (trimmed && trimmed !== 'OK' && trimmed !== 'Unknown Error') {
      return trimmed;
    }
  }

  return null;
}
