import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';

const vite = await createServer({ server: { middlewareMode: true }, appType: 'custom' });
const { default: SectionCard } = await vite.ssrLoadModule('/src/components/sections/SectionCard.tsx');
const { default: SectionForm } = await vite.ssrLoadModule('/src/components/sections/SectionForm.tsx');
const { default: SectionIndex } = await vite.ssrLoadModule('/src/admin/sections/SectionIndex.tsx');
const { emptyDraft } = await vite.ssrLoadModule('/src/components/sections/sectionTypes.ts');
const { safeReturnPath } = await vite.ssrLoadModule('/src/auth/api.ts');
test.after(async () => vite.close());

test('Section card shows order, status, image and fitted action controls', () => {
  const html = renderToStaticMarkup(React.createElement(SectionCard, {
    item: { id: '7', pageId: '3', type: 'hero', sortOrder: 1, isEnabled: true, heading: 'Welcome',
      body: 'About the business', mediaUrl: '/api/media/4/file', imageAlt: 'Team', mediaId: '4' },
    index: 0, count: 2, pageId: '3', busy: false,
    onMove: () => {}, onToggle: () => {}, onDelete: () => {},
  }));
  assert.match(html, /Hero · Enabled/);
  assert.match(html, /alt="Team"/);
  assert.match(html, /Move Up/);
  assert.match(html, /Move Down/);
  assert.match(html, /Disable/);
  assert.match(html, /href="\/admin\/pages\/3\/sections\/7\/edit"/);
  assert.match(html, /Delete/);
});

test('Section form exposes only fields belonging to selected type', () => {
  const base = { creating: true, busy: false, errors: {}, pageId: '3',
    onChange: () => {}, onType: () => {}, onChooseImage: () => {}, onSave: () => {} };
  const text = renderToStaticMarkup(React.createElement(SectionForm, { ...base, draft: emptyDraft('text') }));
  assert.match(text, /Text Content/);
  assert.match(text, /Body/);
  assert.doesNotMatch(text, /Choose image/);
  assert.doesNotMatch(text, /Button URL/);
  const hero = renderToStaticMarkup(React.createElement(SectionForm, { ...base, draft: emptyDraft('hero') }));
  assert.match(hero, /Choose image/);
  assert.match(hero, /Button URL/);
  assert.match(hero, /Create Section/);
});

test('Section routes are safe return destinations and list has no unapproved search', () => {
  const previous = globalThis.window;
  globalThis.window = { location: { origin: 'http://localhost', search: '' } };
  try {
    for (const path of ['/admin/pages/3/sections', '/admin/pages/3/sections/create',
      '/admin/pages/3/sections/7/edit']) assert.equal(safeReturnPath(path), path);
    assert.equal(safeReturnPath('/admin/pages/evil/sections'), '/admin/dashboard');
    const html = renderToStaticMarkup(React.createElement(SectionIndex, { pageId: '3' }));
    assert.doesNotMatch(html, /Search sections/);
  } finally { globalThis.window = previous; }
});
