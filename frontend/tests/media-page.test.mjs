import assert from 'node:assert/strict';
import test from 'node:test';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { createServer } from 'vite';

const vite = await createServer({ server: { middlewareMode: true }, appType: 'custom' });
const { default: MediaPage } = await vite.ssrLoadModule('/src/admin/MediaPage.tsx');
const { default: MediaCard } = await vite.ssrLoadModule('/src/components/media/MediaCard.tsx');
test.after(async () => vite.close());

test('Media Library presents aligned upload, search, filters, and an accessible list', () => {
  const html = renderToStaticMarkup(React.createElement(MediaPage));
  assert.match(html, /Upload media/);
  assert.match(html, /Choose media files/);
  assert.match(html, /Browse media/);
  assert.match(html, /Uploaded files are public/);
  assert.match(html, /Search display names or filenames/);
  assert.match(html, /<select[^>]+id="media-type"/);
  assert.match(html, /<option value="all"[^>]*>All<\/option>/);
  assert.match(html, /Apply<\/button>/);
  assert.match(html, /Reset<\/button>/);
  assert.match(html, /Images/);
  assert.match(html, /Documents/);
});


test('media card matches compact metadata and collapsed edit layout', () => {
  const html = renderToStaticMarkup(React.createElement(MediaCard, {
    item: { id: '9', originalName: 'park.jpg', title: 'Park-Top 2', mimeType: 'image/jpeg', size: 1992294,
      width: 3840, height: 2160, createdAt: '2026-10-16T15:48:00', url: '/api/media/9/file' },
    updated: () => {}, deleted: () => {},
  }));
  assert.match(html, /Park-Top 2/);
  assert.match(html, /park.jpg/);
  assert.match(html, /image\/jpeg · 1\.9 MiB/);
  assert.match(html, /3840 × 2160 px/);
  assert.match(html, /Uploaded 16 Oct 2026/);
  assert.match(html, /<details[^>]*><summary>Edit display name<\/summary>/);
  assert.match(html, /Leave blank to use the original filename/);
  assert.match(html, /Save display name/);
  assert.match(html, /Cancel/);
  assert.match(html, /Open asset/);
  assert.match(html, /Delete/);
});
