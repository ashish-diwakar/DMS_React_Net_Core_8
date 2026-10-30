import type { FormEvent } from 'react';

export type Role = { id: string; name: string; permissions: string[] };

export default function RoleList({ roles, managedPermissions, selected, canManagePermissions, busy, error, message, onSelectionChange, onSavePermissions }: {
  roles: Role[];
  managedPermissions: string[];
  selected: Record<string, string[]>;
  canManagePermissions: boolean;
  busy: boolean;
  error: string;
  message: string;
  onSelectionChange: (roleId: string, permission: string, checked: boolean) => void;
  onSavePermissions: (event: FormEvent<HTMLFormElement>, roleId: string) => void;
}) {
  return <section className="admin-role-list">
    {error && <p className="auth-error" role="alert">{error}</p>}
    {message && <p className="auth-success" role="status">{message}</p>}
    <div className="admin-list">{roles.map(role => <article className="admin-row" key={role.id}>
      <h2>{role.name}</h2>
      {role.name === 'Super Admin'
        ? <p>Protected full-access role. Permissions are granted through the centralized Super Admin authorization bypass.</p>
        : canManagePermissions
          ? <form onSubmit={event => onSavePermissions(event, role.id)}>
            <fieldset disabled={busy}>
              <div className="permissions-grid">{managedPermissions.map(permission => <label className="permission-option" key={permission}>
                <input type="checkbox" checked={(selected[role.id] ?? []).includes(permission)} onChange={event => onSelectionChange(role.id, permission, event.target.checked)} />{permission}
              </label>)}</div>
              <button className="admin-save">Save Permissions</button>
            </fieldset>
          </form>
          : <div className="permissions-grid" aria-label={role.name + ' permissions (read only)'}>
            {[...new Set([...managedPermissions, ...role.permissions])].map(permission => <label className="permission-option" key={permission}>
              <input type="checkbox" checked={role.permissions.includes(permission)} disabled readOnly />{permission}
            </label>)}
          </div>}
    </article>)}</div>
  </section>;
}
