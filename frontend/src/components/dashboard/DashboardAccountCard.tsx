export default function DashboardAccountCard({ emailVerified, busy, signOut }: {
  emailVerified: boolean;
  busy: boolean;
  signOut: () => void;
}) {
  return <section className="dashboard-account-card" aria-labelledby="dashboard-account-heading">
    <h2 id="dashboard-account-heading">Account</h2>
    <div className="dashboard-account-actions">
      <a className="dashboard-action-link" href="/change-password">Change password</a>
      <a className="dashboard-action-link" href="/confirm-password">Confirm password</a>
      <a className="dashboard-action-link" href="/verify-email">{emailVerified ? 'Email verified' : 'Verify email'}</a>
      <button className="dashboard-action-link" disabled={busy} onClick={signOut}>{busy ? 'Signing out…' : 'Sign out'}</button>
    </div>
  </section>;
}
