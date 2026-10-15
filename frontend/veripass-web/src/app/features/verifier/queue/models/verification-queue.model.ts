export interface VerificationQueueAssignee {
  id: string;
  name: string;
}

export interface VerificationQueueSla {
  dueAt: string;
  isOverdue: boolean;
  remainingMinutes: number;
  formattedRemaining: string;
}

export interface VerificationQueueItem {
  id: string;
  caseNumber: string;
  businessId: string;
  businessName: string;
  businessIdentifier?: string;
  verificationType: string;
  submittedAt: string;
  riskLevel: 'LOW' | 'MEDIUM' | 'HIGH';
  riskScore: number;
  priority: 'HIGH' | 'MEDIUM' | 'LOW';
  assignedTo?: VerificationQueueAssignee | null;
  sla: VerificationQueueSla;
  status: 'PENDING' | 'IN_REVIEW' | 'MORE_INFO_REQUIRED' | 'APPROVED' | 'REJECTED' | 'ESCALATED' | 'CANCELLED';
  actionLabel: string;
}

export interface VerificationQueueSummary {
  totalWaiting: number;
  myCasesCount: number;
  inReviewCount: number;
  actionableCount: number;
}

export interface VerificationQueueResponse {
  items: VerificationQueueItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  summary: VerificationQueueSummary;
}

export interface VerificationQueueFilterParams {
  scope: 'my' | 'all';
  search?: string;
  status?: string;
  priority?: string;
  type?: string;
  risk?: string;
  dateRange?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}
