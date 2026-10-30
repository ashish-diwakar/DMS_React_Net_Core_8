import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { api, ApiError } from '../auth/api';
import CreateAdminForm from '../components/users/CreateAdminForm';
import UserTable from '../components/users/UserTable';
import type { UserRow, AssignmentRole } from '../components/users/UserTable';
import RoleList from '../components/roles/RoleList';
import type { Role } from '../components/roles/RoleList';

type Authz = { isSuperAdmin: boolean; canViewUsers: boolean; canAssignRoles: boolean; canViewRoles: boolean; canManagePermissions: boolean };
type RoleResponse = { roles: Role[]; managedPermissions: string[] };
type UserResponse = { users: UserRow[]; roles: AssignmentRole[] };

export default function AdminPage({ kind, authz }: { kind: 'users' | 'roles'; authz: Authz }) {
  const [users, setUsers] = useState<UserRow[]>([]);
  const [assignmentRoles, setAssignmentRoles] = useState<AssignmentRole[]>([]);
  const [data, setData] = useState<RoleResponse>({ roles: [], managedPermissions: [] });
  const [assign, setAssign] = useState<Record<string, string>>({});
  const [selected, setSelected] = useState<Record<string, string[]>>({});
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    const load = kind === 'users'
      ? api<UserResponse>('/api/admin/users', 'GET', undefined, controller.signal).then(value => {
        setUsers(value.users);
        setAssignmentRoles(value.roles);
        setAssign(Object.fromEntries(value.users.map(user => [user.id, value.roles.find(role => role.name === user.role)?.id ?? ''])));
      })
      : api<RoleResponse>('/api/admin/roles', 'GET', undefined, controller.signal).then(value => {
        setData(value);
        setSelected(Object.fromEntries(value.roles.map(role => [role.id, role.permissions.filter(permission => value.managedPermissions.includes(permission))])));
      });
    void load.catch((failure: unknown) => {
      if (!controller.signal.aborted) setError(failure instanceof ApiError ? failure.message : 'Service unavailable.');
    });
    return () => controller.abort();
  }, [kind]);

  async function save(action: () => Promise<unknown>) {
    setBusy(true); setError(''); setMessage('');
    try {
      const result = await action() as { message?: string };
      setMessage(result.message ?? 'Saved.');
    } catch (failure) {
      setError(failure instanceof ApiError ? failure.message : 'Service unavailable.');
    } finally {
      setBusy(false);
    }
  }

  function saveRole(event: FormEvent<HTMLFormElement>, userId: string) {
    event.preventDefault();
    void save(async () => {
      const result = await api('/api/admin/users/' + encodeURIComponent(userId) + '/role', 'PATCH', { roleId: assign[userId] ?? '' });
      const role = assignmentRoles.find(item => item.id === assign[userId])?.name ?? null;
      setUsers(current => current.map(item => item.id === userId ? { ...item, role } : item));
      return result;
    });
  }

  function savePermissions(event: FormEvent<HTMLFormElement>, roleId: string) {
    event.preventDefault();
    void save(() => api('/api/admin/roles/' + encodeURIComponent(roleId) + '/permissions', 'PATCH', { permissions: selected[roleId] ?? [] }));
  }

  if (kind === 'users') return <>
    {authz.isSuperAdmin && <CreateAdminForm onCreated={created => {
      setUsers(current => [created, ...current].slice(0, 500));
      const role = assignmentRoles.find(item => item.name === 'Admin');
      if (role) setAssign(current => ({ ...current, [created.id]: role.id }));
    }} />}
    <UserTable users={users} roles={assignmentRoles} assignments={assign} canAssignRoles={authz.canAssignRoles}
      busy={busy} error={error} message={message}
      onAssignmentChange={(userId, roleId) => setAssign(current => ({ ...current, [userId]: roleId }))}
      onSaveRole={saveRole} />
  </>;

  return <RoleList roles={data.roles} managedPermissions={data.managedPermissions} selected={selected}
    canManagePermissions={authz.canManagePermissions} busy={busy} error={error} message={message}
    onSelectionChange={(roleId, permission, checked) => setSelected(current => ({
      ...current,
      [roleId]: checked ? [...(current[roleId] ?? []), permission] : (current[roleId] ?? []).filter(item => item !== permission),
    }))}
    onSavePermissions={savePermissions} />;
}
