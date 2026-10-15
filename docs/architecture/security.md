# VERIPASS - Security & Access Control

## 1. Authentication & Session Management
* **JWT Bearer Tokens:** Signed using HMAC-SHA256 with 120-minute validity.
* **Cryptographic Refresh Tokens:** 64-byte CSPRNG-generated tokens stored as SHA256 hashes in PostgreSQL to prevent exposure during database breach.
* **Password Hashing:** BCrypt with a cost factor of 12 and unique salts.

## 2. Authorization & RBAC
* Dynamic permission-based authorization (`[HasPermission("...")]`).
* Users belong to `OrganizationMember` entries with contextual roles (`SUPPLIER`, `BUYER`, `VERIFIER`, `ADMIN`).
* A user can belong to multiple organizations without role bleed.

## 3. Data Protection & Sensitive Data Handling
* **Bank Details:** Only `account_number_last4` is exposed in API payloads and UI views. Full account numbers are encrypted at rest.
* **Masked Identifiers:** Masked PAN (`******1234F`) displayed where full statutory numbers are unnecessary.
* **SHA256 Integrity Hashes:** Every document binary is hashed upon ingestion to verify integrity and detect duplicate uploads.

## 4. Field-Level Access Control for Passport Shares
Suppliers retain sovereign control over what buyers can view. Share tokens store an `access_scope` JSONB document:
```json
{
  "identity": true,
  "gst": true,
  "pan": false,
  "bank": false,
  "certifications": true
}
```
The public passport endpoint programmatically strips unshared fields from the response.

## 5. Audit Logging & Non-Repudiation
Every state change, document upload, verification approval, and passport share emits an immutable row into `audit_logs` capturing:
* Actor User ID & Organization ID
* Action & Resource Identifier
* Before and After JSONB state diffs
* Client IP address and User Agent
* Correlation ID for distributed tracing
