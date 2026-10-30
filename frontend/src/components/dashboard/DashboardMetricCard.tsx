import type { Card } from './DashboardSummary';

export default function DashboardMetricCard({ card }: { card: Card }) {
  return <article className="dashboard-metric">
    <p className="dashboard-metric-label">{card.label}</p>
    <strong className={card.textValue ? 'dashboard-text-value' : undefined}>{card.value}</strong>
    <p>{card.description}</p>
    {card.href && <a className="dashboard-view-link" href={card.href}>{card.action}</a>}
  </article>;
}
