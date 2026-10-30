import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { api } from '../../auth/api';
import { mediaFailureText } from './mediaFailureText';

export type MediaItem = { id: string; originalName: string; title: string | null; mimeType: string; size: number; width: number | null; height: number | null; createdAt: string; url: string };
function fileSize(size: number) {
  return size >= 1048576 ? `${(size / 1048576).toFixed(1)} MiB`
    : size >= 1024 ? `${(size / 1024).toFixed(1)} KiB` : `${size} B`;
}

function uploadedAt(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : new Intl.DateTimeFormat('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
  }).format(date);
}

export default function MediaCard({ item, updated, deleted }: { item: MediaItem; updated: (item: MediaItem) => void; deleted: () => void }) {
  const [title, setTitle] = useState(item.title ?? '');
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [previewFailed, setPreviewFailed] = useState(false);
  const editorRef = useRef<HTMLDetailsElement>(null);
  const date = uploadedAt(item.createdAt);
  async function save(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(''); setNotice('');
    try {
      const result = await api<{ item: MediaItem }>('/api/admin/media/' + item.id, 'PATCH', { title });
      updated(result.item); setTitle(result.item.title ?? ''); setNotice('Display name updated.');
      if (editorRef.current) editorRef.current.open = false;
    } catch (problem) { setError(mediaFailureText(problem)); }
    finally { setBusy(false); }
  }
  function cancel() {
    setTitle(item.title ?? ''); setError(''); setNotice('');
    if (editorRef.current) editorRef.current.open = false;
  }
  async function remove() {
    if (!window.confirm(`Permanently delete “${item.title || item.originalName}”? This cannot be undone.`)) return;
    setBusy(true); setError('');
    try { await api('/api/admin/media/' + item.id, 'DELETE'); deleted(); }
    catch (problem) { setError(mediaFailureText(problem)); }
    finally { setBusy(false); }
  }
  return <article className="media-card">
    <div className="media-preview">{item.mimeType.startsWith('image/')
      ? previewFailed ? <span className="media-preview-failed" role="status">Image preview unavailable. Try Open asset.</span>
        : <img src={item.url} alt="" loading="lazy" onError={() => setPreviewFailed(true)} />
      : <div className="media-pdf-mark" aria-label="PDF document"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true"><path strokeLinecap="round" strokeLinejoin="round" d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6ZM14 2v6h6M8 13h8M8 17h6" /></svg><span>PDF document</span></div>}</div>
    <div className="media-card-body">
      <div className="media-card-meta">
        <h2>{item.title || item.originalName}</h2>
        {item.title && <p className="media-original">{item.originalName}</p>}
        <p>{item.mimeType} · {fileSize(item.size)}</p>
        {item.width !== null && item.height !== null && <p>{item.width} × {item.height} px</p>}
        {date && <p>Uploaded {date}</p>}
      </div>
      <details ref={editorRef} className="media-editor">
        <summary>Edit display name</summary>
        <form className="media-title-form" onSubmit={save} aria-busy={busy}>
          <label htmlFor={'title-' + item.id}>Display name</label>
          <input id={'title-' + item.id} value={title} maxLength={255} disabled={busy} onChange={event => setTitle(event.target.value)} />
          <p>Leave blank to use the original filename. The asset URL stays the same.</p>
          {error && <p className="media-card-error" role="alert">{error}</p>}
          <div className="media-title-actions"><button className="media-primary-button" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save display name'}</button><button className="media-reset-button" type="button" disabled={busy} onClick={cancel}>Cancel</button></div>
        </form>
      </details>
      {notice && <p className="media-card-success" role="status">{notice}</p>}
      {error && !editorRef.current?.open && <p className="media-card-error" role="alert">{error}</p>}
      <div className="media-card-actions"><a className="dashboard-action-link" href={item.url} target="_blank" rel="noopener noreferrer">Open asset<span className="sr-only">: {item.title || item.originalName}</span></a><button className="media-delete-button" type="button" disabled={busy} onClick={() => void remove()}>Delete<span className="sr-only"> {item.title || item.originalName}</span></button></div>
    </div>
  </article>;
}

