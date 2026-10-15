export interface VerifierProfileData {
  userId: string;
  verifierId: string;
  fullName: string;
  email: string;
  role: string;
  organization: string;
  status: 'ACTIVE' | 'INACTIVE' | 'SUSPENDED' | string;
  phone?: string;
}

export interface VerifierScope {
  regions: string[];
  verificationCategories: string[];
}

export interface VerifierSecurity {
  mfaEnabled: boolean;
  lastLoginAtUtc?: string;
  activeSessionCount: number;
}

export interface VerifierActivity {
  range: string;
  casesReviewed: number;
  approved: number;
  rejected: number;
  escalated: number;
  averageReviewTimeSeconds: number;
}

export interface VerifierProfileResponse {
  profile: VerifierProfileData;
  scope: VerifierScope;
  security: VerifierSecurity;
  activity: VerifierActivity;
}

export interface VerifierSession {
  id: string;
  device: string;
  browser: string;
  platform: string;
  ipAddress?: string;
  lastActiveAtUtc: string;
  isCurrent: boolean;
  createdAtUtc: string;
}

export interface VerifierLoginHistoryItem {
  id: string;
  timestampUtc: string;
  action: string;
  status: 'SUCCESS' | 'FAILED' | string;
  ipAddress?: string;
  browser: string;
  platform: string;
}
