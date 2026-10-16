export interface AuthResponseDto {
  accessToken: string;
  refreshToken: string;
  userId: string;
  email: string;
  fullName: string;
  organizationId?: string;
  organizationName?: string;
  role: string;
  permissions: string[];
  mustChangePassword?: boolean;
  requiresMfa?: boolean;
  mfaMethod?: string;
  mfaTicket?: string;
}

export interface UserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName?: string;
  phone?: string;
  companyName?: string;
  organizationName?: string;
  businessType?: string;
  currentOrganizationId?: string;
  role?: string;
  permissions: string[];
  mustChangePassword?: boolean;
}

export interface BusinessProfileDto {
  id: string;
  organizationId: string;
  businessIdNumber: string;
  legalName: string;
  tradeName?: string;
  businessType?: string;
  incorporationDate?: string;
  pan?: string;
  gstin?: string;
  cin?: string;
  udyamNumber?: string;
  website?: string;
  employeeCount?: number;
  description?: string;
  verificationStatus: string;
  trustScore: number;
  addresses: AddressDto[];
  directors: DirectorDto[];
  bankAccounts: BankAccountDto[];
}

export interface AddressDto {
  id: string;
  addressType: string;
  line1: string;
  line2?: string;
  city: string;
  district?: string;
  state: string;
  pincode: string;
  country: string;
  isPrimary: boolean;
  verificationStatus: string;
}

export interface DirectorDto {
  id: string;
  fullName: string;
  designation: string;
  din?: string;
  panMasked?: string;
  appointmentDate?: string;
  status: string;
}

export interface BankAccountDto {
  id: string;
  accountHolderName: string;
  bankName: string;
  accountNumberLast4: string;
  ifsc: string;
  accountType: string;
  verificationStatus: string;
  verifiedAt?: string;
}

export interface BusinessDashboardDto {
  businessId: string;
  businessName: string;
  businessIdNumber: string;
  overallTrustScore: number;
  identityScore: number;
  credentialScore: number;
  complianceScore: number;
  verificationScore: number;
  riskScore: number;
  verificationStatus: string;
  totalCredentials: number;
  verifiedCredentials: number;
  pendingVerificationCount: number;
  activeAlertsCount: number;
  connectedBuyersCount: number;
  recentActivities: RecentActivityDto[];
}

export interface RecentActivityDto {
  title: string;
  description: string;
  timestamp: string;
  type: string;
}

export interface DocumentDto {
  id: string;
  businessId: string;
  credentialId?: string;
  documentType: string;
  originalFileName: string;
  storageKey: string;
  mimeType: string;
  fileSize: number;
  status: string;
  uploadedAt: string;
  extractionConfidence?: number;
  extractedFields?: Record<string, string>;
}

export interface CredentialDto {
  id: string;
  businessId: string;
  credentialTypeCode: string;
  credentialTypeName: string;
  displayValue?: string;
  issuer?: string;
  issuedDate?: string;
  expiryDate?: string;
  status: string;
  verificationStatus: string;
  lastVerifiedAt?: string;
}

export interface DocumentUploadResultDto {
  documentId: string;
  credentialId?: string;
  verificationCaseId?: string;
  documentType: string;
  confidenceScore: number;
  extractedFields: Record<string, string>;
  verificationStatus: string;
  newTrustScore: number;
}

export interface TrustScoreDetailDto {
  businessId: string;
  overallScore: number;
  identityScore: number;
  credentialScore: number;
  complianceScore: number;
  verificationScore: number;
  riskScore: number;
  lastCalculatedAt: string;
  version: string;
  history: TrustScoreSnapshotDto[];
}

export interface TrustScoreSnapshotDto {
  overallScore: number;
  identityScore: number;
  credentialScore: number;
  complianceScore: number;
  verificationScore: number;
  riskScore: number;
  reason: string;
  createdAt: string;
}

