import { useEffect, useRef } from 'react';
import type { PageItem } from './pageTypes';

export default function PageDeleteDialog({ page, busy, error, onClose, onConfirm }: {
  page: PageItem | null; busy: boolean; error: string; onClose: () => void; onConfirm: () => void;
}) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (page && !dialog.open) dialog.showModal();
    if (!page && dialog.open) dialog.close();
  }, [page]);
  return <dialog ref={dialogRef} className="page-delete-dialog" aria-labelledby="page-delete-heading"
    aria-describedby="page-delete-message" onClose={onClose}
    onCancel={event => { if (busy) event.preventDefault(); }}>
    <h2 id="page-delete-heading">Delete Page?</h2>
    <p id="page-delete-message">Are you sure you want to permanently delete “{page?.title}”? This action cannot be undone.</p>
    {error && <p className="settings-notice settings-notice-error" role="alert">{error}</p>}
    <div className="page-delete-actions">
      <button className="page-secondary-button" type="button" autoFocus disabled={busy} onClick={onClose}>Cancel</button>
      <button className="page-danger-button" type="button" disabled={busy || !page} onClick={onConfirm}>{busy ? 'Deleting…' : 'Delete Page'}</button>
    </div>
  </dialog>;
}
