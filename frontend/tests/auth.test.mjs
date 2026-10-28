import assert from 'node:assert/strict';
import test from 'node:test';
// Node 24 loads this erasable TypeScript directly; no extra test framework.
import { api, ApiError, safeReturnPath } from '../src/auth/api.ts';

test('return paths stay on the approved local account pages', () => {
  const previous = globalThis.window;
  globalThis.window = { location: { origin: 'http://127.0.0.1:5178' } };
  try {
    for (const value of [null, 'https://evil.test', '//evil.test', '/\\evil.test', '/login', '/%2f%2fevil.test', '/admin/dashboard/../../login'])
      assert.equal(safeReturnPath(value), '/admin/dashboard');
    assert.equal(safeReturnPath('/change-password'), '/change-password');
    assert.equal(safeReturnPath('/verify-email?token=synthetic'), '/verify-email?token=synthetic');
  } finally { globalThis.window = previous; }
});

test('writes obtain fresh CSRF tokens and use same-origin cookies; rejected CSRF stops the write', async () => {
  const original = globalThis.fetch;
  let issued = 0;
  let writes = 0;
  globalThis.fetch = async (path, options) => {
    assert.equal(options.credentials, 'same-origin');
    if (path === '/api/auth/csrf') return Response.json({ token: 'synthetic-' + ++issued });
    writes++;
    assert.equal(options.headers['X-CSRF-TOKEN'], 'synthetic-' + issued);
    return Response.json({ message: 'Completed' });
  };
  try {
    await api('/api/auth/login', 'POST', {});
    await api('/api/auth/logout', 'POST', {});
    assert.equal(issued, 2);
    assert.equal(writes, 2);
    globalThis.fetch = async (path) => {
      assert.equal(path, '/api/auth/csrf');
      return Response.json({ message: 'Request could not be verified.' }, { status: 400 });
    };
    await assert.rejects(api('/api/auth/logout', 'POST', {}), error => error instanceof ApiError && error.status === 400);
    assert.equal(writes, 2);
  } finally { globalThis.fetch = original; }
});