export interface BusinessPassportDto {
  id: string;
  businessId: string;
  businessName: string;
  passportNumber: string;
  publicIdentifier: string;
  version: string;
  status: string;
  verificationScore: number;
  issuedAt?: string;
  expiresAt?: string;
  lastVerifiedAt?: string;
  qrCodeUrl: string;
  verifiedCredentials: PassportCredentialSummaryDto[];
  activeShares: PassportShareDto[];
}

export interface PassportCredentialSummaryDto {
  credentialType: string;
  credentialName: string;
  displayValue?: string;
  verificationStatus: string;
  lastVerifiedAt?: string;
}

export interface PassportShareDto {
  id: string;
  shareToken: string;
  recipientEmail?: string;
  recipientName?: string;
  accessScope: Record<string, boolean>;
  expiresAt?: string;
  isActive: boolean;
}

export interface PublicPassportDto {
  businessName: string;
  passportNumber: string;
  verificationScore: number;
  status: string;
  lastVerifiedAt?: string;
  publicData: Record<string, any>;
  verifiedBadges: string[];
}

export interface SupplierSearchResultDto {
  businessId: string;
  legalName: string;
  tradeName?: string;
  businessIdNumber: string;
  city?: string;
  state?: string;
  gstin?: string;
  trustScore: number;
  verificationStatus: string;
  verifiedCredentials: string[];
  hasActiveGst: boolean;
  isMsmRegistered: boolean;
}

export interface SupplierCompareDto {
  businessId: string;
  legalName: string;
  tradeName?: string;
  category?: string;
  businessType?: string;
  location?: string;
  state?: string;
  yearsActive?: number;
  verificationStatus?: string;
  businessIdNumber: string;
  overallTrustScore: number;
  identityScore: number;
  credentialScore: number;
  complianceScore: number;
  verificationScore: number;
  riskScore: number;
  gstin?: string;
  pan?: string;
  udyam?: string;
  udyamNumber?: string;
  employeeCount?: number;
  verifiedCredentials: string[];
}

export interface ComplianceEventDto {
  id: string;
  businessId: string;
  eventType: string;
  severity: string;
  title: string;
  description: string;
  dueDate?: string;
  isAcknowledged: boolean;
  createdAt: string;
}

export interface VerificationQueueItemDto {
  caseId: string;
  businessId: string;
  businessName: string;
  businessIdNumber: string;
  credentialCode?: string;
  verificationType: string;
  status: string;
  priority: string;
  createdAt: string;
  reason?: string;
}

export interface VerificationCaseDetailDto {
  caseId: string;
  caseNumber?: string;
  businessId: string;
  businessName: string;
  gstin?: string;
  pan?: string;
  verificationType: string;
  status: string;
  priority: string;
  finalDecision: string;
  decisionReason?: string;
  riskLevel?: string;
  riskScore?: number;
  trustScore?: number;
  assignedVerifierId?: string;
  assignedVerifierName?: string;
  submittedAt?: string;
  startedAt?: string;
  completedAt?: string;
  checks: VerificationCheckDto[];
  evidences: VerificationEvidenceDto[];
}

export interface VerificationCheckDto {
  id: string;
  provider: string;
  checkType: string;
  status: string;
  requestReference?: string;
  confidenceScore?: number;
  performedAt: string;
  errorMessage?: string;
}

export interface VerificationEvidenceDto {
  id: string;
  evidenceType: string;
  fieldName: string;
  expectedValue?: string;
  actualValue?: string;
  matchStatus: string;
  confidenceScore?: number;
}

// Admin Verifier Management Models
export interface AdminVerifierSummaryDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  phone?: string;
  status: 'ACTIVE' | 'INACTIVE' | 'SUSPENDED';
  mustChangePassword: boolean;
  createdAt: string;
  lastLoginAt?: string;
  assignedCasesCount: number;
  completedCasesCount: number;
  pendingCasesCount: number;
}

