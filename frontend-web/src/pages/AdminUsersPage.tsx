import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../context/AuthContext';
import { getUsers, updateUserRole } from '../api/endpoints';
import type { UserSummaryResponse, UpdateUserRoleRequest } from '../types/api';
import { formatRole, formatDate } from '../utils/formatters';
import styles from './AdminUsersPage.module.css';

const ALLOWED_ROLES: UpdateUserRoleRequest['role'][] = [
  'EMPLOYEE',
  'PROCUREMENT_OFFICER',
  'ADMIN',
];

export default function AdminUsersPage() {
  const { user: currentUser } = useAuth();
  const queryClient = useQueryClient();

  const [selectedRoles, setSelectedRoles] = useState<Record<string, UpdateUserRoleRequest['role']>>({});
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { data: users = [], isLoading, error } = useQuery({
    queryKey: ['admin-users'],
    queryFn: getUsers,
  });

  const roleMutation = useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: UpdateUserRoleRequest['role'] }) =>
      updateUserRole(userId, role),
    onSuccess: (updatedUser) => {
      setErrorMessage(null);
      setSuccessMessage(`Role for ${updatedUser.email} updated to ${formatRole(updatedUser.role)}.`);
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      // Clear message after 5 seconds
      setTimeout(() => setSuccessMessage(null), 5000);
    },
    onError: (err: unknown) => {
      setSuccessMessage(null);
      const axiosErr = err as { response?: { data?: string | { message?: string } } };
      const data = axiosErr.response?.data;
      const msg = typeof data === 'string' ? data : data?.message ?? 'Failed to update user role.';
      setErrorMessage(msg);
    },
  });

  const handleRoleSelect = (userId: string, newRole: UpdateUserRoleRequest['role']) => {
    setSelectedRoles((prev) => ({ ...prev, [userId]: newRole }));
  };

  const handleApplyRole = (user: UserSummaryResponse) => {
    const targetRole = selectedRoles[user.id] ?? (user.role as UpdateUserRoleRequest['role']);
    if (targetRole === user.role) return;

    if (user.id === currentUser?.userId && targetRole !== 'ADMIN') {
      const confirmed = window.confirm(
        'Warning: You are changing your own role away from ADMIN. You will lose access to administrative functions. Are you sure?'
      );
      if (!confirmed) return;
    }

    roleMutation.mutate({ userId: user.id, role: targetRole });
  };

  const getRoleBadgeClass = (role: string) => {
    switch (role) {
      case 'ADMIN':
        return styles.roleAdmin;
      case 'PROCUREMENT_OFFICER':
        return styles.roleOfficer;
      case 'EMPLOYEE':
        return styles.roleEmployee;
      default:
        return styles.roleManager;
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h1 className={styles.heading}>User & Role Management</h1>
        <p className={styles.subheading}>
          Manage system users and assign authorized roles. Public registrations default to Employee.
        </p>
      </div>

      {successMessage && (
        <div className={`${styles.messageBanner} ${styles.successBanner}`}>
          {successMessage}
        </div>
      )}

      {errorMessage && (
        <div className={`${styles.messageBanner} ${styles.errorBanner}`}>
          {errorMessage}
        </div>
      )}

      <div className={styles.card}>
        {isLoading && <p className={styles.state}>Loading users…</p>}
        {error && <p className={`${styles.state} ${styles.errorBanner}`}>Failed to load users. Please verify admin permissions.</p>}

        {!isLoading && !error && users.length === 0 && (
          <p className={styles.emptyState}>No users registered yet.</p>
        )}

        {!isLoading && !error && users.length > 0 && (
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Current Role</th>
                <th>Joined</th>
                <th>Change Role</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => {
                const isCurrent = u.id === currentUser?.userId;
                const chosenRole = selectedRoles[u.id] ?? (u.role as UpdateUserRoleRequest['role']);
                const isChanged = chosenRole !== u.role;
                const isPendingThis = roleMutation.isPending && roleMutation.variables?.userId === u.id;

                return (
                  <tr key={u.id}>
                    <td>
                      <strong>{u.firstName} {u.lastName}</strong>
                      {isCurrent && <span className={styles.youBadge}>You</span>}
                    </td>
                    <td>{u.email}</td>
                    <td>
                      <span className={`${styles.roleBadge} ${getRoleBadgeClass(u.role)}`}>
                        {formatRole(u.role)}
                      </span>
                    </td>
                    <td>{formatDate(u.createdAt)}</td>
                    <td>
                      <div className={styles.roleAction}>
                        <select
                          className={styles.roleSelect}
                          value={chosenRole}
                          onChange={(e) =>
                            handleRoleSelect(u.id, e.target.value as UpdateUserRoleRequest['role'])
                          }
                          disabled={isPendingThis}
                        >
                          {ALLOWED_ROLES.map((role) => (
                            <option key={role} value={role}>
                              {formatRole(role)}
                            </option>
                          ))}
                        </select>
                        {isChanged && (
                          <button
                            type="button"
                            className={styles.btnChange}
                            onClick={() => handleApplyRole(u)}
                            disabled={isPendingThis}
                          >
                            {isPendingThis ? 'Saving…' : 'Apply'}
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
