import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';

const vite = await createServer({ server: { middlewareMode: true }, appType: 'custom' });
const { default: AdminShell } = await vite.ssrLoadModule('/src/admin/AdminShell.tsx');

test.after(async () => { await vite.close(); });

function render(authz, path) {
  return renderToStaticMarkup(React.createElement(AdminShell, {
    user: { name: 'Sample Admin' }, authz, path,
    children: React.createElement('p', null, 'Page content'),
  }));
}

const base = {
  roles: ['Admin'], permissions: [], isSuperAdmin: false,
  canAccessAdmin: true, canViewUsers: false, canViewRoles: false, canManageSettings: false,
};

test('shell keeps identity, mobile controls, content and logout in one layout', () => {
  const html = render({ ...base, canViewUsers: true, canViewRoles: true }, '/admin/users');
  assert.match(html, /aria-label="Primary navigation"/);
  assert.match(html, /aria-controls="admin-sidebar"/);
  assert.match(html, /aria-label="Close navigation"/);
  assert.match(html, /aria-label="Account menu for Sample Admin"/);
  assert.match(html, /Sample Admin/);
  assert.match(html, /Page content/);
  assert.match(html, /href="\/admin\/users"[^>]*aria-current="page"/);
  assert.match(html, /href="\/admin\/roles"/);
});

test('navigation omits unavailable destinations and only marks the current route active', () => {
  const html = render(base, '/admin/dashboard');
  assert.match(html, /href="\/admin\/dashboard"[^>]*aria-current="page"/);
  assert.doesNotMatch(html, /href="\/admin\/users"/);
  assert.doesNotMatch(html, /href="\/admin\/roles"/);
  assert.doesNotMatch(html, /href="\/admin\/(settings|media|pages)"/);
  assert.doesNotMatch(html, /aria-current="page"[^>]*href="\/admin\/users"/);
});

test('Settings becomes an active link only for an authorized account', () => {
  const allowed = render({ ...base, canManageSettings: true }, '/admin/settings');
  assert.match(allowed, /href="\/admin\/settings"[^>]*aria-current="page"/);
  assert.doesNotMatch(allowed, /Settings<\/span>/);
  const denied = render(base, '/admin/settings');
  assert.doesNotMatch(denied, /href="\/admin\/settings"/);
});