export interface AdminVerifierStatsDto {
  totalVerifiers: number;
  activeVerifiers: number;
  inactiveVerifiers: number;
  suspendedVerifiers: number;
}

export interface AdminVerifiersPagedResponse {
  items: AdminVerifierSummaryDto[];
  stats: AdminVerifierStatsDto;
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface VerifierAssignedCaseDto {
  caseId: string;
  businessName: string;
  verificationType: string;
  status: string;
  priority: string;
  createdAt: string;
}

export interface VerifierAuditEntryDto {
  id: string;
  action: string;
  details?: string;
  timestamp: string;
}

export interface AdminVerifierDetailDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  phone?: string;
  status: 'ACTIVE' | 'INACTIVE' | 'SUSPENDED';
  mustChangePassword: boolean;
  createdAt: string;
  lastLoginAt?: string;
  totalAssigned: number;
  completedCases: number;
  pendingCases: number;
  recentCases: VerifierAssignedCaseDto[];
  auditHistory: VerifierAuditEntryDto[];
}

export interface CreateVerifierRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  phone?: string;
  mustChangePassword?: boolean;
}

export interface UpdateVerifierRequest {
  firstName: string;
  lastName: string;
  phone?: string;
}

export interface VerificationRequestDto {
  id: string;
  buyerOrganizationId: string;
  buyerOrganizationName: string;
  supplierBusinessId: string;
  supplierBusinessName?: string;
  supplierTradeName?: string;
  supplierCity?: string;
  supplierState?: string;
  supplierGstin?: string;
  supplierTrustScore?: number;
  requestType: string;
  requestedItems: string[];
  reason?: string;
  status: 'PENDING' | 'APPROVED' | 'REJECTED' | 'EXPIRED' | 'CANCELLED';
  supplierResponse?: string;
  respondedAt?: string;
  createdAt: string;
}

export interface CreateVerificationRequestPayload {
  buyerOrganizationId?: string;
  supplierBusinessId: string;
  requestType?: string;
  requestedItems: string[];
  reason?: string;
}

export interface VerificationRequestResponseDto {
  notes?: string;
}

export interface NotificationCategoryPreference {
  id: string;
  name: string;
  description: string;
  badge: 'CRITICAL' | 'STANDARD' | 'INFORMATIONAL';
  email: boolean;
  sms: boolean;
  push: boolean;
  whatsapp: boolean;
}

export interface NotificationChannelsStatus {
  email: boolean;
  sms: boolean;
  push: boolean;
  whatsapp: boolean;
}

export interface NotificationPreferencesDto {
  emailDeliveryAddress: string;
  phoneDeliveryNumber: string;
  digestFrequency: 'INSTANT' | 'DAILY_DIGEST' | 'WEEKLY_SUMMARY';
  quietHoursEnabled: boolean;
  autoEscalateCritical: boolean;
  channels: NotificationChannelsStatus;
  categories: NotificationCategoryPreference[];
}

export type AttributeVisibilityLevel = 'PUBLIC' | 'VERIFIED_BUYERS' | 'PRIVATE';

export interface PrivacyAttributeConfig {
  id: string;
  key?: string;
  name: string;
  category: string;
  visibility: AttributeVisibilityLevel;
  locked?: boolean;
  description: string;
  icon?: string;
}

export interface AllowedBuyerScopes {
  verifiedEnterpriseOnly: boolean;
  explicitInviteOnly: boolean;
  allProcurementDepts: boolean;
}

export interface BusinessPrivacyPreferencesDto {
  profileVisibility: 'PUBLIC' | 'VERIFIED_BUYERS' | 'PRIVATE';
  maskPanPublic: boolean;
  maskBankDetails: boolean;
  autoApproveTier1: boolean;
  requireNdaForAuditReports: boolean;
  searchEngineIndexing: boolean;
  allowDirectBuyerInquiries: boolean;
  allowedBuyerScopes: AllowedBuyerScopes;
  attributes: PrivacyAttributeConfig[];
}

