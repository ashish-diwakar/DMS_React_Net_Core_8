import type { SectionItem } from './sectionTypes';
import { sectionTypeName } from './sectionTypes';

export default function SectionCard({ item, index, count, pageId, busy, onMove, onToggle, onDelete }: {
  item: SectionItem; index: number; count: number; pageId: string; busy: boolean;
  onMove: (id: string, direction: 'up' | 'down') => void; onToggle: (id: string) => void; onDelete: (item: SectionItem) => void;
}) {
  return <article className="section-card">
    <div className="section-card-content">
      {item.mediaUrl && <img className="section-card-image" src={item.mediaUrl} alt={item.imageAlt ?? ''} />}
      <div className="section-card-copy">
        <p className="section-card-meta">{item.sortOrder} · {sectionTypeName(item.type)} · {item.isEnabled ? 'Enabled' : 'Disabled'}</p>
        <h2>{item.heading}</h2>
        {item.body && <p className="section-card-summary">{item.body}</p>}
      </div>
    </div>
    <div className="section-card-actions">
      <button className="page-secondary-button" type="button" disabled={busy || index === 0} onClick={() => onMove(item.id, 'up')}>Move Up</button>
      <button className="page-secondary-button" type="button" disabled={busy || index === count - 1} onClick={() => onMove(item.id, 'down')}>Move Down</button>
      <button className="page-secondary-button" type="button" disabled={busy} onClick={() => onToggle(item.id)}>{item.isEnabled ? 'Disable' : 'Enable'}</button>
      <a className="page-secondary-button" href={'/admin/pages/' + pageId + '/sections/' + item.id + '/edit'}>Edit</a>
      <button className="page-delete-button" type="button" disabled={busy} onClick={() => onDelete(item)}>Delete</button>
    </div>
  </article>;
}
