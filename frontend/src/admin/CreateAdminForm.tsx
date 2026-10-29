import { useState } from 'react';
import type { FormEvent } from 'react';
import { api, ApiError } from '../auth/api';

export type CreatedAdmin = { id: string; name: string; email: string; emailVerified: boolean; role: 'Admin' };
type CreateAdminResponse = { message: string; user: CreatedAdmin };

export default function CreateAdminForm({ onCreated }: { onCreated: (user: CreatedAdmin) => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const values = new FormData(form);
    setBusy(true);
    setError('');
    setMessage('');
    try {
      const result = await api<CreateAdminResponse>('/api/admin/users', 'POST', {
        name: String(values.get('name') ?? ''),
        email: String(values.get('email') ?? ''),
        password: String(values.get('password') ?? ''),
        passwordConfirmation: String(values.get('passwordConfirmation') ?? ''),
      });
      onCreated(result.user);
      form.reset();
      setMessage(result.message);
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : 'The Admin could not be created. Please try again.');
    } finally {
      setBusy(false);
    }
  }

  return <section className="admin-create-card" aria-labelledby="create-admin-heading">
    <h2 id="create-admin-heading">Create Admin</h2>
    <p>Create an account with the existing Admin role. No invitation email is sent.</p>
    {error && <p className="auth-error" role="alert">{error}</p>}
    {message && <p className="auth-success" role="status">{message}</p>}
    <form onSubmit={event => void submit(event)}>
      <fieldset disabled={busy}>
        <div className="admin-create-grid">
          <label>Name<input name="name" type="text" maxLength={255} autoComplete="name" required /></label>
          <label>Email<input name="email" type="email" maxLength={255} autoComplete="email" required /></label>
          <label>Initial password<input name="password" type="password" minLength={8} maxLength={1024} autoComplete="new-password" required /></label>
          <label>Confirm password<input name="passwordConfirmation" type="password" minLength={8} maxLength={1024} autoComplete="new-password" required /></label>
        </div>
        <button className="admin-save" type="submit">{busy ? 'Creating…' : 'Create Admin'}</button>
      </fieldset>
    </form>
  </section>;
}
