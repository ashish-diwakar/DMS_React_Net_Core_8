import { useEffect, useRef, useState } from 'react';
import MediaCard from '../components/media/MediaCard';
import MediaUploadForm from '../components/media/MediaUploadForm';
import MediaFilters from '../components/media/MediaFilters';
import type { MediaItem } from '../components/media/MediaCard';
import type { FormEvent } from 'react';
import { api, apiForm, ApiError } from '../auth/api';
import { mediaFailureText } from '../components/media/mediaFailureText';

type Page = { items: MediaItem[]; page: number; totalPages: number; total: number };
const listFailureText = (error: unknown) => error instanceof ApiError && error.status === 404
  ? 'Media Library is unavailable on the running API. Restart the .NET API and refresh this page.'
  : mediaFailureText(error);

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
    catch (problem) { setError(mediaFailureText(problem)); }
    finally { setBusy(false); }
  }
  return <div className="media-page">
    {notice && <p className="settings-notice settings-notice-success" role="status">{notice}</p>}
    {error && <p className="settings-notice settings-notice-error" role="alert">{error}</p>}
    <MediaUploadForm fileInput={fileInput} busy={busy} upload={upload} />
    <MediaFilters query={query} setQuery={setQuery} draftType={draftType} setDraftType={setDraftType} applyFilters={applyFilters} resetFilters={resetFilters} />
    <section className="media-results" aria-label="Browse media">
      {loading && <p role="status">Loading media…</p>}
      {!loading && data && (data.items.length
        ? <div className="media-grid">{data.items.map(item => <MediaCard key={item.id} item={item} updated={next => setData(current => current ? { ...current, items: current.items.map(existing => existing.id === next.id ? next : existing) } : current)} deleted={() => { setNotice('Media deleted.'); setReload(value => value + 1); }} />)}</div>
        : <p className="media-empty">No media found.</p>)}
      {data && data.totalPages > 1 && <nav className="media-pages" aria-label="Media pages"><button className="dashboard-action-link" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</button><span>Page {data.page} of {data.totalPages}</span><button className="dashboard-action-link" disabled={page >= data.totalPages} onClick={() => setPage(page + 1)}>Next</button></nav>}
    </section>
  </div>;
}

