import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { api, apiForm, ApiError } from '../auth/api';

type Item = { id: string; originalName: string; title: string | null; mimeType: string; size: number; width: number | null; height: number | null; createdAt: string; url: string };
type Page = { items: Item[]; page: number; totalPages: number; total: number };
const failureText = (error: unknown) => error instanceof ApiError ? error.message : 'The request could not be completed.';
const listFailureText = (error: unknown) => error instanceof ApiError && error.status === 404
  ? 'Media Library is unavailable on the running API. Restart the .NET API and refresh this page.'
  : failureText(error);

function fileSize(size: number) {
  return size >= 1048576 ? `${(size / 1048576).toFixed(1)} MiB`
    : size >= 1024 ? `${(size / 1024).toFixed(1)} KiB` : `${size} B`;
}

function uploadedAt(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : new Intl.DateTimeFormat('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
  }).format(date);
}

export function MediaCard({ item, updated, deleted }: { item: Item; updated: (item: Item) => void; deleted: () => void }) {
  const [title, setTitle] = useState(item.title ?? '');
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [previewFailed, setPreviewFailed] = useState(false);
  const editorRef = useRef<HTMLDetailsElement>(null);
  const date = uploadedAt(item.createdAt);
  async function save(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(''); setNotice('');
    try {
      const result = await api<{ item: Item }>('/api/admin/media/' + item.id, 'PATCH', { title });
      updated(result.item); setTitle(result.item.title ?? ''); setNotice('Display name updated.');
      if (editorRef.current) editorRef.current.open = false;
    } catch (problem) { setError(failureText(problem)); }
    finally { setBusy(false); }
  }
  function cancel() {
    setTitle(item.title ?? ''); setError(''); setNotice('');
    if (editorRef.current) editorRef.current.open = false;
  }
  async function remove() {
    if (!window.confirm(`Permanently delete “${item.title || item.originalName}”? This cannot be undone.`)) return;
    setBusy(true); setError('');
    try { await api('/api/admin/media/' + item.id, 'DELETE'); deleted(); }
    catch (problem) { setError(failureText(problem)); }
    finally { setBusy(false); }
  }
  return <article className="media-card">
    <div className="media-preview">{item.mimeType.startsWith('image/')
      ? previewFailed ? <span className="media-preview-failed" role="status">Image preview unavailable. Try Open asset.</span>
        : <img src={item.url} alt="" loading="lazy" onError={() => setPreviewFailed(true)} />
      : <div className="media-pdf-mark" aria-label="PDF document"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true"><path strokeLinecap="round" strokeLinejoin="round" d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6ZM14 2v6h6M8 13h8M8 17h6" /></svg><span>PDF document</span></div>}</div>
    <div className="media-card-body">
      <div className="media-card-meta">
        <h2>{item.title || item.originalName}</h2>
        {item.title && <p className="media-original">{item.originalName}</p>}
        <p>{item.mimeType} · {fileSize(item.size)}</p>
        {item.width !== null && item.height !== null && <p>{item.width} × {item.height} px</p>}
        {date && <p>Uploaded {date}</p>}
      </div>
      <details ref={editorRef} className="media-editor">
        <summary>Edit display name</summary>
        <form className="media-title-form" onSubmit={save} aria-busy={busy}>
          <label htmlFor={'title-' + item.id}>Display name</label>
          <input id={'title-' + item.id} value={title} maxLength={255} disabled={busy} onChange={event => setTitle(event.target.value)} />
          <p>Leave blank to use the original filename. The asset URL stays the same.</p>
          {error && <p className="media-card-error" role="alert">{error}</p>}
          <div className="media-title-actions"><button className="media-primary-button" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save display name'}</button><button className="media-reset-button" type="button" disabled={busy} onClick={cancel}>Cancel</button></div>
        </form>
      </details>
      {notice && <p className="media-card-success" role="status">{notice}</p>}
      {error && !editorRef.current?.open && <p className="media-card-error" role="alert">{error}</p>}
      <div className="media-card-actions"><a className="dashboard-action-link" href={item.url} target="_blank" rel="noopener noreferrer">Open asset<span className="sr-only">: {item.title || item.originalName}</span></a><button className="media-delete-button" type="button" disabled={busy} onClick={() => void remove()}>Delete<span className="sr-only"> {item.title || item.originalName}</span></button></div>
    </div>
  </article>;
}