export interface OrganizationMemberDto {
  memberId: string;
  userId: string;
  email: string;
  fullName: string;
  role: string;
  status: 'ACTIVE' | 'INVITED' | 'SUSPENDED';
  createdAt?: string;
  phone?: string;
  department?: string;
}

export interface InviteMemberRequest {
  email: string;
  fullName?: string;
  roleName: string;
  department?: string;
}

export interface OrganizationApiKeyDto {
  id: string;
  name: string;
  keyPrefix: string;
  maskedKey: string;
  fullKey?: string;
  environment: 'PRODUCTION' | 'TEST';
  scopes: string[];
  createdAt: string;
  lastUsedAt?: string | null;
  expiresAt?: string | null;
  status: 'ACTIVE' | 'REVOKED';
  createdByName?: string;
}

export interface CreateApiKeyRequest {
  name: string;
  environment: 'PRODUCTION' | 'TEST';
  scopes: string[];
  expiresInDays?: number | null;
}

export interface BuyerRelationshipDto {
  id: string;
  buyerOrganizationId: string;
  buyerOrganizationName: string;
  supplierBusinessId: string;
  supplierBusinessName: string;
  supplierTradeName?: string;
  supplierCity?: string;
  supplierState?: string;
  supplierGstin?: string;
  supplierTrustScore?: number;
  supplierCategory?: string;
  status: 'INVITED' | 'PENDING' | 'SUSPENDED' | 'APPROVED' | 'REJECTED';
  stage: 'IDENTIFIED' | 'CONTACTED' | 'IN_VERIFICATION' | 'APPROVED' | 'REJECTED';
  relationshipType: string;
  riskLevel: 'LOW' | 'MEDIUM' | 'HIGH';
  erpVendorCode?: string | null;
  requestedBy?: string;
  purpose?: string;
  requiredDocs: { name: string; completed: boolean }[];
  approvedAt?: string | null;
  createdAt: string;
}

export interface InviteSupplierPayload {
  buyerOrganizationId?: string;
  email?: string;
  phone?: string;
  supplierBusinessId?: string;
}

export interface AutomationPolicyDto {
  autoPilotEnabled: boolean;
  minTrustScore: number;
  autoGenerateErpCode: boolean;
  requireGstnMatch: boolean;
  requireCbdtPanMatch: boolean;
  requireBankPennyDrop: boolean;
  requireIsoAccreditation: boolean;
  stpExecutionMode: string;
  policyName: string;
  targetSlaMinutes: number;
  averageCycleTime: string;
  autonomousEmpanelmentRate: number;
  lastAuditSync: string;
}

export interface AutoQualifyResultDto {
  relationshipId: string;
  stage: string;
  status: string;
  erpVendorCode: string;
  checksExecuted: string[];
  executionDurationMs: number;
  timestamp: string;
}

export interface BatchAutoQualifyResultDto {
  totalProcessed: number;
  autoApproved: number;
  exceptionsRequiringReview: number;
  approvedRelationshipIds: string[];
}

// ==========================================
// BUYER ONBOARDING WORKFLOW (ENTERPRISE SPEC)
// ==========================================
export type OnboardingStatus =
  | 'NEW'
  | 'REQUESTED'
  | 'IN_VERIFICATION'
  | 'ACTION_REQUIRED'
  | 'APPROVED'
  | 'REJECTED'
  | 'EXPIRED'
  | 'WITHDRAWN';

export type OnboardingRequirementStatus =
  | 'PENDING'
  | 'SUBMITTED'
  | 'VERIFYING'
  | 'VERIFIED'
  | 'FAILED'
  | 'EXPIRED'
  | 'WAIVED';

export interface OnboardingStatusCounts {
  newCount: number;
  requestedCount: number;
  inVerificationCount: number;
  actionRequiredCount: number;
  approvedCount: number;
  rejectedCount: number;
  expiredCount: number;
  withdrawnCount: number;
  totalActiveCount: number;
}

