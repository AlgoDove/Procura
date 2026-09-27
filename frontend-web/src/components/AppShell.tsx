import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { formatRole } from '../utils/formatters';
import styles from './AppShell.module.css';

export default function AppShell({ children }: { children: React.ReactNode }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const isEmployee = user?.role === 'EMPLOYEE';
  const isProcurementOfficer = user?.role === 'PROCUREMENT_OFFICER';
  const isAdmin = user?.role === 'ADMIN';

  const navLinkClass = (path: string) =>
    [styles.navLink, location.pathname.startsWith(path) ? styles.active : ''].join(' ');

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <div className={styles.headerLeft}>
          <Link to="/" className={styles.brandLink}>Procura</Link>
          <nav className={styles.nav}>
            <Link to="/requests" className={navLinkClass('/requests')}>
              {isEmployee ? 'My Requests' : 'Procurement Requests'}
            </Link>
            {isEmployee && (
              <Link to="/requests/new" className={navLinkClass('/requests/new')}>
                + New Request
              </Link>
            )}
            {(isProcurementOfficer || isAdmin) && (
              <Link to="/vendors" className={navLinkClass('/vendors')}>
                Vendors
              </Link>
            )}
            {isAdmin && (
              <Link to="/admin" className={navLinkClass('/admin')}>
                Admin / Users
              </Link>
            )}
          </nav>
        </div>
        <div className={styles.userArea}>
          <div className={styles.userInfo}>
            <span className={styles.userName}>{user?.displayName}</span>
            <span className={styles.roleBadge}>{formatRole(user?.role ?? '')}</span>
          </div>
          <button onClick={handleLogout} className={styles.logoutBtn}>
            Log out
          </button>
        </div>
      </header>
      <main className={styles.main}>{children}</main>
    </div>
  );
}
