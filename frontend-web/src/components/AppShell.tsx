import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import styles from './AppShell.module.css';

export default function AppShell({ children }: { children: React.ReactNode }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const isProcurementOfficerOrAdmin =
    user?.role === 'PROCUREMENT_OFFICER' || user?.role === 'ADMIN';

  const navLinkClass = (path: string) =>
    [styles.navLink, location.pathname.startsWith(path) ? styles.active : ''].join(' ');

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <div className={styles.brand}>
          <Link to="/" className={styles.brandLink}>Procura</Link>
        </div>
        <nav className={styles.nav}>
          <Link to="/requests" className={navLinkClass('/requests')}>
            Procurement Requests
          </Link>
          {isProcurementOfficerOrAdmin && (
            <Link to="/vendors" className={navLinkClass('/vendors')}>
              Vendors
            </Link>
          )}
          {user?.role === 'ADMIN' && (
            <Link to="/admin" className={navLinkClass('/admin')}>
              Admin
            </Link>
          )}
        </nav>
        <div className={styles.userArea}>
          <span className={styles.userInfo}>
            {user?.email} &middot; <span className={styles.role}>{user?.role}</span>
          </span>
          <button onClick={handleLogout} className={styles.logoutBtn}>
            Log out
          </button>
        </div>
      </header>
      <main className={styles.main}>{children}</main>
    </div>
  );
}