export default function MediaPage() {
  const [data, setData] = useState<Page | null>(null);
  const [query, setQuery] = useState('');
  const [search, setSearch] = useState('');
  const [type, setType] = useState<'all' | 'images' | 'documents'>('all');
  const [draftType, setDraftType] = useState<'all' | 'images' | 'documents'>('all');
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const fileInput = useRef<HTMLInputElement>(null);
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError('');
    const params = new URLSearchParams({ page: String(page), type, search });
    void api<Page>('/api/admin/media?' + params, 'GET', undefined, controller.signal).then(value => { setData(value); setError(''); })
      .catch(problem => { if (!controller.signal.aborted) setError(listFailureText(problem)); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, type, search, reload]);
  function applyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPage(1);
    setSearch(query.trim());
    setType(draftType);
  }
  function resetFilters() {
    setQuery('');
    setDraftType('all');
    setSearch('');
    setType('all');
    setPage(1);
  }
  async function upload(event: FormEvent) {
    event.preventDefault(); const files = fileInput.current?.files;
    if (!files?.length || files.length > 10) { setError('Choose between 1 and 10 files.'); return; }
    const body = new FormData(); for (const file of files) body.append('files', file);
    setBusy(true); setError(''); setNotice('');
    try { const result = await apiForm<{ message: string }>('/api/admin/media', body); setNotice(result.message); if (fileInput.current) fileInput.current.value = ''; setPage(1); setReload(value => value + 1); }
    catch (problem) { setError(failureText(problem)); }
    finally { setBusy(false); }
  }
  return <div className="media-page">
    {notice && <p className="settings-notice settings-notice-success" role="status">{notice}</p>}
    {error && <p className="settings-notice settings-notice-error" role="alert">{error}</p>}
    <section className="media-toolbar" aria-labelledby="media-upload-heading">
      <h2 id="media-upload-heading">Upload media</h2>
      <p id="media-upload-help">Choose up to 10 files. JPEG, PNG or WebP up to 5 MiB each. PDF up to 10 MiB each. Uploaded files are public. Server request limits may restrict the total upload size.</p>
      <form className="media-upload-form" onSubmit={upload}>
        <input ref={fileInput} type="file" accept=".jpg,.jpeg,.png,.webp,.pdf" multiple required disabled={busy} aria-label="Choose media files" aria-describedby="media-upload-help" />
        <button className="media-primary-button" type="submit" disabled={busy}>{busy ? 'Uploading…' : 'Upload'}</button>
      </form>
    </section>
    <form className="media-filter-form" onSubmit={applyFilters} aria-label="Filter media">
      <label className="media-filter-field" htmlFor="media-search">Search display names or filenames
        <input id="media-search" type="search" maxLength={255} value={query} onChange={event => setQuery(event.target.value)} />
      </label>
      <label className="media-filter-field" htmlFor="media-type">Type
        <select id="media-type" value={draftType} onChange={event => setDraftType(event.target.value as 'all' | 'images' | 'documents')}>
          <option value="all">All</option><option value="images">Images</option><option value="documents">Documents</option>
        </select>
      </label>
      <div className="media-filter-actions">
        <button className="media-primary-button" type="submit">Apply</button>
        <button className="media-reset-button" type="button" onClick={resetFilters}>Reset</button>
      </div>
    </form>
    <section className="media-results" aria-label="Browse media">
      {loading && <p role="status">Loading media…</p>}
      {!loading && data && (data.items.length
        ? <div className="media-grid">{data.items.map(item => <MediaCard key={item.id} item={item} updated={next => setData(current => current ? { ...current, items: current.items.map(existing => existing.id === next.id ? next : existing) } : current)} deleted={() => { setNotice('Media deleted.'); setReload(value => value + 1); }} />)}</div>
        : <p className="media-empty">No media found.</p>)}
      {data && data.totalPages > 1 && <nav className="media-pages" aria-label="Media pages"><button className="dashboard-action-link" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</button><span>Page {data.page} of {data.totalPages}</span><button className="dashboard-action-link" disabled={page >= data.totalPages} onClick={() => setPage(page + 1)}>Next</button></nav>}
    </section>
  </div>;
}

