import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { api, ApiError } from '../../auth/api';

export type PickerImage = { id: string; name: string; url: string };
type PickerPage = { items: PickerImage[]; page: number; totalPages: number; total: number };

export default function ImagePicker({ pageId, open, onSelect, onClose }: {
  pageId: string; open: boolean; onSelect: (image: PickerImage) => void; onClose: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const [query, setQuery] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PickerPage | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal();
    if (!open && dialog.open) dialog.close();
  }, [open]);
  useEffect(() => {
    if (!open) return;
    const controller = new AbortController();
    setLoading(true); setError('');
    const params = new URLSearchParams({ page: String(page), search });
    void api<PickerPage>('/api/admin/pages/' + pageId + '/sections/images?' + params, 'GET', undefined, controller.signal)
      .then(setData)
      .catch(problem => { if (!controller.signal.aborted) setError(problem instanceof ApiError ? problem.message : 'Images could not be loaded.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [open, page, search, pageId]);
  function apply(event: FormEvent) { event.preventDefault(); setPage(1); setSearch(query.trim()); }
  return <dialog ref={ref} className="section-picker page-delete-dialog" aria-labelledby="section-picker-heading"
    onClose={onClose}>
    <div className="section-picker-header">
      <h2 id="section-picker-heading">Choose an image</h2>
      <button className="page-secondary-button" type="button" onClick={onClose}>Close</button>
    </div>
    <form className="section-picker-search" onSubmit={apply}>
      <label className="page-field">Search display names or filenames
        <input type="search" maxLength={255} value={query} onChange={event => setQuery(event.target.value)} /></label>
      <button className="page-primary-button" type="submit">Search</button>
    </form>
    {error && <p className="settings-notice settings-notice-error" role="alert">{error}</p>}
    {loading ? <p role="status">Loading images…</p> : data && <>
      <div className="section-picker-grid">
        {data.items.map(image => <button className="section-picker-item" type="button" key={image.id} onClick={() => onSelect(image)}>
          <img src={image.url} alt="" loading="lazy" />
          <span>{image.name}</span>
        </button>)}
      </div>
      {data.total === 0 && <p>No images found.</p>}
      {data.totalPages > 1 && <nav className="media-pages" aria-label="Image pages">
        <button className="page-secondary-button" type="button" disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Previous</button>
        <span>Page {data.page} of {data.totalPages}</span>
        <button className="page-secondary-button" type="button" disabled={page >= data.totalPages} onClick={() => setPage(value => value + 1)}>Next</button>
      </nav>}
    </>}
  </dialog>;
}
