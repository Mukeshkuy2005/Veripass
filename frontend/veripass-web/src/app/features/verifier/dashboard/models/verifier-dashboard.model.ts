export interface VerifierDashboardSummary {
  pending: number;
  pendingTodayDelta: number;
  inReview: number;
  assignedToCurrentVerifier: number;
  approved: number;
  approvedTodayDelta: number;
  rejected: number;
  escalated: number;
}

export interface VerifierPriorityCase {
  id: string;
  priority: 'HIGH' | 'MEDIUM' | 'LOW' | string;
  businessName: string;
  verificationType: string;
  submittedAt: string;
  riskLevel: 'HIGH' | 'MEDIUM' | 'LOW' | string;
  status: string;
  caseNumber?: string;
}

export interface VerifierPerformance {
  casesReviewedToday: number;
  averageReviewTimeSeconds: number;
  approvalRate: number;
  escalationRate: number;
}

export interface VerifierRiskDistribution {
  low: number;
  medium: number;
  high: number;
}

export interface VerifierRecentDecision {
  id: string;
  verificationType: string;
  decision: 'APPROVED' | 'REJECTED' | 'ESCALATED' | string;
  businessName: string;
  timestamp: string;
  verifierName: string;
}

export interface VerifierDashboardResponse {
  summary: VerifierDashboardSummary;
  priorityCases: VerifierPriorityCase[];
  performance: VerifierPerformance;
  riskDistribution: VerifierRiskDistribution;
  recentDecisions: VerifierRecentDecision[];
}
