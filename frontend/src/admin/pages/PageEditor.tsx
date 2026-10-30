import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { api, ApiError } from '../../auth/api';
import PageForm from '../../components/pages/PageForm';
import type { PageDraft, PageItem } from '../../components/pages/pageTypes';
import { suggestSlug } from '../../components/pages/pageTypes';

export default function PageEditor({ id }: { id?: string }) {
  const creating = !id;
  const [draft, setDraft] = useState<PageDraft>({ title: '', slug: '', status: 'draft' });
  const [manuallyEdited, setManuallyEdited] = useState(false);
  const [loading, setLoading] = useState(!creating);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!id) return;
    const controller = new AbortController();
    void api<PageItem>('/api/admin/pages/' + id, 'GET', undefined, controller.signal)
      .then(item => { setDraft({ title: item.title, slug: item.slug, status: item.status }); setManuallyEdited(true); })
      .catch(problem => { if (!controller.signal.aborted) setError(problem instanceof ApiError ? problem.message : 'The Page could not be loaded.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [id]);

  function titleChanged(value: string) {
    setDraft(current => ({ ...current, title: value, slug: creating && !manuallyEdited ? suggestSlug(value) : current.slug }));
  }
  function slugChanged(value: string) {
    setManuallyEdited(true); setDraft(current => ({ ...current, slug: value }));
  }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (busy) return;
    setBusy(true); setError(''); setErrors({});
    try {
      await api('/api/admin/pages' + (id ? '/' + id : ''), creating ? 'POST' : 'PATCH', draft);
      window.location.assign('/admin/pages?notice=' + (creating ? 'created' : 'updated'));
    } catch (problem) {
      if (problem instanceof ApiError) { setError(problem.message); setErrors(problem.fieldErrors); }
      else setError('The Page could not be saved. Please try again.');
    } finally { setBusy(false); }
  }

  if (loading) return <p role="status">Loading Page…</p>;
  if (error && !creating && draft.title === '') return <p className="settings-notice settings-notice-error" role="alert">{error}</p>;
  return <div className="page-editor">
    {error && <p className="settings-notice settings-notice-error" role="alert">{error}</p>}
    <PageForm draft={draft} creating={creating} errors={errors} busy={busy} onTitle={titleChanged} onSlug={slugChanged}
      onStatus={value => setDraft(current => ({ ...current, status: value }))} onSubmit={event => void save(event)} />
  </div>;
}
