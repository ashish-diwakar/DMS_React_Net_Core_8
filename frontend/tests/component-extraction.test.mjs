import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';

const vite = await createServer({ server: { middlewareMode: true }, appType: 'custom' });
const { default: RoleList } = await vite.ssrLoadModule('/src/components/roles/RoleList.tsx');
const { default: UserTable } = await vite.ssrLoadModule('/src/components/users/UserTable.tsx');
const { default: AuthForm } = await vite.ssrLoadModule('/src/components/auth/AuthForm.tsx');
const { default: DashboardAccountCard } = await vite.ssrLoadModule('/src/components/dashboard/DashboardAccountCard.tsx');
const { default: WebsiteSettingsForm } = await vite.ssrLoadModule('/src/components/settings/WebsiteSettingsForm.tsx');
test.after(async () => vite.close());

const render = (component, props) => renderToStaticMarkup(React.createElement(component, props));

test('roles retain editable and read-only permission views', () => {
  const props = {
    roles: [{ id: '1', name: 'Admin', permissions: ['admin.access'] }, { id: '2', name: 'Super Admin', permissions: [] }],
    managedPermissions: ['admin.access', 'users.view'],
    selected: { '1': ['admin.access'] }, busy: false, error: '', message: '',
    onSelectionChange: () => {}, onSavePermissions: () => {},
  };
  const editable = render(RoleList, { ...props, canManagePermissions: true });
  assert.match(editable, /permissions-grid/);
  assert.match(editable, /Save Permissions/);
  assert.match(editable, /Protected full-access role/);
  const readOnly = render(RoleList, { ...props, canManagePermissions: false });
  assert.match(readOnly, /Admin permissions \(read only\)/);
  assert.match(readOnly, /disabled=""[^>]*readOnly=""/);
  assert.doesNotMatch(readOnly, /Save Permissions/);
});

test('user role actions remain restricted to accounts with assignment permission', () => {
  const props = {
    users: [{ id: '3', name: 'Admin User', email: 'admin@example.test', emailVerified: false, role: 'Admin' }],
    roles: [{ id: '1', name: 'Admin' }], assignments: { '3': '1' },
    busy: false, error: '', message: '', onAssignmentChange: () => {}, onSaveRole: () => {},
  };
  assert.match(render(UserTable, { ...props, canAssignRoles: true }), /Assign role to Admin User/);
  const denied = render(UserTable, { ...props, canAssignRoles: false });
  assert.match(denied, /Role assignment unavailable/);
  assert.doesNotMatch(denied, /<select/);
});

test('auth and account forms retain essential actions and input limits', () => {
  const reset = render(AuthForm, { path: '/reset-password', resetEmail: 'admin@example.test', busy: false, submit: () => {} });
  assert.match(reset, /admin@example.test/);
  assert.match(reset, /passwordConfirmation/);
  assert.match(reset, /Reset password/);
  const card = render(DashboardAccountCard, { emailVerified: false, busy: false, signOut: () => {} });
  assert.match(card, /href="\/change-password"/);
  assert.match(card, /href="\/confirm-password"/);
  assert.match(card, /href="\/verify-email"/);
  assert.match(card, /Sign out/);
});

test('settings form retains grouped fields and validation messages', () => {
  const settings = Object.fromEntries(['siteName', 'tagline', 'publicEmail', 'primaryPhone', 'secondaryPhone', 'address',
    'facebookUrl', 'instagramUrl', 'linkedinUrl', 'youtubeUrl', 'xTwitterUrl', 'footerText'].map(name => [name, name === 'siteName' ? 'Example' : null]));
  const html = render(WebsiteSettingsForm, {
    settings, busy: false, fieldErrors: { publicEmail: 'Invalid email' }, error: '', message: '', noticeRef: { current: null }, update: () => {}, save: () => {},
  });
  assert.match(html, /General/);
  assert.match(html, /Contact/);
  assert.match(html, /Social links/);
  assert.match(html, /Footer/);
  assert.match(html, /aria-invalid="true"/);
  assert.match(html, /Invalid email/);
  assert.match(html, /Save Settings/);
});
