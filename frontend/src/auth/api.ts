export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors: Record<string, string>;
  constructor(message: string, status: number, fieldErrors: Record<string, string> = {}) {
    super(message); this.status = status; this.fieldErrors = fieldErrors;
  }
}

export async function api<T>(path: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<T> {
  let token: string | undefined;
  if (method !== 'GET') token = (await api<{ token: string }>('/api/auth/csrf')).token;
  const response = await fetch(path, {
    method,
    credentials: 'same-origin',
    cache: 'no-store',
    headers: {
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...(token ? { 'X-CSRF-TOKEN': token } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
  });
  if (!response.ok) {
    let message = response.status === 429 ? 'Too many requests. Try again in a minute.' : 'The request could not be completed. Please try again.';
    const fieldErrors: Record<string, string> = {};
    try {
      const error: unknown = await response.json();
      if (typeof error === 'object' && error !== null && 'message' in error && typeof error.message === 'string') message = error.message;
      if (typeof error === 'object' && error !== null && 'errors' in error && typeof error.errors === 'object' && error.errors !== null) {
        for (const [field, detail] of Object.entries(error.errors))
          if (typeof detail === 'string') fieldErrors[field] = detail;
      }
    } catch { /* A non-JSON error still has a safe fallback message. */ }
    throw new ApiError(message, response.status, fieldErrors);
  }
  return response.json() as Promise<T>;
}

export async function apiForm<T>(path: string, form: FormData): Promise<T> {
  const token = (await api<{ token: string }>("/api/auth/csrf")).token;
  const response = await fetch(path, { method: "POST", credentials: "same-origin", cache: "no-store", headers: { "X-CSRF-TOKEN": token }, body: form });
  if (!response.ok) {
    let message = "The upload could not be completed.";
    try { const body = await response.json(); if (typeof body.message === "string") message = body.message; } catch { /* Keep a safe fallback. */ }
    throw new ApiError(message, response.status);
  }
  return response.json() as Promise<T>;
}

export function safeReturnPath(value: string | null): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) return '/admin/dashboard';
  const url = new URL(value, window.location.origin);
  return url.origin === window.location.origin && ['/admin/dashboard','/admin/users','/admin/roles','/admin/settings','/admin/media', '/confirm-password', '/change-password', '/verify-email'].includes(url.pathname)
    ? url.pathname + url.search : '/admin/dashboard';
}
