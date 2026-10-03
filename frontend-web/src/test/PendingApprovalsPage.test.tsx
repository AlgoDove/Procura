import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import PendingApprovalsPage from '../pages/approvals/PendingApprovalsPage';
import * as endpoints from '../api/endpoints';

vi.mock('../api/endpoints', () => ({
  getPendingApprovals: vi.fn(),
}));

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <PendingApprovalsPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('PendingApprovalsPage', () => {
  it('renders heading and search input', async () => {
    vi.mocked(endpoints.getPendingApprovals).mockResolvedValue([]);
    renderPage();

    expect(screen.getByRole('heading', { name: /pending approvals/i })).toBeTruthy();
    expect(screen.getByPlaceholderText(/search by request number/i)).toBeTruthy();
    expect(await screen.findByText(/you have no procurement requests currently awaiting your approval/i)).toBeTruthy();
  });

  it('renders pending items when workflows exist', async () => {
    vi.mocked(endpoints.getPendingApprovals).mockResolvedValue([
      {
        id: 'wf-1',
        procurementRequestId: 'req-1',
        requestNumber: 'REQ-2026-001',
        requestTitle: 'Enterprise Server Upgrade',
        estimatedTotal: 15000,
        requesterId: 'usr-1',
        requesterName: 'Jane Doe',
        currentStatus: 'WAITING_MANAGER_APPROVAL',
        createdAt: '2026-09-28T00:00:00Z',
        updatedAt: '2026-09-28T00:00:00Z',
        decisions: [],
        notifications: [],
        vendorRecommendationSummary: {
          procurementRequestId: 'req-1',
          totalCandidatesEvaluated: 3,
          topScore: 94.5,
          recommendationSummary: 'Recommended Vendor A due to best pricing.',
          evaluatedAt: '2026-09-28T00:00:00Z',
          rankedEvaluations: [],
        },
      },
    ]);

    renderPage();

    expect(await screen.findByText('REQ-2026-001')).toBeTruthy();
    expect(screen.getByText('Enterprise Server Upgrade')).toBeTruthy();
    expect(screen.getByText('Jane Doe')).toBeTruthy();
    expect(screen.getByText('Score: 94.5')).toBeTruthy();
    expect(screen.getByRole('link', { name: /review & decide/i })).toBeTruthy();
  });
});