export interface OnboardingRequestSummary {
  id: string;
  buyerOrganizationId: string;
  buyerOrganizationName: string;
  supplierBusinessId: string;
  supplierBusinessName: string;
  supplierTradeName?: string;
  supplierCity?: string;
  supplierState?: string;
  supplierGstin?: string;
  supplierTrustScore?: number;
  supplierPassportNumber?: string;
  status: OnboardingStatus;
  purpose: string;
  businessCategory: string;
  procurementDepartment: string;
  expectedRelationship: string;
  totalRequirements: number;
  verifiedRequirements: number;
  actionRequiredReason?: string;
  erpVendorCode?: string;
  requestedByName?: string;
  createdAt: string;
  updatedAt?: string;
  expiresAt?: string;
  approvedAt?: string;
  rejectedAt?: string;
}

export interface OnboardingPagedList {
  items: OnboardingRequestSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
  statusCounts: OnboardingStatusCounts;
}

export interface OnboardingRequestRequirement {
  id: string;
  requirementId: string;
  requirementCode: string;
  requirementName: string;
  category: string;
  isMandatory: boolean;
  status: OnboardingRequirementStatus;
  verificationStatus: string;
  verifiedAt?: string;
  verificationSource?: string;
  failureReason?: string;
  evidenceDocumentName?: string;
  requestedAt: string;
  completedAt?: string;
}

export interface OnboardingActivity {
  id: string;
  action: string;
  title: string;
  description?: string;
  actorName?: string;
  timestamp: string;
}

export interface OnboardingRequestDetail {
  id: string;
  buyerOrganizationId: string;
  buyerOrganizationName: string;
  supplierBusinessId: string;
  supplierBusinessName: string;
  legalName: string;
  tradeName?: string;
  gstin?: string;
  pan?: string;
  cin?: string;
  udyamNumber?: string;
  website?: string;
  location?: string;
  industry?: string;
  verificationStatus?: string;
  trustScore?: number;
  passportNumber?: string;
  passportStatus?: string;
  status: OnboardingStatus;
  purpose: string;
  businessCategory: string;
  procurementDepartment: string;
  expectedRelationship: string;
  message?: string;
  createdAt: string;
  updatedAt?: string;
  expiresAt?: string;
  completedAt?: string;
  approvedAt?: string;
  approvedByName?: string;
  rejectedAt?: string;
  rejectedByName?: string;
  rejectionReason?: string;
  actionRequiredReason?: string;
  erpVendorCode?: string;
  paymentTerms?: string;
  targetPlant?: string;
  totalRequirements: number;
  verifiedRequirements: number;
  canApprove: boolean;
  canReject: boolean;
  canWithdraw: boolean;
  canRemind: boolean;
  requirements: OnboardingRequestRequirement[];
  activities: OnboardingActivity[];
}

export interface SupplierSearchItem {
  id: string;
  businessName: string;
  legalName: string;
  tradeName?: string;
  passportId?: string;
  gstinMasked?: string;
  panMasked?: string;
  location?: string;
  industry?: string;
  verificationStatus: string;
  trustScore: number;
}

export interface RequirementCatalogueItem {
  id: string;
  code: string;
  name: string;
  category: string;
  description?: string;
  isDefaultMandatory: boolean;
}

export interface CreateOnboardingRequestPayload {
  supplierBusinessId: string;
  purpose: string;
  businessCategory: string;
  procurementDepartment: string;
  expectedRelationship: string;
  message?: string;
  expiresAt?: string;
  requirements: { requirementCode: string; isMandatory: boolean }[];
  sendImmediately?: boolean;
}

export interface ApproveOnboardingPayload {
  erpVendorCode?: string;
  paymentTerms?: string;
  targetPlant?: string;
}

export interface RejectOnboardingPayload {
  reason: string;
}


