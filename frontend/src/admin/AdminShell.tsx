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
  canManageSettings: boolean;
};

const laterModules = [
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
  const mobileToggleRef = useRef<HTMLButtonElement>(null);
  const mobileCloseRef = useRef<HTMLButtonElement>(null);
  const sidebarRef = useRef<HTMLElement>(null);
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

  function closeMobileNavigation() {
    setMobileOpen(false);
    mobileToggleRef.current?.focus();
  }

  useEffect(() => {
    if (!mobileOpen) return;
    mobileCloseRef.current?.focus();
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        closeMobileNavigation();
      } else if (event.key === 'Tab') {
        const controls = sidebarRef.current?.querySelectorAll<HTMLElement>('a[href], button:not([disabled])');
        if (!controls?.length) return;
        const first = controls[0];
        const last = controls[controls.length - 1];
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first.focus();
        }
      }
    };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [mobileOpen]);

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
    <aside ref={sidebarRef} id="admin-sidebar" className={`admin-sidebar${mobileOpen ? ' open' : ''}`} role={mobileOpen ? 'dialog' : undefined} aria-modal={mobileOpen ? true : undefined} aria-label={mobileOpen ? 'Admin navigation panel' : undefined}>
      <div className="admin-sidebar-header"><a className="admin-brand" href="/admin/dashboard">Diwakar Enterprise CMS</a><button ref={mobileCloseRef} className="admin-sidebar-close" type="button" aria-label="Close navigation" onClick={closeMobileNavigation}>×</button></div>
      <nav className="admin-nav" aria-label="Primary navigation">
        <p className="admin-nav-group">General</p>
        {navLink('Dashboard', '/admin/dashboard', path === '/admin/dashboard')}
        <p className="admin-nav-group">Administration</p>
        {authz.canViewUsers && navLink('Users', '/admin/users', path === '/admin/users')}
        {authz.canViewRoles && navLink('Roles', '/admin/roles', path === '/admin/roles')}
        {authz.canManageSettings && navLink('Settings', '/admin/settings', path === '/admin/settings')}
        {laterModules.filter(item => authz.isSuperAdmin || authz.permissions.includes(item.permission)).map(item =>
          <span key={item.label} className="admin-nav-link disabled" aria-disabled="true" title="This module is not available in F002">
            <span className="admin-nav-dot" aria-hidden="true" />{item.label}
          </span>
        )}
      </nav>
    </aside>
    {mobileOpen && <button className="admin-backdrop" type="button" aria-label="Close navigation" tabIndex={-1} onClick={closeMobileNavigation} />}
    <div className="admin-app-area">
      <header className="admin-topbar">
        <button ref={mobileToggleRef} className="admin-menu-toggle" type="button" aria-label="Open navigation" aria-controls="admin-sidebar" aria-expanded={mobileOpen} onClick={() => { setProfileOpen(false); setMobileOpen(true); }}>☰</button>
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
