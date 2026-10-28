import { useEffect, useState } from 'react';
import AuthApp from './auth/AuthApp';

type ConnectionStatus = 'checking' | 'available' | 'unavailable';

const connectionLabels: Record<ConnectionStatus, string> = {
  checking: 'Checking connection…',
  available: 'Service available',
  unavailable: 'Service unavailable',
};

export default function App() {
  return window.location.pathname === '/' ? <Foundation /> : <AuthApp />;
}

function Foundation() {
  const [connection, setConnection] = useState<ConnectionStatus>('checking');

  useEffect(() => {
    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), 5000);
    let active = true;

    async function checkConnection() {
      try {
        const response = await fetch('/api/health', {
          signal: controller.signal,
          cache: 'no-store',
        });
        const status = await response.text();

        if (active) {
          setConnection(response.ok && status.trim() === 'Healthy' ? 'available' : 'unavailable');
        }
      } catch {
        if (active) setConnection('unavailable');
      } finally {
        window.clearTimeout(timeout);
      }
    }

    void checkConnection();

    return () => {
      active = false;
      window.clearTimeout(timeout);
      controller.abort();
    };
  }, []);

  return (
    <div className="workspace">
      <header className="site-header">
        <a className="brand" href="/" aria-label="Diwakar Enterprise CMS home">
          <span className="brand-mark" aria-hidden="true">D</span>
          <span>Diwakar <span className="brand-detail">Enterprise CMS</span></span>
        </a>
        <a className="auth-text-link" href="/login">Sign in</a>
      </header>

      <main id="main-content">
        <section className="welcome" aria-labelledby="welcome-heading">
          <p className="eyebrow">A foundation for your content</p>
          <h1 id="welcome-heading">Your next chapter<br />starts here.</h1>
          <p className="intro">A shared home for managing your website, built to grow with your business.</p>
          <div className="connection" role="status" aria-live="polite">
            <span className={`status-dot ${connection}`} aria-hidden="true" />
            <span>{connectionLabels[connection]}</span>
          </div>
        </section>

        <aside className="setup-card" aria-labelledby="setup-heading">
          <span className="card-number" aria-hidden="true">01 / Foundation</span>
          <h2 id="setup-heading">Room to build.</h2>
          <p>Sign in to your account to get started. Content management will become available as each module is introduced.</p>
          <div className="card-rule" />
          <p className="card-note">Thoughtfully structured.<br />Ready to grow.</p>
        </aside>
      </main>

      <footer>Diwakar Enterprise CMS <span>Made for what comes next.</span></footer>
    </div>
  );
}
