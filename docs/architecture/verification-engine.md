# VERIPASS - Verification Engine & Trust Score Formulation

## The Core Philosophy
A document is merely an artifact; compliance is a continuous state. The VERIPASS verification engine does not simply archive PDFs—it continuously validates statutory facts against authoritative sovereign and institutional registries.

## The Verification & Extraction Pipeline
```
Supplier Uploads PDF / Image
         │
         ▼
[File Storage + SHA256 Hashing]
         │
         ▼
[Document Intelligence & OCR]
   - Parse Indian standard formats (GST REG-06, PAN, Udyam, Cheques, ISO 9001)
   - Perform layout & tamper detection analysis
   - Output structured key-value payload with confidence %
         │
         ▼
[Registry Verification Adapters]
   - GSTN: Verify active status, 6-month filing discipline (GSTR-1 & 3B), ITC eligibility
   - NSDL: Verify PAN validity, entity type, and Section 206AB TDS status
   - MCA21: Validate CIN, RoC jurisdiction, active charges, and director DINs
   - UDYAM: Check MSME classification (Micro, Small, Medium) & NIC code
   - NPCI Penny Drop: Match beneficiary account name against legal business name
         │
         ▼
[Verification Case Resolution]
   - Record matched evidence & discrepancies
   - Flag risk signals if tampering or identity mismatch is detected
         │
         ▼
[Deterministic Trust Score Calculation]
```

## Deterministic Trust Score Formulation (0–100)
Unlike arbitrary credit scores, the VERIPASS score is deterministic, reproducible, and explainable:

$$\text{Trust Score} = (0.25 \times \text{Identity}) + (0.25 \times \text{Credentials}) + (0.20 \times \text{Compliance}) + (0.20 \times \text{Verification}) - \text{Risk Penalties}$$

### Score Pillars:
1. **Identity (25%)**: Valid PAN, active GSTIN, CIN/Udyam registration, active registered address, and valid directors.
2. **Credentials (25%)**: Statutory credential completeness, active validity, and non-expired certificates.
3. **Compliance (20%)**: Absence of unresolved statutory alerts, filing defaults, or approaching expirations.
4. **Verification (20%)**: Percentage of credentials successfully verified against authoritative primary registries.
5. **Risk Penalties (10%)**: Deductions incurred for unresolved tamper warnings, name mismatches, or disputed claims.

Every calculation generates an immutable `trust_score_snapshots` entry, enabling the historical trust timeline.
