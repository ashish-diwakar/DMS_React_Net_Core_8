import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { api, ApiError } from '../auth/api';

export type WebsiteSettings = {
  siteName: string;
  tagline: string | null;
  publicEmail: string | null;
  primaryPhone: string | null;
  secondaryPhone: string | null;
  address: string | null;
  facebookUrl: string | null;
  instagramUrl: string | null;
  linkedinUrl: string | null;
  youtubeUrl: string | null;
  xTwitterUrl: string | null;
  footerText: string | null;
};

type FieldName = keyof WebsiteSettings;
type Field = { name: FieldName; label: string; maxLength: number; type?: 'email' | 'url'; multiline?: boolean };
type Section = { heading: string; fields: Field[] };

const sections: Section[] = [
  { heading: 'General', fields: [
    { name: 'siteName', label: 'Site name', maxLength: 120 },
    { name: 'tagline', label: 'Tagline', maxLength: 180 },
  ] },
  { heading: 'Contact', fields: [
    { name: 'publicEmail', label: 'Public email', maxLength: 255, type: 'email' },
    { name: 'primaryPhone', label: 'Primary phone', maxLength: 50 },
    { name: 'secondaryPhone', label: 'Secondary phone', maxLength: 50 },
    { name: 'address', label: 'Address', maxLength: 1000, multiline: true },
  ] },
  { heading: 'Social links', fields: [
    { name: 'facebookUrl', label: 'Facebook URL', maxLength: 2048, type: 'url' },
    { name: 'instagramUrl', label: 'Instagram URL', maxLength: 2048, type: 'url' },
    { name: 'linkedinUrl', label: 'LinkedIn URL', maxLength: 2048, type: 'url' },
    { name: 'youtubeUrl', label: 'YouTube URL', maxLength: 2048, type: 'url' },
    { name: 'xTwitterUrl', label: 'X/Twitter URL', maxLength: 2048, type: 'url' },
  ] },
  { heading: 'Footer', fields: [
    { name: 'footerText', label: 'Footer text', maxLength: 500 },
  ] },
];

const names = sections.flatMap(section => section.fields.map(field => field.name));
export function isWebsiteSettings(value: unknown): value is WebsiteSettings {
  if (!value || typeof value !== 'object') return false;
  const record = value as Record<string, unknown>;
  return typeof record.siteName === 'string'
    && names.every(name => name === 'siteName' || record[name] === null || typeof record[name] === 'string');
}

export default function WebsiteSettingsPage() {
  const [settings, setSettings] = useState<WebsiteSettings | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const noticeRef = useRef<HTMLParagraphElement>(null);
  useEffect(() => {
    if (message || error) noticeRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [message, error]);

  useEffect(() => {
    const controller = new AbortController();
    void api<unknown>('/api/admin/settings', 'GET', undefined, controller.signal)
      .then(value => {
        if (!isWebsiteSettings(value)) throw new Error('Invalid website settings');
        if (!controller.signal.aborted) setSettings(value);
      })
      .catch((failure: unknown) => {
        if (!controller.signal.aborted)
          setError(failure instanceof ApiError ? failure.message : 'Website settings are unavailable. Refresh the page to try again.');
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, []);

  function update(name: FieldName, value: string) {
    setSettings(current => current ? { ...current, [name]: value } : current);
    setFieldErrors(current => {
      if (!current[name]) return current;
      const next = { ...current };
      delete next[name];
      return next;
    });
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!settings || busy) return;
    setBusy(true);
    setError('');
    setMessage('');
    setFieldErrors({});
    try {
      const result = await api<{ message: string; settings: unknown }>('/api/admin/settings', 'PATCH', settings);
      if (!isWebsiteSettings(result.settings)) throw new Error('Invalid website settings');
      setSettings(result.settings);
      setMessage(result.message);
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : 'Website settings could not be saved. Please try again.');
      if (failure instanceof ApiError) setFieldErrors(failure.fieldErrors);
    } finally {
      setBusy(false);
    }
  }

  if (loading) return <p role="status">Loading website settings…</p>;
  if (!settings) return <p className="auth-error" role="alert">{error || 'Website settings are unavailable.'}</p>;

  return <form className="settings-form" onSubmit={event => void save(event)}>
    {error && <p ref={noticeRef} className="settings-notice settings-notice-error" role="alert">{error}</p>}
    {message && <p ref={noticeRef} className="settings-notice settings-notice-success" role="status">{message}</p>}
    <fieldset disabled={busy}>
      {sections.map(section => <section className="settings-section" key={section.heading}>
        <h2>{section.heading}</h2>
        <div className="settings-grid">
          {section.fields.map(field => <label className={'settings-field' + (field.multiline ? ' settings-field-wide' : '')} key={field.name}>
            {field.label}
            {field.multiline
              ? <textarea name={field.name} rows={4} maxLength={field.maxLength}
                  value={settings[field.name] ?? ''} onChange={event => update(field.name, event.target.value)}
                  aria-invalid={Boolean(fieldErrors[field.name])} aria-describedby={fieldErrors[field.name] ? field.name + '-error' : undefined} />
              : <input name={field.name} type={field.type ?? 'text'} maxLength={field.maxLength}
                  required={field.name === 'siteName'} value={settings[field.name] ?? ''}
                  onChange={event => update(field.name, event.target.value)}
                  aria-invalid={Boolean(fieldErrors[field.name])} aria-describedby={fieldErrors[field.name] ? field.name + '-error' : undefined} />}
            {fieldErrors[field.name] && <span id={field.name + '-error'} className="settings-field-error">{fieldErrors[field.name]}</span>}
          </label>)}
        </div>
      </section>)}
      <button className="admin-save settings-save" type="submit">{busy ? 'Saving…' : 'Save Settings'}</button>
    </fieldset>
  </form>;
}
