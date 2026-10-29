import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { api, ApiError, safeReturnPath } from './api';
import AdminPage from '../admin/AdminPage';
import AdminShell from '../admin/AdminShell';
import AdminPageHeading from '../admin/AdminPageHeading';
import DashboardSummaryCards, { isDashboardSummary } from '../admin/DashboardSummary';
import type { DashboardSummary } from '../admin/DashboardSummary';

type User = { id: string; name: string; email: string; emailVerified: boolean; passwordConfirmed: boolean };
type Authz = { roles:string[]; permissions:string[]; isSuperAdmin:boolean; canAccessAdmin:boolean; canViewUsers:boolean; canAssignRoles:boolean; canViewRoles:boolean; canManagePermissions:boolean };
const titles: Record<string, string> = {
  '/login': 'Welcome back.', '/forgot-password': 'Forgot your password?', '/reset-password': 'Choose a new password.',
  '/confirm-password': 'Confirm it’s you.', '/change-password': 'Change your password.', '/verify-email': 'Verify your email.',
  '/admin/dashboard': 'Dashboard', '/admin/users':'Users', '/admin/roles':'Roles and permissions',
};
const protectedPaths = ['/confirm-password', '/change-password', '/verify-email', '/admin/dashboard','/admin/users','/admin/roles'];

