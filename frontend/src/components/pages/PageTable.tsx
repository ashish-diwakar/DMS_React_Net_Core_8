import type { PageItem } from './pageTypes';

export function PageStatusBadge({ status }: { status: PageItem['status'] }) {
  return <span className={'page-status page-status-' + status}>{status === 'published' ? 'Published' : 'Draft'}</span>;
}

function updatedAt(value: string | null) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
  }).format(date);
}

export default function PageTable({ items, onDelete }: { items: PageItem[]; onDelete: (page: PageItem) => void }) {
  return <div className="page-table-wrap"><table className="page-table">
    <thead><tr><th scope="col">Title</th><th scope="col">Slug</th><th scope="col">Status</th><th scope="col">Updated</th><th scope="col">Actions</th></tr></thead>
    <tbody>{items.length ? items.map(page => <tr key={page.id}>
      <td data-label="Title" className="page-table-title">{page.title}</td>
      <td data-label="Slug" className="page-table-slug">{page.slug}</td>
      <td data-label="Status"><PageStatusBadge status={page.status} /></td>
      <td data-label="Updated">{updatedAt(page.updatedAt)}</td>
      <td data-label="Actions"><div className="page-row-actions">
        <a className="page-secondary-button" href={'/admin/pages/' + page.id + '/sections'}>Manage Sections<span className="sr-only"> {page.title}</span></a>
        <a className="page-secondary-button" href={'/admin/pages/' + page.id + '/edit'}>Edit<span className="sr-only"> {page.title}</span></a>
        <button className="page-delete-button" type="button" onClick={() => onDelete(page)}>Delete<span className="sr-only"> {page.title}</span></button>
      </div></td>
    </tr>) : <tr><td colSpan={5} className="page-table-empty">No pages found.</td></tr>}</tbody>
  </table></div>;
}
