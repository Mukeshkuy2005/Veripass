export interface BuyerActivityUser {
  id: string;
  name: string;
  role: string;
  email: string;
}

export interface BuyerActivityTarget {
  type: string;
  id?: string | null;
  name?: string | null;
}

export interface BuyerActivityOrg {
  id: string;
  name: string;
}

export interface BuyerActivitySecurityContext {
  ipAddress?: string | null;
  device?: string | null;
  session?: string | null;
  requestId?: string | null;
  isRestricted: boolean;
}

export interface BuyerActivityItem {
  id: string;
  eventCode: string;
  timestamp: string;
  user: BuyerActivityUser;
  action: string;
  actionLabel: string;
  category: string;
  target: BuyerActivityTarget;
  result: 'SUCCESS' | 'FAILED' | 'DENIED' | string;
  resultLabel: string;
}

export interface BuyerActivityPagedResult {
  items: BuyerActivityItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface BuyerActivitySummary {
  today: number;
  success: number;
  failed: number;
  denied: number;
  total: number;
}

export interface BuyerActivityDetail {
  id: string;
  eventCode: string;
  timestamp: string;
  user: BuyerActivityUser;
  organization: BuyerActivityOrg;
  action: string;
  actionLabel: string;
  category: string;
  target: BuyerActivityTarget;
  result: string;
  resultLabel: string;
  securityContext: BuyerActivitySecurityContext;
  oldValuesJson?: string | null;
  newValuesJson?: string | null;
  metadataJson?: string | null;
}

export interface BuyerActivityFilterState {
  search?: string;
  dateRange: 'all' | 'today' | 'last7' | 'last30' | 'last90' | 'custom';
  from?: string | null;
  to?: string | null;
  userId?: string | null;
  category?: string | null;
  action?: string | null;
  target?: string | null;
  result?: string | null;
  sort?: string;
  page: number;
  pageSize: number;
}

export interface ExportBuyerActivityRequest {
  format: 'CSV' | 'JSON';
  search?: string;
  from?: string | null;
  to?: string | null;
  userId?: string | null;
  category?: string | null;
  action?: string | null;
  target?: string | null;
  result?: string | null;
}
