import { useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { api, ApiError } from '../auth/api';
import WebsiteSettingsForm, { isWebsiteSettings } from '../components/settings/WebsiteSettingsForm';
import type { WebsiteSettings } from '../components/settings/WebsiteSettingsForm';

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

  function update(name: keyof WebsiteSettings, value: string) {
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

  return <WebsiteSettingsForm settings={settings} busy={busy} fieldErrors={fieldErrors}
    error={error} message={message} noticeRef={noticeRef} update={update} save={save} />;
}
