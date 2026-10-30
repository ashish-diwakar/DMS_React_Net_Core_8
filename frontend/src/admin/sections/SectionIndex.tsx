import { useEffect, useState } from 'react';
import { api, ApiError } from '../../auth/api';
import type { SectionItem, SectionList } from '../../components/sections/sectionTypes';
import SectionCard from '../../components/sections/SectionCard';
import SectionDeleteDialog from '../../components/sections/SectionDeleteDialog';
import { PageStatusBadge } from '../../components/pages/PageTable';

export default function SectionIndex({ pageId }: { pageId: string }) {
  const [data, setData] = useState<SectionList | null>(null);
  const [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const initialNotice = new URLSearchParams(window.location.search).get('notice');
  const [notice, setNotice] = useState(initialNotice === 'created' ? 'Section created successfully.'
    : initialNotice === 'updated' ? 'Section updated successfully.' : '');
  const [selected, setSelected] = useState<SectionItem | null>(null);
  const [deleteError, setDeleteError] = useState('');
  const base = '/api/admin/pages/' + pageId + '/sections';
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    void api<SectionList>(base, 'GET', undefined, controller.signal).then(setData)
      .catch(problem => { if (!controller.signal.aborted) setError(problem instanceof ApiError ? problem.message : 'Sections could not be loaded.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [base, reload]);
  async function change(id: string, action: 'move' | 'toggle', direction?: 'up' | 'down') {
    if (busy) return;
    setBusy(true); setError(''); setNotice('');
    try {
      const result = await api<{ message: string }>(base + '/' + id + '/' + action, 'POST', direction ? { direction } : {});
      setNotice(result.message); setReload(value => value + 1);
    } catch (problem) { setError(problem instanceof ApiError ? problem.message : 'The section could not be updated.'); }
    finally { setBusy(false); }
  }
  async function remove() {
    if (!selected || busy) return;
    setBusy(true); setDeleteError('');
    try {
      const result = await api<{ message: string }>(base + '/' + selected.id, 'DELETE');
      setSelected(null); setNotice(result.message); setReload(value => value + 1);
    } catch (problem) { setDeleteError(problem instanceof ApiError ? problem.message : 'The section could not be deleted.'); }
    finally { setBusy(false); }
  }
  return <div className="section-index">
    {notice && <p className="settings-notice settings-notice-success" role="status">{notice}</p>}
    {error && <p className="settings-notice settings-notice-error" role="alert">{error}</p>}
    {loading ? <p role="status">Loading sections…</p> : data && <>
      <div className="section-list-header">
        <div><h2>{data.page.title}</h2><p>Page status: <PageStatusBadge status={data.page.status} /></p></div>
        <div className="section-list-links">
          <a className="page-secondary-button" href={'/admin/pages/' + pageId + '/edit'}>Edit Page identity</a>
          <a className="page-secondary-button" href="/admin/pages">Pages</a>
          <a className="page-primary-button" href={'/admin/pages/' + pageId + '/sections/create'}>Add Section</a>
        </div>
      </div>
      {data.sections.length ? <div className="section-card-list">{data.sections.map((item, index) =>
        <SectionCard key={item.id} item={item} index={index} count={data.sections.length} pageId={pageId} busy={busy}
          onMove={(id, direction) => void change(id, 'move', direction)}
          onToggle={id => void change(id, 'toggle')} onDelete={section => { setDeleteError(''); setSelected(section); }} />)}</div>
        : <div className="section-empty">No sections yet. Add a section to arrange this Page’s content.</div>}
    </>}
    <SectionDeleteDialog section={selected} busy={busy} error={deleteError}
      onClose={() => { if (!busy) setSelected(null); }} onConfirm={() => void remove()} />
  </div>;
}
