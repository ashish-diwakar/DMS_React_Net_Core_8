import type { FormEvent } from 'react';

export default function AuthForm({ path, resetEmail, busy, submit }: {
  path: string;
  resetEmail: string;
  busy: boolean;
  submit: (event: FormEvent<HTMLFormElement>) => void;
}) {
  const passwordField = (name: string, label: string, current = false) => (
    <label className="auth-field" key={name}>{label}
      <input name={name} type="password" required autoComplete={current ? 'current-password' : 'new-password'} minLength={current ? undefined : 8} />
    </label>
  );

  return <form onSubmit={submit} aria-busy={busy}>
    <fieldset disabled={busy}>
      {['/login', '/forgot-password', '/reset-password'].includes(path) && <label className="auth-field">Email
        <input name="email" type="email" maxLength={255} required autoComplete="username" defaultValue={path === '/reset-password' ? resetEmail : ''} />
      </label>}
      {path === '/change-password' && passwordField('currentPassword', 'Current password', true)}
      {path !== '/forgot-password' && passwordField('password', ['/reset-password', '/change-password'].includes(path) ? 'New password' : 'Password', ['/login', '/confirm-password'].includes(path))}
      {['/reset-password', '/change-password'].includes(path) && <>
        <p className="auth-hint">Use at least eight characters.</p>
        {passwordField('passwordConfirmation', 'Confirm new password')}
      </>}
      {path === '/login' && <div className="auth-options"><label><input type="checkbox" name="remember" /> Remember me</label><a href="/forgot-password">Forgot password?</a></div>}
      <button className="auth-button" type="submit">{busy ? 'Please wait…' : ({ '/login': 'Sign in', '/forgot-password': 'Send reset link', '/reset-password': 'Reset password', '/confirm-password': 'Confirm password', '/change-password': 'Change password' }[path])}</button>
    </fieldset>
    {path === '/forgot-password' && <p className="auth-hint">Enter your account email to request a reset link.</p>}
  </form>;
}
