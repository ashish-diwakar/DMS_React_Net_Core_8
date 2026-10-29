import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { api, ApiError } from '../auth/api';

type User = { name: string };
type Authz = {
  roles: string[];
  permissions: string[];
  isSuperAdmin: boolean;
  canAccessAdmin: boolean;
  canViewUsers: boolean;
  canViewRoles: boolean;
};

const laterModules = [
  { label: 'Settings', permission: 'settings.manage' },
  { label: 'Media Library', permission: 'media.manage' },
  { label: 'Pages', permission: 'pages.manage' },
];

export default function AdminShell({ user, authz, path, children }: {
  user: User;
  authz: Authz;
  path: string;
  children: ReactNode;
}) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const [signingOut, setSigningOut] = useState(false);
  const [signOutError, setSignOutError] = useState('');
  const profileRef = useRef<HTMLDivElement>(null);
  const profileButtonRef = useRef<HTMLButtonElement>(null);
  const role = authz.isSuperAdmin ? 'Super Admin' : authz.roles[0] ?? 'User';
  const initial = user.name.trim().charAt(0).toUpperCase() || 'U';

  useEffect(() => {
    if (!profileOpen) return;
    const closeOnOutsideClick = (event: PointerEvent) => {
      if (!profileRef.current?.contains(event.target as Node)) setProfileOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setProfileOpen(false);
        profileButtonRef.current?.focus();
      }
    };
    document.addEventListener('pointerdown', closeOnOutsideClick);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsideClick);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [profileOpen]);

  async function signOut() {
    if (signingOut) return;
    setSigningOut(true);
    setSignOutError('');
    try {
      await api('/api/auth/logout', 'POST', {});
      window.location.assign('/');
    } catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) {
        window.location.assign('/login');
        return;
      }
      setSignOutError(failure instanceof ApiError ? failure.message : 'Could not log out. Please try again.');
      setSigningOut(false);
    }
  }

  const navLink = (label: string, href: string, active: boolean) => (
    <a className={`admin-nav-link${active ? ' active' : ''}`} href={href}
      aria-current={active ? 'page' : undefined} onClick={() => setMobileOpen(false)}>
      <span className="admin-nav-dot" aria-hidden="true" />{label}
    </a>
  );

  return <div className="admin-shell">
    <aside className={`admin-sidebar${mobileOpen ? ' open' : ''}`}>
      <a className="admin-brand" href="/admin/dashboard">Diwakar Enterprise CMS</a>
      <nav className="admin-nav" aria-label="Primary navigation">
        <p className="admin-nav-group">General</p>
        {navLink('Dashboard', '/admin/dashboard', path === '/admin/dashboard')}
        <p className="admin-nav-group">Administration</p>
        {authz.canViewUsers && navLink('Users', '/admin/users', path === '/admin/users')}
        {authz.canViewRoles && navLink('Roles', '/admin/roles', path === '/admin/roles')}
        {laterModules.filter(item => authz.isSuperAdmin || authz.permissions.includes(item.permission)).map(item =>
          <span key={item.label} className="admin-nav-link disabled" aria-disabled="true" title="This module is not available in F002">
            <span className="admin-nav-dot" aria-hidden="true" />{item.label}
          </span>
        )}
      </nav>
    </aside>
    {mobileOpen && <button className="admin-backdrop" aria-label="Close navigation" onClick={() => setMobileOpen(false)} />}
    <div className="admin-app-area">
      <header className="admin-topbar">
        <button className="admin-menu-toggle" aria-label="Open navigation" aria-expanded={mobileOpen} onClick={() => setMobileOpen(!mobileOpen)}>☰</button>
        <span className="admin-topbar-title">Diwakar Enterprise CMS</span>
        <div className="admin-user" ref={profileRef}>
          <button ref={profileButtonRef} className="admin-user-trigger" type="button"
            aria-label={`Account menu for ${user.name}`} aria-expanded={profileOpen} aria-controls="admin-profile-panel"
            onClick={() => setProfileOpen(!profileOpen)}>
            <span className="admin-user-copy"><strong>{user.name}</strong><small>{role}</small></span>
            <span className="admin-avatar" aria-hidden="true">{initial}</span>
          </button>
          {profileOpen && <div className="admin-profile-panel" id="admin-profile-panel">
            <div className="admin-profile-identity"><strong>{user.name}</strong><small>{role}</small></div>
            <button className="admin-profile-logout" type="button" disabled={signingOut} onClick={() => void signOut()}>
              {signingOut ? 'Logging out…' : 'Log out'}
            </button>
            {signOutError && <p className="admin-profile-error" role="alert">{signOutError}</p>}
          </div>}
        </div>
      </header>
      <main className="admin-main"><div className="admin-page-container">{children}</div></main>
    </div>
  </div>;
}
