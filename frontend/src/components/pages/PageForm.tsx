import type { PageDraft } from './pageTypes';

export default function PageForm({ draft, creating, errors, busy, onTitle, onSlug, onStatus, onSubmit }: {
  draft: PageDraft; creating: boolean; errors: Record<string, string>; busy: boolean;
  onTitle: (value: string) => void; onSlug: (value: string) => void;
  onStatus: (value: PageDraft['status']) => void; onSubmit: (event: React.FormEvent<HTMLFormElement>) => void;
}) {
  return <form className="page-form" onSubmit={onSubmit} noValidate>
    <p>This manages page identity only. Page content and public rendering will be available in later features.</p>
    <div className="page-field">
      <label htmlFor="page-title">Title</label>
      <input id="page-title" name="title" required maxLength={200} value={draft.title} aria-invalid={!!errors.title}
        aria-describedby={errors.title ? 'page-title-error' : undefined} onChange={event => onTitle(event.target.value)} />
      {errors.title && <span id="page-title-error" className="page-field-error" role="alert">{errors.title}</span>}
    </div>
    <div className="page-field">
      <label htmlFor="page-slug">Slug</label>
      <input id="page-slug" name="slug" maxLength={150} required={!creating} value={draft.slug}
        aria-invalid={!!errors.slug} aria-describedby={'page-slug-help' + (errors.slug ? ' page-slug-error' : '')}
        onChange={event => onSlug(event.target.value)} />
      {errors.slug && <span id="page-slug-error" className="page-field-error" role="alert">{errors.slug}</span>}
      <small id="page-slug-help">{creating
        ? 'Suggested from the title until you edit it. Leave blank to derive it when saving. The server normalizes the final slug.'
        : 'Changing the title does not change this slug. Changing the slug changes the eventual public URL; redirects are not created.'} Use a single segment, such as about-us, without slashes.</small>
    </div>
    <div className="page-field">
      <label htmlFor="page-status">Status</label>
      <select id="page-status" name="status" value={draft.status} aria-invalid={!!errors.status}
        aria-describedby={'page-status-help' + (errors.status ? ' page-status-error' : '')}
        onChange={event => onStatus(event.target.value as PageDraft['status'])}>
        <option value="draft">Draft</option><option value="published">Published</option>
      </select>
      {errors.status && <span id="page-status-error" className="page-field-error" role="alert">{errors.status}</span>}
      <small id="page-status-help">Published marks the page as eligible for future public rendering; it does not expose a public page yet.</small>
    </div>
    <div className="page-form-actions">
      <button className="page-primary-button" type="submit" disabled={busy}>{busy ? 'Saving…' : creating ? 'Create page' : 'Save changes'}</button>
      <a className="page-secondary-button" href="/admin/pages">Cancel</a>
    </div>
  </form>;
}
