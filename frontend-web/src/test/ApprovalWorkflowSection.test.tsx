import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ApprovalWorkflowSection from '../components/approvals/ApprovalWorkflowSection';
import * as endpoints from '../api/endpoints';

vi.mock('../api/endpoints', () => ({
  getApprovalWorkflowByRequestId: vi.fn(),
  initializeApprovalWorkflow: vi.fn(),
  transitionApprovalWorkflow: vi.fn(),
  approveWorkflow: vi.fn(),
  rejectWorkflow: vi.fn(),
  requestWorkflowRevision: vi.fn(),
}));

vi.mock('../context/AuthContext', () => ({
  useAuth: () => ({
    user: {
      userId: 'manager-1',
      displayName: 'Alex Manager',
      role: 'MANAGER',
    },
    logout: vi.fn(),
  }),
}));

function renderSection(requestId: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <ApprovalWorkflowSection requestId={requestId} />
    </QueryClientProvider>
  );
}

describe('ApprovalWorkflowSection', () => {
  it('renders uninitialized state when no workflow exists', async () => {
    vi.mocked(endpoints.getApprovalWorkflowByRequestId).mockResolvedValue(null as any);
    renderSection('req-1');

    expect(await screen.findByText(/no formal approval workflow has been initialized/i)).toBeTruthy();
    expect(screen.getByRole('button', { name: /initialize approval workflow/i })).toBeTruthy();
  });

  it('renders active workflow with manager action buttons when WAITING_MANAGER_APPROVAL', async () => {
    vi.mocked(endpoints.getApprovalWorkflowByRequestId).mockResolvedValue({
      id: 'wf-1',
      procurementRequestId: 'req-1',
      requestNumber: 'REQ-100',
      requestTitle: 'Hardware Proc',
      estimatedTotal: 8500,
      requesterId: 'emp-1',
      requesterName: 'Sam Requester',
      currentStatus: 'WAITING_MANAGER_APPROVAL',
      createdAt: '2026-09-28T00:00:00Z',
      updatedAt: '2026-09-28T00:00:00Z',
      decisions: [
        {
          id: 'dec-1',
          managerId: 'mgr-2',
          managerName: 'Prior Manager',
          decision: 'REVISION_REQUESTED',
          comments: 'Needs justification',
          createdAt: '2026-09-27T00:00:00Z',
        },
      ],
      notifications: [],
      vendorRecommendationSummary: {
        procurementRequestId: 'req-1',
        totalCandidatesEvaluated: 2,
        topScore: 91.2,
        recommendationSummary: 'Hardware supply vendor recommended.',
        evaluatedAt: '2026-09-28T00:00:00Z',
        rankedEvaluations: [],
      },
    });

    renderSection('req-1');

    expect(await screen.findByText(/approval lifecycle/i)).toBeTruthy();
    expect(screen.getByText(/manager review required/i)).toBeTruthy();
    expect(screen.getByRole('button', { name: /✓ approve request/i })).toBeTruthy();
    expect(screen.getByRole('button', { name: /request revision/i })).toBeTruthy();
    expect(screen.getByRole('button', { name: /✗ reject request/i })).toBeTruthy();
    expect(screen.getByText('Prior Manager — REVISION_REQUESTED')).toBeTruthy();
  });
});
