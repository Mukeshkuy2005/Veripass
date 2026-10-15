# VERIPASS - API Design & Contracts

## Overview
Base URL: `/api/v1`  
Protocol: HTTPS / JSON  
Authentication: `Authorization: Bearer <JWT_TOKEN>`  
Error Format: RFC 7807 Problem Details

## Endpoint Specifications

### 1. Authentication (`/api/v1/auth`)
| Method | Path | Description | Access |
|---|---|---|---|
| `POST` | `/api/v1/auth/register` | Register user and organization | Public |
| `POST` | `/api/v1/auth/login` | Authenticate and obtain JWT + Refresh token | Public |
| `POST` | `/api/v1/auth/refresh` | Rotate and issue new access token | Public |
| `GET` | `/api/v1/auth/me` | Fetch active user, organization, role, and permissions | Authenticated |

### 2. Business Management (`/api/v1/businesses`)
| Method | Path | Description | Access |
|---|---|---|---|
| `GET` | `/api/v1/businesses/{id}` | Get business profile, addresses, directors, bank | Authenticated |
| `PUT` | `/api/v1/businesses/{id}` | Update business identifiers (PAN, GSTIN, CIN, Udyam) | Authenticated |
| `GET` | `/api/v1/businesses/{id}/dashboard` | Real-time analytics, trust score, and active alerts | Authenticated |
| `POST` | `/api/v1/businesses/{id}/address` | Add registered, factory, or branch address | Authenticated |
| `GET` | `/api/v1/businesses/{id}/documents` | Retrieve uploaded documents with OCR metadata | Authenticated |
| `POST` | `/api/v1/businesses/{id}/documents` | Multipart document upload with OCR & auto-verification | Authenticated |
| `GET` | `/api/v1/businesses/{id}/credentials` | List verified statutory credentials | Authenticated |
| `GET` | `/api/v1/businesses/{id}/passport` | Retrieve business passport and share tokens | Authenticated |
| `POST` | `/api/v1/businesses/{id}/passport/generate` | Generate passport number and public identifier | Authenticated |
| `GET` | `/api/v1/businesses/{id}/trust-score` | Breakdown of 5 score pillars & historical timeline | Authenticated |
| `GET` | `/api/v1/businesses/{id}/compliance` | Unresolved compliance events & expiry deadlines | Authenticated |

### 3. Verification & Compliance Operations (`/api/v1/verifications`)
| Method | Path | Description | Access |
|---|---|---|---|
| `GET` | `/api/v1/verifications/queue` | Verifier review queue filtered by status and priority | Verifier / Admin |
| `GET` | `/api/v1/verifications/{id}` | Full case with OCR evidence vs. registry match | Verifier / Admin |
| `POST` | `/api/v1/verifications/{id}/approve` | Approve verification case and update trust score | Verifier / Admin |
| `POST` | `/api/v1/verifications/{id}/reject` | Reject case with mandatory reason | Verifier / Admin |

### 4. Buyer & Supplier Network (`/api/v1/suppliers`, `/api/v1/relationships`, `/api/v1/shortlists`)
| Method | Path | Description | Access |
|---|---|---|---|
| `GET` | `/api/v1/suppliers/search` | Multi-facet search (Trust score, GST, state, industry) | Buyer / Admin |
| `POST` | `/api/v1/suppliers/compare` | Side-by-side comparison of multiple suppliers | Buyer / Admin |
| `GET` | `/api/v1/shortlists` | Buyer supplier shortlists | Buyer |
| `POST` | `/api/v1/shortlists/{id}/suppliers/{bId}` | Add supplier to shortlist | Buyer |
| `GET` | `/api/v1/relationships` | Active and pending buyer-supplier relationships | Authenticated |
| `POST` | `/api/v1/relationships/invite` | Invite supplier to onboard with buyer | Buyer |
| `PUT` | `/api/v1/relationships/{id}/status` | Approve, reject, or suspend supplier relationship | Buyer / Admin |
| `POST` | `/api/v1/verification-requests` | Request missing documents from supplier | Buyer |

### 5. Public Passport & Verification (`/api/v1/public`)
| Method | Path | Description | Access |
|---|---|---|---|
| `GET` | `/api/v1/public/passports/{identifier}` | View verified passport with granular share scope | Public |
| `GET` | `/api/v1/public/passports/{identifier}/qr` | Generate dynamic SVG QR code for verification | Public |
