# VERIPASS - Database Design & Schema

## Engine: PostgreSQL 16 with Native JSONB
PostgreSQL was selected over traditional SQL Server to natively accommodate semi-structured OCR extraction data, external API responses from GSTN and MCA, dynamic access control scopes, and comprehensive audit logs.

## Entity Relationship Topology
```
Users
  │
  ├────────── OrganizationMembers
  │                    │
  │                    ▼
  │              Organizations
  │                    │
  │          ┌─────────┴─────────┐
  │          ▼                   ▼
  │      Businesses          Buyer side
  │          │
  │     ┌────┼──────────────┐
  │     │    │              │
  │     ▼    ▼              ▼
  │ Directors BankAccounts Credentials
  │                              │
  │                              ▼
  │                         Documents
  │                              │
  │                              ▼
  │                       VerificationCases
  │                              │
  │                    ┌─────────┼──────────┐
  │                    ▼         ▼          ▼
  │             VerificationChecks RiskSignals Evidence
  │                    │
  │                    ▼
  │              TrustScoreSnapshots
  │
  └── Notifications
```

## Core Relational Tables
1. **organizations**: Tenant/enterprise entity (`type = BUYER | SUPPLIER | BOTH`).
2. **users**: User identity with password hashing (BCrypt).
3. **organization_members**: Multi-tenant RBAC mapping users, organizations, and roles.
4. **roles & permissions**: Fine-grained capability matrix.
5. **businesses**: Verified legal entity anchored on PAN, GSTIN, CIN, and Udyam.
6. **business_addresses**: Multi-address registry (Registered, Principal, Factory, Branch).
7. **directors**: Board members, designation, and DIN numbers.
8. **bank_accounts**: Financial settlement accounts verified via NPCI Penny Drop.
9. **credential_types**: Extensible statutory types (GST, PAN, CIN, UDYAM, ISO, FSSAI, IEC, etc.).
10. **credentials**: Statutory credentials with verification timestamps and JSONB metadata.
11. **documents & document_versions**: Metadata, SHA256 checksums, and OCR extraction JSONB.
12. **verification_cases & checks**: Multi-stage verification workflows.
13. **trust_scores & snapshots**: Deterministic compliance rating (0-100) with historical timeline.
14. **compliance_events**: Expiration alerts, GST suspensions, and reverification triggers.
15. **business_passports & passport_shares**: Shareable digital identity with field-level ACLs.
16. **buyer_supplier_relationships & shortlists**: Enterprise vendor management network.
17. **audit_logs**: Immutable forensic records with JSONB diff snapshots.
