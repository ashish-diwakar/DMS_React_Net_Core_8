import { useEffect, useRef } from 'react';
import type { SectionType } from './sectionTypes';
import { sectionTypeName } from './sectionTypes';

export default function TypeChangeDialog({ nextType, onConfirm, onClose }: {
  nextType: SectionType | null; onConfirm: () => void; onClose: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (nextType && !dialog.open) dialog.showModal();
    if (!nextType && dialog.open) dialog.close();
  }, [nextType]);
  return <dialog ref={ref} className="page-delete-dialog" aria-labelledby="section-type-change-heading" onClose={onClose}>
    <h2 id="section-type-change-heading">Change section type?</h2>
    <p>Switch to {nextType ? sectionTypeName(nextType) : 'another type'}? The information already entered will be cleared.</p>
    <div className="page-delete-actions">
      <button type="button" className="page-secondary-button" onClick={onClose}>Keep current type</button>
      <button type="button" className="page-primary-button" onClick={onConfirm}>Change type</button>
    </div>
  </dialog>;
}
