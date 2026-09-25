import { describe, it, expect } from 'vitest';
import {
  formatStatus,
  formatPriority,
  formatRole,
  formatPrice,
  formatTotal,
  formatDate,
} from '../utils/formatters';

describe('formatters', () => {
  describe('formatStatus', () => {
    it('formats known status enum values to title case', () => {
      expect(formatStatus('DRAFT')).toBe('Draft');
      expect(formatStatus('SUBMITTED')).toBe('Submitted');
      expect(formatStatus('UNDER_EVALUATION')).toBe('Under Evaluation');
      expect(formatStatus('PENDING_APPROVAL')).toBe('Pending Approval');
      expect(formatStatus('APPROVED')).toBe('Approved');
      expect(formatStatus('REJECTED')).toBe('Rejected');
      expect(formatStatus('REVISION_REQUESTED')).toBe('Revision Requested');
      expect(formatStatus('COMPLETED')).toBe('Completed');
    });

    it('handles empty string gracefully', () => {
      expect(formatStatus('')).toBe('');
    });
  });

  describe('formatPriority', () => {
    it('formats priority values', () => {
      expect(formatPriority('LOW')).toBe('Low');
      expect(formatPriority('MEDIUM')).toBe('Medium');
      expect(formatPriority('HIGH')).toBe('High');
      expect(formatPriority('URGENT')).toBe('Urgent');
    });
  });

  describe('formatRole', () => {
    it('formats system role values', () => {
      expect(formatRole('EMPLOYEE')).toBe('Employee');
      expect(formatRole('PROCUREMENT_OFFICER')).toBe('Procurement Officer');
      expect(formatRole('ADMIN')).toBe('Admin');
    });
  });

  describe('formatPrice and formatTotal', () => {
    it('displays Pending Quote for 0 or negative price', () => {
      expect(formatPrice(0)).toBe('Pending Quote');
      expect(formatPrice(null)).toBe('Pending Quote');
      expect(formatPrice(125.5)).toBe('$125.50');
    });

    it('displays TBD for 0 or negative total', () => {
      expect(formatTotal(0)).toBe('TBD');
      expect(formatTotal(null)).toBe('TBD');
      expect(formatTotal(450)).toBe('$450.00');
    });
  });

  describe('formatDate', () => {
    it('formats valid ISO date string', () => {
      const res = formatDate('2026-10-15T00:00:00Z');
      expect(res).not.toBe('—');
    });

    it('handles null or invalid date gracefully', () => {
      expect(formatDate(null)).toBe('—');
      expect(formatDate('invalid')).toBe('—');
    });
  });
});