export default function AuthApp() {
  const path = window.location.pathname;
  const query = new URLSearchParams(window.location.search);
  const [user, setUser] = useState<User | null>(null);
  const [authz,setAuthz]=useState<Authz|null>(null);
  const [dashboard,setDashboard]=useState<DashboardSummary|null>(null);
  const [loading, setLoading] = useState(protectedPaths.includes(path));
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const known = path in titles;

  useEffect(() => {
    document.title = `${titles[path] ?? 'Page unavailable'} | Diwakar Enterprise CMS`;
    if (!protectedPaths.includes(path)) return;
    const controller = new AbortController();
    let dashboardRequested = false;
    void api<User>('/api/auth/me', 'GET', undefined, controller.signal).then(async value => {
      setUser(value);
      if (path.startsWith('/admin/')) {
        const state=await api<Authz>('/api/auth/authorization','GET',undefined,controller.signal);setAuthz(state);
        if (path === '/admin/dashboard' && state.canAccessAdmin) {
          dashboardRequested = true;
          const summary = await api<unknown>('/api/admin/dashboard', 'GET', undefined, controller.signal);
          if (!isDashboardSummary(summary) || (summary.scope === 'global') !== state.isSuperAdmin) {
            throw new Error('Invalid dashboard summary');
          }
          setDashboard(summary);
        }
      }
    }).catch((failure: unknown) => {
      if (controller.signal.aborted) return;
      if (failure instanceof ApiError && failure.status === 401) {
        window.location.replace('/login?returnUrl=' + encodeURIComponent(safeReturnPath(window.location.pathname + window.location.search)));
      } else setError(dashboardRequested ? 'Dashboard data is unavailable. Refresh the page to try again.' : 'Your account could not be loaded. Refresh the page to try again.');
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [path]);

  async function perform(action: () => Promise<void>) {
    setBusy(true); setMessage(''); setError('');
    try { await action(); }
    catch (failure) {
      if (failure instanceof ApiError && failure.status === 401 && protectedPaths.includes(path)) {
        window.location.replace('/login?returnUrl=' + encodeURIComponent(safeReturnPath(path + window.location.search)));
      } else setError(failure instanceof Error && failure instanceof ApiError ? failure.message : 'The service could not be reached. Please try again.');
    } finally { setBusy(false); }
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const value = (name: string) => String(data.get(name) ?? '');
    void perform(async () => {
      if (path === '/login') {
        await api('/api/auth/login', 'POST', { email: value('email'), password: value('password'), remember: data.has('remember') });
        window.location.assign(safeReturnPath(query.get('returnUrl')));
      } else if (path === '/forgot-password') {
        const result = await api<{ message: string }>('/api/auth/forgot-password', 'POST', { email: value('email') });
        setMessage(result.message);
      } else if (path === '/reset-password') {
        await api('/api/auth/reset-password', 'POST', { email: value('email'), token: query.get('token'), password: value('password'), passwordConfirmation: value('passwordConfirmation') });
        window.location.replace('/login?status=password-reset');
      } else if (path === '/confirm-password') {
        const result = await api<{ message: string }>('/api/auth/confirm-password', 'POST', { password: value('password') });
        setMessage(result.message); form.reset();
      } else if (path === '/change-password') {
        await api('/api/auth/password', 'PUT', { currentPassword: value('currentPassword'), password: value('password'), passwordConfirmation: value('passwordConfirmation') });
        window.location.replace('/login?status=password-changed');
      }
    });
  }

  const passwordField = (name: string, label: string, current = false) => (
    <label className="auth-field" key={name}>{label}
      <input name={name} type="password" required autoComplete={current ? 'current-password' : 'new-password'} minLength={current ? undefined : 8} />
    </label>
  );

  const showForm = known && !path.startsWith('/admin/') && path !== '/verify-email' && (!protectedPaths.includes(path) || user);

  if(path.startsWith('/admin/') && user && authz?.canAccessAdmin) return <AdminShell user={user} authz={authz} path={path}>
    {error&&<p className="auth-error" role="alert">{error}</p>}{message&&<p className="auth-success" role="status">{message}</p>}
    {path === '/admin/dashboard' && authz.canAccessAdmin && (
      <section className="admin-dashboard">
        <AdminPageHeading title="Dashboard" description="Overview of the currently available CMS administration areas." />
        {dashboard ? (<>
          <section className="dashboard-overview">
            <h2>CMS overview</h2>
            <p>Review the administration areas currently available to your account.</p>
          </section>
          <DashboardSummaryCards summary={dashboard} capabilities={authz} />
        </>) : loading ? <p role="status">Loading dashboard…</p> : !error && <p className="auth-error" role="alert">Dashboard data is unavailable.</p>}
        <section className="dashboard-account-card" aria-labelledby="dashboard-account-heading">
          <h2 id="dashboard-account-heading">Account</h2>
          <div className="dashboard-account-actions">
            <a className="dashboard-action-link" href="/change-password">Change password</a>
            <a className="dashboard-action-link" href="/confirm-password">Confirm password</a>
            <a className="dashboard-action-link" href="/verify-email">{user.emailVerified ? 'Email verified' : 'Verify email'}</a>
            <button className="dashboard-action-link" disabled={busy} onClick={() => void perform(async () => {
              await api('/api/auth/logout', 'POST', {});
              window.location.assign('/');
            })}>{busy ? 'Signing out…' : 'Sign out'}</button>
          </div>
        </section>
      </section>
    )}
    {(path==='/admin/users'||path==='/admin/roles')&&<><AdminPageHeading title={path==='/admin/users'?'Users':'Roles'} description={path==='/admin/users'?'View CMS users and manage their assigned primary role.':'Review web roles and manage their permissions.'} /><AdminPage kind={path.endsWith('users')?'users':'roles'} authz={authz}/></>}
  </AdminShell>;

  return (
    <div className="workspace auth-workspace">
      <header className="site-header">
        <a className="brand" href="/" aria-label="Diwakar Enterprise CMS home"><span className="brand-mark" aria-hidden="true">D</span>
          <span>Diwakar <span className="brand-detail">Enterprise CMS</span></span></a>
        <a className="auth-text-link" href="/">Back to home</a>
      </header>
      <main className="auth-main">
        <section className="auth-card" aria-labelledby="auth-heading">
          <p className="eyebrow">Your CMS workspace</p>
          <h1 id="auth-heading">{titles[path] ?? 'Page unavailable.'}</h1>
          {loading && <p role="status">Loading your account…</p>}
          {!known && <p>This page isn’t available. <a href="/login">Go to sign in</a>.</p>}
          {error && <p className="auth-error" role="alert">{error}</p>}
          {message && <p className="auth-success" role="status">{message}</p>}
          {path === '/login' && query.get('status')?.startsWith('password-') && <p className="auth-success" role="status">Your password has been updated. Sign in to continue.</p>}
          {showForm && !loading && <form onSubmit={submit} aria-busy={busy}>
            <fieldset disabled={busy}>
              {['/login', '/forgot-password', '/reset-password'].includes(path) && <label className="auth-field">Email
                <input name="email" type="email" maxLength={255} required autoComplete="username" defaultValue={path === '/reset-password' ? query.get('email') ?? '' : ''} />
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
          </form>}
          {path.startsWith('/admin/') && user && authz && !authz.canAccessAdmin && <>
            <p className="auth-error" role="alert">You do not have access to CMS administration.</p>
            <button className="auth-button secondary" disabled={busy} onClick={() => void perform(async () => {
              await api('/api/auth/logout', 'POST', {});
              window.location.assign('/login');
            })}>{busy ? 'Signing out…' : 'Sign out'}</button>
          </>}
          {path === '/verify-email' && user && <>
            <p>{user.emailVerified ? 'Your email is verified.' : `Confirm that ${user.email} belongs to you.`}</p>
            {!user.emailVerified && <>
              {query.has('token') && <button className="auth-button" disabled={busy} onClick={() => void perform(async () => {
                const result = await api<{ message: string }>('/api/auth/verify-email', 'POST', { token: query.get('token') });
                setMessage(result.message); setUser({ ...user, emailVerified: true });
                window.history.replaceState(null, '', '/verify-email');
              })}>Verify email</button>}
              <button className="auth-button secondary" disabled={busy} onClick={() => void perform(async () => {
                const result = await api<{ message: string }>('/api/auth/verification-notification', 'POST', {}); setMessage(result.message);
              })}>Send verification link</button>
            </>}
          </>}
          {known && path !== '/login' && !path.startsWith('/admin/') && <p className="auth-back"><a href={user ? '/admin/dashboard' : '/login'}>{user ? 'Back to account' : 'Back to sign in'}</a></p>}
        </section>
      </main>
      <footer>Diwakar Enterprise CMS <span>A home for your content.</span></footer>
    </div>
  );
}
