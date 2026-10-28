export class ApiError extends Error {
  readonly status: number;
  constructor(message: string, status: number) { super(message); this.status = status; }
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
    try {
      const error: unknown = await response.json();
      if (typeof error === 'object' && error !== null && 'message' in error && typeof error.message === 'string') message = error.message;
    } catch { /* A non-JSON error still has a safe fallback message. */ }
    throw new ApiError(message, response.status);
  }
  return response.json() as Promise<T>;
}

export function safeReturnPath(value: string | null): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) return '/admin/dashboard';
  const url = new URL(value, window.location.origin);
  return url.origin === window.location.origin && ['/admin/dashboard', '/confirm-password', '/change-password', '/verify-email'].includes(url.pathname)
    ? url.pathname + url.search : '/admin/dashboard';
}
