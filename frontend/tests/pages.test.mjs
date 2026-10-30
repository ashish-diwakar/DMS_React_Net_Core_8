import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';

const vite = await createServer({ server: { middlewareMode: true }, appType: 'custom' });
const { default: PageForm } = await vite.ssrLoadModule('/src/components/pages/PageForm.tsx');
const { default: PageIndex } = await vite.ssrLoadModule('/src/admin/pages/PageIndex.tsx');
const { default: PageTable } = await vite.ssrLoadModule('/src/components/pages/PageTable.tsx');
const { default: PageDeleteDialog } = await vite.ssrLoadModule('/src/components/pages/PageDeleteDialog.tsx');
const { suggestSlug } = await vite.ssrLoadModule('/src/components/pages/pageTypes.ts');
const { safeReturnPath } = await vite.ssrLoadModule('/src/auth/api.ts');
test.after(async () => vite.close());

test('Page form exposes only identity fields, safe slug guidance and save actions', () => {
  const html = renderToStaticMarkup(React.createElement(PageForm, {
    creating: true, draft: { title: '', slug: '', status: 'draft' }, errors: {}, busy: false,
    onTitle: () => {}, onSlug: () => {}, onStatus: () => {}, onSubmit: () => {},
  }));
  assert.match(html, /name="title"/);
  assert.match(html, /name="slug"/);
  assert.match(html, /name="status"/);
  assert.match(html, /Suggested from the title/);
  assert.match(html, /Create page/);
  assert.match(html, /href="\/admin\/pages"/);
  assert.doesNotMatch(html, /name="body"/);
  assert.equal(suggestSlug('Crème Brûlée'), 'creme-brulee');
});

test('Page table displays the approved columns, badges and fitted Page actions', () => {
  const item = { id: '9', title: 'Contact Us', slug: 'contact-us', status: 'published',
    createdAt: '2026-10-16T19:27:00', updatedAt: '2026-10-16T19:27:00' };
  const html = renderToStaticMarkup(React.createElement(PageTable, { items: [item], onDelete: () => {} }));
  assert.match(html, /<th scope="col">Title<\/th>/);
  assert.match(html, /<th scope="col">Actions<\/th>/);
  assert.match(html, /Contact Us/);
  assert.match(html, /contact-us/);
  assert.match(html, /Published/);
  assert.match(html, /href="\/admin\/pages\/9\/edit"/);
  assert.match(html, /Delete/);
  assert.match(html, /Manage Sections/);
});

test('Delete dialog keeps selected title escaped and offers cancel and confirmation', () => {
  const html = renderToStaticMarkup(React.createElement(PageDeleteDialog, {
    page: { id: '2', title: '<unsafe>', slug: 'unsafe', status: 'draft',
      createdAt: null, updatedAt: null },
    busy: false, error: '', onClose: () => {}, onConfirm: () => {},
  }));
  assert.match(html, /<dialog/);
  assert.match(html, /&lt;unsafe&gt;/);
  assert.match(html, /Cancel/);
  assert.match(html, /Delete Page/);
});

test('safe return paths include only the new Pages index, create and numeric edit screens', () => {
  const previousWindow = globalThis.window;
  globalThis.window = { location: { origin: 'http://localhost' } };
  try {
    assert.equal(safeReturnPath('/admin/pages'), '/admin/pages');
    assert.equal(safeReturnPath('/admin/pages/create'), '/admin/pages/create');
    assert.equal(safeReturnPath('/admin/pages/9/edit'), '/admin/pages/9/edit');
    assert.equal(safeReturnPath('/admin/pages/evil/edit'), '/admin/dashboard');
    assert.equal(safeReturnPath('//example.com/admin/pages'), '/admin/dashboard');
  } finally { globalThis.window = previousWindow; }
});

test('Pages index matches the approved create, search and status controls', () => {
  const previousWindow = globalThis.window;
  globalThis.window = { location: { search: '', origin: 'http://localhost' } };
  try {
    const html = renderToStaticMarkup(React.createElement(PageIndex));
    assert.match(html, /href="\/admin\/pages\/create"/);
    assert.match(html, /Search title or slug/);
    assert.match(html, /<option value="all"/);
    assert.match(html, /<option value="draft"/);
    assert.match(html, /<option value="published"/);
    assert.match(html, /Apply/);
    assert.match(html, /Reset/);
  } finally { globalThis.window = previousWindow; }
});
