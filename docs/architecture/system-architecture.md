# VERIPASS - System Architecture

## Overview
VERIPASS is India's Business Compliance & Vendor Identity Network. It replaces manual, point-in-time document collection with continuous statutory verification and a portable digital passport.

## Architectural Model: Modular Monolith with Clean Architecture
```
                         ┌──────────────────────────┐
                         │      ANGULAR 19 PWA      │
                         │ Public │ Supplier │ Buyer│
                         │ Verifier      │ Admin    │
                         └────────────┬─────────────┘
                                      │ HTTPS/JSON (/api/v1)
                                      ▼
                         ┌──────────────────────────┐
                         │     VeriPass.API         │
                         │ Controllers, Middleware, │
                         │ Permission-based Auth    │
                         └────────────┬─────────────┘
                                      ▼
                         ┌──────────────────────────┐
                         │   VeriPass.Application   │
                         │ CQRS (Commands & Queries)│
                         │ MediatR, FluentValidation│
                         └────────────┬─────────────┘
                                      ▼
                         ┌──────────────────────────┐
                         │      VeriPass.Domain     │
                         │ Entities, ValueObjects,  │
                         │ Enums, Domain Events     │
                         └────────────▲─────────────┘
                                      │
                         ┌────────────┴─────────────┐
                         │  VeriPass.Infrastructure │
                         │ EF Core (Npgsql + JSONB) │
                         │ Verification Providers   │
                         │ Doc Intelligence / OCR   │
                         │ Background Worker Jobs   │
                         └──────────────────────────┘
```

## Layer Responsibilities
1. **VeriPass.Domain**:
   - Holds core business objects, Indian statutory value objects (`Gstin`, `PanNumber`, `CinNumber`, `UdyamNumber`), domain events, and domain rules.
   - Zero external library dependencies.
2. **VeriPass.Application**:
   - Implements CQRS using MediatR.
   - Orchestrates use cases (Auth, Business Onboarding, Document Upload, Case Verification, Passport Issuance, Continuous Compliance).
   - Validates input using FluentValidation pipeline behaviors.
3. **VeriPass.Infrastructure**:
   - Implements data access using Entity Framework Core 8 with PostgreSQL.
   - Stores raw unstructured JSON data in native PostgreSQL `JSONB` columns.
   - Implements Indian statutory verification providers (GSTN, NSDL, MCA21, Udyam, NPCI Penny Drop).
   - Background service `ContinuousVerificationWorker` continuously checks credential expiries and synchronizes trust scores.
4. **VeriPass.API**:
   - Exposes RESTful endpoints under `/api/v1/*`.
   - Secures endpoints using JWT Bearer tokens and permission-based RBAC.
   - Global exception handling and correlation ID tracking.
