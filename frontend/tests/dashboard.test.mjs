import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';

const vite = await createServer({ server: { middlewareMode: true }, appType: 'custom' });
const { default: DashboardSummaryCards, isDashboardSummary } = await vite.ssrLoadModule('/src/components/dashboard/DashboardSummary.tsx');
test.after(async () => { await vite.close(); });

const render = (summary, capabilities) => renderToStaticMarkup(React.createElement(DashboardSummaryCards, { summary, capabilities }));

test('dashboard response validation rejects malformed and cross-scope totals', () => {
  assert.equal(isDashboardSummary({ scope: 'global', userCount: 3, roleCount: 2, permissionCount: 8 }), true);
  assert.equal(isDashboardSummary({ scope: 'personal', primaryRole: 'Admin', effectivePermissionCount: 4 }), true);
  assert.equal(isDashboardSummary({ scope: 'personal', primaryRole: 'Admin', effectivePermissionCount: -1 }), false);
  assert.equal(isDashboardSummary({ scope: 'personal', primaryRole: 'Admin', effectivePermissionCount: 4, roleCount: 2 }), false);
  assert.equal(isDashboardSummary({ scope: 'global', userCount: null, roleCount: 2, permissionCount: 8 }), false);
});

test('Super Admin sees global cards and authorized links', () => {
  const html = render({ scope: 'global', userCount: 3, roleCount: 2, permissionCount: 8 }, { canViewUsers: true, canViewRoles: true });
  assert.match(html, />Users<\/p>/);
  assert.match(html, />Roles<\/p>/);
  assert.match(html, />Permissions<\/p>/);
  assert.match(html, /href="\/admin\/users"/);
  assert.match(html, /href="\/admin\/roles"/);
  assert.doesNotMatch(html, /My permissions/);
});

test('Admin sees personal cards and no unauthorized user count or link', () => {
  const html = render({ scope: 'personal', primaryRole: 'Admin', effectivePermissionCount: 4 }, { canViewUsers: false, canViewRoles: true });
  assert.match(html, /My role/);
  assert.match(html, /My permissions/);
  assert.match(html, /Effective web permissions for your account/);
  assert.doesNotMatch(html, /Total CMS users/);
  assert.doesNotMatch(html, /href="\/admin\/users"/);
  assert.match(html, /href="\/admin\/roles"/);
});

test('Admin user count appears only with users.view', () => {
  const summary = { scope: 'personal', primaryRole: 'Admin', effectivePermissionCount: 4, userCount: 3 };
  const visible = render(summary, { canViewUsers: true, canViewRoles: false });
  const hidden = render(summary, { canViewUsers: false, canViewRoles: false });
  assert.match(visible, /Total CMS users/);
  assert.match(visible, /href="\/admin\/users"/);
  assert.doesNotMatch(visible, /href="\/admin\/roles"/);
  assert.doesNotMatch(hidden, /Total CMS users/);
});
