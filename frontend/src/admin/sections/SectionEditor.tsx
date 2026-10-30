import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { api, ApiError } from '../../auth/api';
import SectionForm from '../../components/sections/SectionForm';
import ImagePicker from '../../components/sections/ImagePicker';
import type { PickerImage } from '../../components/sections/ImagePicker';
import TypeChangeDialog from '../../components/sections/TypeChangeDialog';
import type { SectionDraft, SectionItem, SectionType } from '../../components/sections/sectionTypes';
import { emptyDraft } from '../../components/sections/sectionTypes';

export default function SectionEditor({ pageId, sectionId }: { pageId: string; sectionId?: string }) {
  const creating = !sectionId;
  const [draft, setDraft] = useState<SectionDraft>(emptyDraft);
  const [loading, setLoading] = useState(true);
  const [pageName, setPageName] = useState('');
  const [busy, setBusy] = useState(false);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [nextType, setNextType] = useState<SectionType | null>(null);
  const [error, setError] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [dirty, setDirty] = useState(false);
  const base = '/api/admin/pages/' + pageId + '/sections';
  useEffect(() => {
    if (!sectionId) {
      const controller = new AbortController();
      void api<{ title: string }>('/api/admin/pages/' + pageId, 'GET', undefined, controller.signal)
        .then(page => setPageName(page.title))
        .catch(problem => { if (!controller.signal.aborted) setError(problem instanceof ApiError ? problem.message : 'The Page could not be loaded.'); })
        .finally(() => { if (!controller.signal.aborted) setLoading(false); });
      return () => controller.abort();
    }
    const controller = new AbortController();
    void api<SectionItem>(base + '/' + sectionId, 'GET', undefined, controller.signal)
      .then(item => setDraft({ type: item.type, heading: item.heading, body: item.body ?? '', isEnabled: item.isEnabled,
        mediaId: item.mediaId, mediaName: item.mediaName, mediaUrl: item.mediaUrl,
        imageAlt: item.imageAlt ?? '', buttonLabel: item.buttonLabel ?? '', buttonUrl: item.buttonUrl ?? '' }))
      .catch(problem => { if (!controller.signal.aborted) setError(problem instanceof ApiError ? problem.message : 'The section could not be loaded.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [base, sectionId]);
  function update(change: Partial<SectionDraft>) { setDraft(current => ({ ...current, ...change })); setDirty(true); }
  function changeType(type: SectionType) {
    if (type === draft.type) return;
    if (dirty) setNextType(type);
    else { setDraft(emptyDraft(type)); setErrors({}); }
  }
  function selectImage(image: PickerImage) {
    update({ mediaId: image.id, mediaName: image.name, mediaUrl: image.url });
    setPickerOpen(false);
  }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (busy) return;
    setBusy(true); setError(''); setErrors({});
    const payload: Record<string, unknown> = {
      heading: draft.heading, body: draft.body || null, isEnabled: draft.isEnabled,
    };
    if (creating) payload.type = draft.type;
    if (draft.type !== 'text') {
      payload.mediaId = draft.mediaId;
      payload.imageAlt = draft.mediaId ? draft.imageAlt : null;
    }
    if (draft.type === 'hero') {
      payload.buttonLabel = draft.buttonLabel || null;
      payload.buttonUrl = draft.buttonUrl || null;
    }
    try {
      await api(base + (sectionId ? '/' + sectionId : ''), creating ? 'POST' : 'PATCH', payload);
      window.location.assign('/admin/pages/' + pageId + '/sections?notice=' + (creating ? 'created' : 'updated'));
    } catch (problem) {
      if (problem instanceof ApiError) { setError(problem.message); setErrors(problem.fieldErrors); }
      else setError('The section could not be saved. Please try again.');
    } finally { setBusy(false); }
  }
  if (loading) return <p role="status">Loading section…</p>;
  if (error && draft.heading === '' && (!creating || !pageName)) return <p className="settings-notice settings-notice-error" role="alert">{error}</p>;
  return <div className="section-editor">
    {error && <p className="settings-notice settings-notice-error" role="alert">{error}</p>}
    {pageName && <p className="section-editor-context">Page: {pageName}</p>}
    <SectionForm draft={draft} creating={creating} busy={busy} errors={errors} pageId={pageId}
      onChange={update} onType={changeType} onChooseImage={() => setPickerOpen(true)} onSave={event => void save(event)} />
    <ImagePicker pageId={pageId} open={pickerOpen} onSelect={selectImage} onClose={() => setPickerOpen(false)} />
    <TypeChangeDialog nextType={nextType} onClose={() => setNextType(null)}
      onConfirm={() => { if (nextType) setDraft(emptyDraft(nextType)); setNextType(null); setDirty(false); setErrors({}); }} />
  </div>;
}
