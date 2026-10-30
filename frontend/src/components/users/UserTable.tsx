import type { FormEvent } from 'react';

export type UserRow = { id: string; name: string; email: string; emailVerified: boolean; role: string | null };
export type AssignmentRole = { id: string; name: string };

export default function UserTable({ users, roles, assignments, canAssignRoles, busy, error, message, onAssignmentChange, onSaveRole }: {
  users: UserRow[];
  roles: AssignmentRole[];
  assignments: Record<string, string>;
  canAssignRoles: boolean;
  busy: boolean;
  error: string;
  message: string;
  onAssignmentChange: (userId: string, roleId: string) => void;
  onSaveRole: (event: FormEvent<HTMLFormElement>, userId: string) => void;
}) {
  return <section className="admin-table-card" aria-label="CMS users">
    {error && <p className="auth-error" role="alert">{error}</p>}
    {message && <p className="auth-success" role="status">{message}</p>}
    <div className="admin-table-scroll"><table className="admin-users-table">
      <thead><tr><th scope="col">Name</th><th scope="col">Email</th><th scope="col">Role</th><th scope="col">Assign role</th></tr></thead>
      <tbody>{users.map(user => <tr key={user.id}>
        <td>{user.name}</td><td>{user.email}</td><td>{user.role ?? '—'}</td>
        <td>{canAssignRoles
          ? <form className="admin-assignment-form" onSubmit={event => onSaveRole(event, user.id)}>
            <label className="sr-only" htmlFor={'role-' + user.id}>Assign role to {user.name}</label>
            <select id={'role-' + user.id} required value={assignments[user.id] ?? ''} onChange={event => onAssignmentChange(user.id, event.target.value)}>
              <option value="">Choose a role</option>{roles.map(role => <option key={role.id} value={role.id}>{role.name}</option>)}
            </select>
            <button className="admin-save" disabled={busy || !assignments[user.id]}>Save</button>
          </form>
          : <span aria-label="Role assignment unavailable">—</span>}</td>
      </tr>)}</tbody>
    </table></div>
  </section>;
}
