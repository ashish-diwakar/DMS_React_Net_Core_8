import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { api, ApiError } from '../../auth/api';
import PageTable from '../../components/pages/PageTable';
import PageDeleteDialog from '../../components/pages/PageDeleteDialog';
import type { PageItem, PageList } from '../../components/pages/pageTypes';

type FilterStatus = 'all' | 'draft' | 'published';
const initialParams = () => new URLSearchParams(window.location.search);
const initialStatus = (): FilterStatus => {
  const value = initialParams().get('status');
  return value === 'draft' || value === 'published' ? value : 'all';
};

export default function PageIndex() {
  const [query, setQuery] = useState(initialParams().get('search') ?? '');
  const [search, setSearch] = useState(initialParams().get('search') ?? '');
  const [draftStatus, setDraftStatus] = useState<FilterStatus>(initialStatus);
  const [status, setStatus] = useState<FilterStatus>(initialStatus);
  const [page, setPage] = useState(1);
  const [reload, setReload] = useState(0);
  const [data, setData] = useState<PageList | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState(initialParams().get('notice') === 'created' ? 'Page created successfully.'
    : initialParams().get('notice') === 'updated' ? 'Page updated successfully.' : '');
  const [selected, setSelected] = useState<PageItem | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState('');

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    const params = new URLSearchParams({ page: String(page), search, status });
    void api<PageList>('/api/admin/pages?' + params, 'GET', undefined, controller.signal)
      .then(result => { setData(result); setError(''); })
      .catch(problem => { if (!controller.signal.aborted) setError(problem instanceof ApiError ? problem.message : 'Pages could not be loaded.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, search, status, reload]);

  function apply(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setPage(1); setSearch(query.trim()); setStatus(draftStatus);
  }
  function reset() {
    setQuery(''); setSearch(''); setDraftStatus('all'); setStatus('all'); setPage(1);
  }
  async function remove() {
    if (!selected || deleting) return;
    setDeleting(true); setDeleteError('');
    try {
      const result = await api<{ message: string }>('/api/admin/pages/' + selected.id, 'DELETE');
      setSelected(null); setNotice(result.message); setReload(value => value + 1);
    } catch (problem) { setDeleteError(problem instanceof ApiError ? problem.message : 'The Page could not be deleted.'); }
    finally { setDeleting(false); }
  }

  return <div className="page-index">
    {notice && <p className="settings-notice settings-notice-success" role="status">{notice}</p>}
    {error && <p className="settings-notice settings-notice-error" role="alert">{error}</p>}
    <div className="page-create-row"><a className="page-primary-button" href="/admin/pages/create">Create page</a></div>
    <form className="page-filter-form" onSubmit={apply} aria-label="Filter pages">
      <label className="page-filter-field" htmlFor="page-search">Search title or slug
        <input id="page-search" type="search" maxLength={200} value={query} onChange={event => setQuery(event.target.value)} />
      </label>
      <label className="page-filter-field" htmlFor="page-filter-status">Status
        <select id="page-filter-status" value={draftStatus} onChange={event => setDraftStatus(event.target.value as FilterStatus)}>
          <option value="all">All</option><option value="draft">Draft</option><option value="published">Published</option>
        </select>
      </label>
      <div className="page-filter-actions">
        <button className="page-primary-button" type="submit">Apply</button>
        <button className="page-secondary-button" type="button" onClick={reset}>Reset</button>
      </div>
    </form>
    <section aria-label="Page results">
      {loading ? <p role="status">Loading pages…</p> : data && <PageTable items={data.items} onDelete={item => { setDeleteError(''); setSelected(item); }} />}
      {data && data.totalPages > 1 && <nav className="media-pages" aria-label="Page pagination">
        <button className="page-secondary-button" type="button" disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Previous</button>
        <span>Page {data.page} of {data.totalPages}</span>
        <button className="page-secondary-button" type="button" disabled={page >= data.totalPages} onClick={() => setPage(value => value + 1)}>Next</button>
      </nav>}
    </section>
    <PageDeleteDialog page={selected} busy={deleting} error={deleteError} onClose={() => { if (!deleting) setSelected(null); }} onConfirm={() => void remove()} />
  </div>;
}
