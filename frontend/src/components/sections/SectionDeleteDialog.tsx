import { useEffect, useRef } from 'react';
import type { SectionItem } from './sectionTypes';

export default function SectionDeleteDialog({ section, busy, error, onClose, onConfirm }: {
  section: SectionItem | null; busy: boolean; error: string; onClose: () => void; onConfirm: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (section && !dialog.open) dialog.showModal();
    if (!section && dialog.open) dialog.close();
  }, [section]);
  return <dialog ref={ref} className="page-delete-dialog" aria-labelledby="section-delete-heading"
    onClose={onClose} onCancel={event => { if (busy) event.preventDefault(); }}>
    <h2 id="section-delete-heading">Delete Section?</h2>
    <p>Delete “{section?.heading}”? This does not delete its image.</p>
    {error && <p className="settings-notice settings-notice-error" role="alert">{error}</p>}
    <div className="page-delete-actions">
      <button className="page-secondary-button" type="button" disabled={busy} onClick={onClose}>Cancel</button>
      <button className="page-danger-button" type="button" disabled={busy || !section} onClick={onConfirm}>{busy ? 'Deleting…' : 'Delete Section'}</button>
    </div>
  </dialog>;
}
