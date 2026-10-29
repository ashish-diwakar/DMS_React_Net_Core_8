type GlobalSummary = { scope: 'global'; userCount: number; roleCount: number; permissionCount: number };
type PersonalSummary = { scope: 'personal'; primaryRole: string | null; effectivePermissionCount: number; userCount?: number };
export type DashboardSummary = GlobalSummary | PersonalSummary;

type Capabilities = { canViewUsers: boolean; canViewRoles: boolean };
type Card = { label: string; value: number | string; description: string; href?: string; action?: string; textValue?: boolean };

const validCount = (value: unknown): value is number => typeof value === 'number' && Number.isSafeInteger(value) && value >= 0;

export function isDashboardSummary(value: unknown): value is DashboardSummary {
  if (!value || typeof value !== 'object') return false;
  const summary = value as Record<string, unknown>;
  if (summary.scope === 'global')
    return validCount(summary.userCount) && validCount(summary.roleCount) && validCount(summary.permissionCount)
      && !Object.hasOwn(summary, 'primaryRole') && !Object.hasOwn(summary, 'effectivePermissionCount');
  if (summary.scope === 'personal')
    return (summary.primaryRole === null || typeof summary.primaryRole === 'string')
      && validCount(summary.effectivePermissionCount)
      && (!Object.hasOwn(summary, 'userCount') || validCount(summary.userCount))
      && !Object.hasOwn(summary, 'roleCount') && !Object.hasOwn(summary, 'permissionCount');
  return false;
}

export default function DashboardSummaryCards({ summary, capabilities }: { summary: DashboardSummary; capabilities: Capabilities }) {
  const cards: Card[] = summary.scope === 'global' ? [
    { label: 'Users', value: summary.userCount, description: 'Total CMS user accounts.', href: capabilities.canViewUsers ? '/admin/users' : undefined, action: 'View Users' },
    { label: 'Roles', value: summary.roleCount, description: 'Configured CMS roles for the web guard.', href: capabilities.canViewRoles ? '/admin/roles' : undefined, action: 'View Roles' },
    { label: 'Permissions', value: summary.permissionCount, description: 'Configured CMS permissions for the web guard.', href: capabilities.canViewRoles ? '/admin/roles' : undefined, action: 'View Roles' },
  ] : [
    ...(capabilities.canViewUsers && summary.userCount !== undefined ? [{ label: 'Total CMS users', value: summary.userCount, description: 'Total CMS user accounts.', href: '/admin/users', action: 'View Users' }] : []),
    { label: 'My role', value: summary.primaryRole ?? 'No role assigned', description: 'Your assigned primary CMS role.', href: capabilities.canViewRoles ? '/admin/roles' : undefined, action: 'View Roles', textValue: true },
    { label: 'My permissions', value: summary.effectivePermissionCount, description: 'Effective web permissions for your account.' },
  ];

  return <section className="dashboard-metrics" aria-label="Dashboard summary">
    {cards.map(card => <article className="dashboard-metric" key={card.label}>
      <p className="dashboard-metric-label">{card.label}</p>
      <strong className={card.textValue ? 'dashboard-text-value' : undefined}>{card.value}</strong>
      <p>{card.description}</p>
      {card.href && <a className="dashboard-view-link" href={card.href}>{card.action}</a>}
    </article>)}
  </section>;
}
