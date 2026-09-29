import 'package:flutter/material.dart';
import '../../models/api_models.dart';
import '../../utils/formatters.dart';

class VendorRecommendationCard extends StatelessWidget {
  final ProcurementEvaluationSummaryDto? summary;
  final String requestStatus;
  final String userRole;

  const VendorRecommendationCard({
    super.key,
    required this.summary,
    required this.requestStatus,
    required this.userRole,
  });

  @override
  Widget build(BuildContext context) {
    final isEmployee = userRole == 'EMPLOYEE';

    // 1. Employee Status Notice: UNDER_EVALUATION
    if (isEmployee && requestStatus == 'UNDER_EVALUATION') {
      return Container(
        margin: const EdgeInsets.only(top: 12),
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: const Color(0xFFF8FAFC),
          border: Border.all(color: const Color(0xFFCBD5E1)),
          borderRadius: BorderRadius.circular(8),
        ),
        child: const Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('⏳', style: TextStyle(fontSize: 18)),
            SizedBox(width: 8),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Vendor Evaluation in Progress',
                    style: TextStyle(fontWeight: FontWeight.bold, fontSize: 13, color: Color(0xFF1E293B)),
                  ),
                  SizedBox(height: 2),
                  Text(
                    'The procurement team is actively reviewing quotes and evaluating suppliers. The approved vendor recommendation will appear here once finalized.',
                    style: TextStyle(fontSize: 12, color: Color(0xFF475569), height: 1.4),
                  ),
                ],
              ),
            ),
          ],
        ),
      );
    }

    // 2. Employee Status Notice: PENDING_APPROVAL
    if (isEmployee && requestStatus == 'PENDING_APPROVAL') {
      return Container(
        margin: const EdgeInsets.only(top: 12),
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: const Color(0xFFFFFBEB),
          border: Border.all(color: const Color(0xFFFDE68A)),
          borderRadius: BorderRadius.circular(8),
        ),
        child: const Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('📋', style: TextStyle(fontSize: 18)),
            SizedBox(width: 8),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Awaiting Management Approval',
                    style: TextStyle(fontWeight: FontWeight.bold, fontSize: 13, color: Color(0xFF92400E)),
                  ),
                  SizedBox(height: 2),
                  Text(
                    'Candidate vendors have been evaluated and the recommendation has been submitted. This request is now waiting for manager approval.',
                    style: TextStyle(fontSize: 12, color: Color(0xFF78350F), height: 1.4),
                  ),
                ],
              ),
            ),
          ],
        ),
      );
    }

    // 3. Evaluation Summary Card (Approved/Completed for employees, or all stages for Officers/Admins)
    if (summary == null || summary!.rankedEvaluations.isEmpty) {
      return const SizedBox.shrink();
    }

    final topEval = summary!.rankedEvaluations.first;

    // Extract cleaner vendor name from reasoning or fallback
    String vendorDisplayName = 'Vendor ${topEval.vendorId.length >= 8 ? topEval.vendorId.substring(0, 8) : topEval.vendorId}';
    final nameMatch = RegExp(r'^([A-Za-z0-9\s&.-]+?)\s+(?:achieved|offered|scored|was ranked)', caseSensitive: false).firstMatch(topEval.reasoning);
    if (nameMatch != null) {
      vendorDisplayName = nameMatch.group(1)!.trim();
    }

    // Extract metrics from reasoning
    String displayPrice = '—';
    final priceMatch = RegExp(r'\$([0-9,]+(?:\.[0-9]{2})?)').firstMatch(topEval.reasoning);
    if (priceMatch != null) {
      displayPrice = '\$${priceMatch.group(1)}';
    }

    String displayLeadTime = '—';
    final daysMatch = RegExp(r'([0-9]+)\s*day\(s\)', caseSensitive: false).firstMatch(topEval.reasoning);
    if (daysMatch != null) {
      displayLeadTime = '${daysMatch.group(1)} Days';
    }

    String displayReliability = '—';
    final relMatch = RegExp(r'reliability rating of ([0-9]+(?:\.[0-9]+)?)\/100', caseSensitive: false).firstMatch(topEval.reasoning);
    if (relMatch != null) {
      final parsed = double.tryParse(relMatch.group(1)!);
      if (parsed != null) {
        displayReliability = '${(parsed / 20).toStringAsFixed(1)} / 5.0';
      }
    }

    return Container(
      margin: const EdgeInsets.only(top: 12),
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: const Color(0xFF15803D), width: 1.5),
        borderRadius: BorderRadius.circular(10),
        boxShadow: const [
          BoxShadow(color: Color(0x0A000000), blurRadius: 4, offset: Offset(0, 2)),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header Bar
          Container(
            padding: const EdgeInsets.all(12),
            decoration: const BoxDecoration(
              color: Color(0xFFF0FDF4),
              borderRadius: BorderRadius.vertical(top: Radius.circular(8)),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Wrap(
                        spacing: 6,
                        runSpacing: 4,
                        children: [
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                            decoration: BoxDecoration(
                              color: const Color(0xFF15803D),
                              borderRadius: BorderRadius.circular(4),
                            ),
                            child: const Text(
                              '🏆 TOP RECOMMENDED',
                              style: TextStyle(color: Colors.white, fontSize: 10, fontWeight: FontWeight.bold),
                            ),
                          ),
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                            decoration: BoxDecoration(
                              color: topEval.generatedByAgent ? const Color(0xFF6D28D9) : const Color(0xFF475569),
                              borderRadius: BorderRadius.circular(4),
                            ),
                            child: Text(
                              topEval.generatedByAgent ? 'AI Evaluated' : 'Deterministic',
                              style: const TextStyle(color: Colors.white, fontSize: 10, fontWeight: FontWeight.bold),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 6),
                      Text(
                        vendorDisplayName,
                        style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFF0F172A)),
                      ),
                    ],
                  ),
                ),
                // Score Box
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    border: Border.all(color: const Color(0xFFBBF7D0)),
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Column(
                    children: [
                      Text(
                        topEval.overallScore.toStringAsFixed(1),
                        style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF15803D)),
                      ),
                      const Text(
                        'Score / 100',
                        style: TextStyle(fontSize: 9, color: Color(0xFF64748B), fontWeight: FontWeight.w600),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),

          // 2x2 Metric Grid
          Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              children: [
                Row(
                  children: [
                    Expanded(child: _metricTile('Quoted Price', displayPrice, 'Rank #1 value')),
                    const SizedBox(width: 8),
                    Expanded(child: _metricTile('Delivery Lead Time', displayLeadTime, 'Fulfillment schedule')),
                  ],
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(child: _metricTile('Reliability Score', displayReliability, 'Historical record')),
                    const SizedBox(width: 8),
                    Expanded(child: _metricTile('Candidates', '${summary!.totalCandidatesEvaluated} Vendors', 'Evaluated pool')),
                  ],
                ),
              ],
            ),
          ),

          // Justification Section
          if (topEval.reasoning.isNotEmpty) ...[
            Container(
              margin: const EdgeInsets.symmetric(horizontal: 12),
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: const Color(0xFFF8FAFC),
                border: Border.all(color: const Color(0xFFE2E8F0)),
                borderRadius: BorderRadius.circular(6),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Row(
                    children: [
                      Text('🤖', style: TextStyle(fontSize: 13)),
                      SizedBox(width: 4),
                      Text(
                        'Executive Recommendation Justification',
                        style: TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: Color(0xFF334155)),
                      ),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    topEval.reasoning,
                    style: const TextStyle(fontSize: 12, color: Color(0xFF475569), height: 1.4),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 10),
          ],

          // Action Button: View Breakdown
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
            child: SizedBox(
              width: double.infinity,
              child: OutlinedButton.icon(
                icon: const Text('📊', style: TextStyle(fontSize: 13)),
                label: const Text('View Scoring Breakdown', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600)),
                style: OutlinedButton.styleFrom(
                  foregroundColor: const Color(0xFF334155),
                  side: const BorderSide(color: Color(0xFFCBD5E1)),
                  padding: const EdgeInsets.symmetric(vertical: 8),
                ),
                onPressed: () => _showBreakdownSheet(context, topEval, vendorDisplayName),
              ),
            ),
          ),
        ],
      ),
    );
  }

  static Widget _metricTile(String label, String value, String sub) {
    return Container(
      padding: const EdgeInsets.all(8),
      decoration: BoxDecoration(
        color: const Color(0xFFF8FAFC),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: const Color(0xFFF1F5F9)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontSize: 10, color: Color(0xFF64748B), fontWeight: FontWeight.w500)),
          const SizedBox(height: 2),
          Text(value, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
          const SizedBox(height: 1),
          Text(sub, style: const TextStyle(fontSize: 9, color: Color(0xFF94A3B8))),
        ],
      ),
    );
  }

  void _showBreakdownSheet(BuildContext context, VendorEvaluationResponseDto evaluation, String vendorName) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) {
        return Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Scoring Breakdown', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                        Text(vendorName, style: const TextStyle(fontSize: 13, color: Color(0xFF64748B))),
                      ],
                    ),
                  ),
                  IconButton(
                    icon: const Icon(Icons.close),
                    onPressed: () => Navigator.pop(ctx),
                  ),
                ],
              ),
              const Divider(),
              if (evaluation.criterionScores.isEmpty) ...[
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 16),
                  child: Center(child: Text('No detailed criterion scores recorded.', style: TextStyle(color: Colors.grey))),
                ),
              ] else ...[
                ...evaluation.criterionScores.map((c) {
                  return Padding(
                    padding: const EdgeInsets.symmetric(vertical: 6),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text(formatStatus(c.criterionName), style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
                            Text('${c.score.toStringAsFixed(1)} / 100 (${(c.weight * 100).toInt()}%)', style: const TextStyle(fontSize: 12, color: Color(0xFF1E3A5F), fontWeight: FontWeight.bold)),
                          ],
                        ),
                        const SizedBox(height: 4),
                        LinearProgressIndicator(
                          value: (c.score / 100).clamp(0.0, 1.0),
                          backgroundColor: const Color(0xFFE2E8F0),
                          valueColor: AlwaysStoppedAnimation<Color>(
                            c.score >= 80
                                ? const Color(0xFF15803D)
                                : c.score >= 60
                                    ? const Color(0xFFD97706)
                                    : const Color(0xFFDC2626),
                          ),
                          minHeight: 6,
                          borderRadius: BorderRadius.circular(3),
                        ),
                      ],
                    ),
                  );
                }),
              ],
              const SizedBox(height: 16),
            ],
          ),
        );
      },
    );
  }
}
