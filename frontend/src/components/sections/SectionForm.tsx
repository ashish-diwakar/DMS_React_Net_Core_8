import type { FormEvent } from 'react';
import type { SectionDraft, SectionType } from './sectionTypes';
import { sectionTypeName } from './sectionTypes';

export default function SectionForm({ draft, creating, busy, errors, pageId, onChange, onType, onChooseImage, onSave }: {
  draft: SectionDraft; creating: boolean; busy: boolean; errors: Record<string, string>; pageId: string;
  onChange: (change: Partial<SectionDraft>) => void; onType: (type: SectionType) => void;
  onChooseImage: () => void; onSave: (event: FormEvent<HTMLFormElement>) => void;
}) {
  const imageAllowed = draft.type !== 'text';
  return <form className="page-form section-form" onSubmit={onSave} noValidate>
    {creating && <div className="page-field">
      <label htmlFor="section-type">Section type</label>
      <select id="section-type" value={draft.type} onChange={event => onType(event.target.value as SectionType)}>
        <option value="hero">Hero</option><option value="text">Text Content</option><option value="image_text">Image with Text</option>
      </select>
      {errors.type && <span className="page-field-error" role="alert">{errors.type}</span>}
    </div>}
    {!creating && <p>Type: {sectionTypeName(draft.type)}. To use another type, create a different section.</p>}
    <div className="page-field"><label htmlFor="section-heading">Heading</label>
      <input id="section-heading" value={draft.heading} required maxLength={200} aria-invalid={!!errors.heading}
        onChange={event => onChange({ heading: event.target.value })} />
      {errors.heading && <span className="page-field-error" role="alert">{errors.heading}</span>}</div>
    <div className="page-field"><label htmlFor="section-body">Body {draft.type === 'hero' ? '(optional)' : ''}</label>
      <textarea id="section-body" value={draft.body} required={draft.type !== 'hero'}
        maxLength={draft.type === 'text' ? 10000 : draft.type === 'image_text' ? 5000 : 500}
        rows={draft.type === 'text' ? 10 : 5} aria-invalid={!!errors.body}
        onChange={event => onChange({ body: event.target.value })} />
      <small>Plain text only. HTML is not supported.</small>
      {errors.body && <span className="page-field-error" role="alert">{errors.body}</span>}</div>
    {imageAllowed && <div className="section-image-field">
      <div className="page-field"><label>Image {draft.type === 'image_text' ? '(required)' : '(optional)'}</label>
        <div className="section-image-actions">
          <button className="page-secondary-button" type="button" onClick={onChooseImage}>Choose image</button>
          {draft.mediaId && <button className="page-secondary-button" type="button"
            onClick={() => onChange({ mediaId: null, mediaName: null, mediaUrl: null, imageAlt: '' })}>Remove image</button>}
        </div>
        {draft.mediaId && <span>{draft.mediaName}</span>}
        {errors.mediaId && <span className="page-field-error" role="alert">{errors.mediaId}</span>}
      </div>
      {draft.mediaUrl && <img className="section-selected-image" src={draft.mediaUrl} alt="" />}
      {draft.mediaId && <div className="page-field"><label htmlFor="section-alt">Image description</label>
        <input id="section-alt" value={draft.imageAlt} required maxLength={255} aria-invalid={!!errors.imageAlt}
          onChange={event => onChange({ imageAlt: event.target.value })} />
        {errors.imageAlt && <span className="page-field-error" role="alert">{errors.imageAlt}</span>}</div>}
    </div>}
    {draft.type === 'hero' && <div className="section-cta-fields">
      <div className="page-field"><label htmlFor="section-button-label">Button label (optional)</label>
        <input id="section-button-label" maxLength={80} value={draft.buttonLabel} aria-invalid={!!errors.buttonLabel}
          onChange={event => onChange({ buttonLabel: event.target.value })} />
        {errors.buttonLabel && <span className="page-field-error" role="alert">{errors.buttonLabel}</span>}</div>
      <div className="page-field"><label htmlFor="section-button-url">Button URL (optional)</label>
        <input id="section-button-url" maxLength={2048} value={draft.buttonUrl} aria-invalid={!!errors.buttonUrl}
          onChange={event => onChange({ buttonUrl: event.target.value })} />
        {errors.buttonUrl && <span className="page-field-error" role="alert">{errors.buttonUrl}</span>}</div>
      <p className="section-cta-help">Use a root-relative path or HTTP/HTTPS URL. Provide both button fields together.</p>
    </div>}
    <label className="section-enabled"><input type="checkbox" checked={draft.isEnabled}
      onChange={event => onChange({ isEnabled: event.target.checked })} /> Enabled</label>
    <div className="page-form-actions">
      <button className="page-primary-button" type="submit" disabled={busy}>{busy ? 'Saving…' : creating ? 'Create Section' : 'Save changes'}</button>
      <a className="page-secondary-button" href={'/admin/pages/' + pageId + '/sections'}>Cancel</a>
    </div>
  </form>;
}
