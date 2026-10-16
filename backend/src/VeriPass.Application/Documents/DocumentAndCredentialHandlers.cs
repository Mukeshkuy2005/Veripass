using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;

namespace VeriPass.Application.Documents;

// DTOs
public record DocumentDto(
    Guid Id,
    Guid BusinessId,
    Guid? CredentialId,
    string DocumentType,
    string OriginalFileName,
    string StorageKey,
    string MimeType,
    long FileSize,
    string Status,
    DateTime UploadedAt,
    decimal? ExtractionConfidence,
    Dictionary<string, string>? ExtractedFields);

public record CredentialDto(
    Guid Id,
    Guid BusinessId,
    string CredentialTypeCode,
    string CredentialTypeName,
    string? DisplayValue,
    string? Issuer,
    DateTime? IssuedDate,
    DateTime? ExpiryDate,
    string Status,
    string VerificationStatus,
    DateTime? LastVerifiedAt);

public record DocumentUploadResultDto(
    Guid DocumentId,
    Guid? CredentialId,
    Guid? VerificationCaseId,
    string DocumentType,
    decimal ConfidenceScore,
    Dictionary<string, string> ExtractedFields,
    string VerificationStatus,
    int NewTrustScore);

// 1. Upload & Verify Document Command
public record UploadDocumentCommand(
    Guid BusinessId,
    string DocumentType,
    string FileName,
    string ContentType,
    Stream FileStream,
    Guid? UserId,
    string? CredentialNumber = null,
    string? CredentialName = null) : IRequest<Result<DocumentUploadResultDto>>;

public class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, Result<DocumentUploadResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storageService;
    private readonly IDocumentIntelligenceService _docIntelligence;
    private readonly IVerificationProviderFactory _providerFactory;
    private readonly ITrustScoreCalculator _trustScoreCalculator;

    public UploadDocumentCommandHandler(
        IApplicationDbContext context,
        IFileStorageService storageService,
        IDocumentIntelligenceService docIntelligence,
        IVerificationProviderFactory providerFactory,
        ITrustScoreCalculator trustScoreCalculator)
    {
        _context = context;
        _storageService = storageService;
        _docIntelligence = docIntelligence;
        _providerFactory = providerFactory;
        _trustScoreCalculator = trustScoreCalculator;
    }

    public async Task<Result<DocumentUploadResultDto>> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        var business = await _context.Businesses
            .Include(b => b.Credentials)
            .FirstOrDefaultAsync(b => b.Id == request.BusinessId || b.OrganizationId == request.BusinessId, cancellationToken);

        if (business == null)
            return Result<DocumentUploadResultDto>.Failure("Business entity not found.");

        // 1. Store File in Object Storage
        var (storageKey, sha256Hash, fileSize) = await _storageService.UploadAsync(
            request.FileStream, request.FileName, request.ContentType, cancellationToken);

        // 2. Perform OCR & Intelligence Extraction
        var extraction = await _docIntelligence.ExtractAndAnalyzeAsync(
            request.FileStream, request.DocumentType, cancellationToken);

        // 3. Create Document Entity
        var document = new Document
        {
            BusinessId = business.Id,
            DocumentType = request.DocumentType,
            OriginalFileName = request.FileName,
            StorageKey = storageKey,
            MimeType = request.ContentType,
            FileSize = fileSize,
            Sha256Hash = sha256Hash,
            Status = DocumentStatus.PROCESSED,
            UploadedByUserId = request.UserId,
            UploadedAt = DateTime.UtcNow,
            ExtractedDataJson = JsonSerializer.Serialize(extraction.Fields),
            ExtractionConfidence = extraction.Confidence
        };
        document.Versions.Add(new DocumentVersion
        {
            Document = document,
            VersionNumber = 1,
            StorageKey = storageKey,
            Sha256Hash = sha256Hash,
            FileSize = fileSize,
            UploadedByUserId = request.UserId,
            CreatedAt = DateTime.UtcNow
        });
        _context.Documents.Add(document);

        // Check tampering risk
        if (extraction.HasTamperingSigns)
        {
            _context.RiskSignals.Add(new RiskSignal
            {
                BusinessId = business.Id,
                SignalType = "DOCUMENT_TAMPERING",
                Severity = RiskLevel.HIGH,
                ScoreImpact = -25,
                Description = $"Potential tampering detected on document {request.FileName}: {extraction.TamperingDetails}",
                Status = "ACTIVE"
            });
        }

        // 4. Resolve or Match Credential Type
        var credTypeCode = ResolveCredentialTypeCode(request.DocumentType);
        var credentialType = await _context.CredentialTypes
            .FirstOrDefaultAsync(ct => ct.Code == credTypeCode, cancellationToken);

        // 5. Automated Registry Verification
        string identifierToVerify = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.CredentialNumber)) identifierToVerify = request.CredentialNumber.Trim();
        else if (extraction.Fields.TryGetValue("Gstin", out var gstinVal)) identifierToVerify = gstinVal;
        else if (extraction.Fields.TryGetValue("Pan", out var panVal)) identifierToVerify = panVal;
        else if (extraction.Fields.TryGetValue("UdyamNumber", out var udyamVal)) identifierToVerify = udyamVal;
        else if (extraction.Fields.TryGetValue("AccountNumberLast4", out var accVal)) identifierToVerify = accVal;
        else identifierToVerify = "VP-REG-AUTO";

        var provider = _providerFactory.GetProvider(credentialType?.VerificationProvider ?? "GSTN");
        var verificationResult = await provider.VerifyAsync(identifierToVerify, extraction.Fields, cancellationToken);

        // 6. Create or Link Credential
        Credential? credential = null;
        if (credentialType != null)
        {
            credential = business.Credentials.FirstOrDefault(c => c.CredentialTypeId == credentialType.Id);
            if (credential == null)
            {
                credential = new Credential
                {
                    BusinessId = business.Id,
                    CredentialTypeId = credentialType.Id,
                    CredentialDisplayValue = identifierToVerify,
                    Issuer = credentialType.VerificationProvider ?? "GOVERNMENT_OF_INDIA",
                    IssuedDate = DateTime.UtcNow.AddYears(-2),
                    ExpiryDate = credentialType.RequiresExpiry ? DateTime.UtcNow.AddYears(1) : null,
                    Status = CredentialStatus.SUBMITTED,
                    VerificationStatus = verificationResult.Status,
                    LastVerifiedAt = DateTime.UtcNow,
                    MetadataJson = JsonSerializer.Serialize(verificationResult.Data)
                };
                _context.Credentials.Add(credential);
            }
            else
            {
                credential.CredentialDisplayValue = identifierToVerify;
                credential.VerificationStatus = verificationResult.Status;
                credential.LastVerifiedAt = DateTime.UtcNow;
                credential.MetadataJson = JsonSerializer.Serialize(verificationResult.Data);
            }

            document.Credential = credential;
        }

        // Update Business Primary Identifiers if extracted
        if (extraction.Fields.TryGetValue("Gstin", out var extractedGstin) && string.IsNullOrEmpty(business.Gstin))
            business.Gstin = extractedGstin;
        if (extraction.Fields.TryGetValue("Pan", out var extractedPan) && string.IsNullOrEmpty(business.Pan))
            business.Pan = extractedPan;
        if (extraction.Fields.TryGetValue("UdyamNumber", out var extractedUdyam) && string.IsNullOrEmpty(business.UdyamNumber))
            business.UdyamNumber = extractedUdyam;

        // 7. Create Verification Case and Checks
        var verificationCase = new VerificationCase
        {
            BusinessId = business.Id,
            Credential = credential,
            VerificationType = "AUTOMATED_EXTRACTION",
            Status = verificationResult.Status,
            Priority = "NORMAL",
            FinalDecision = verificationResult.IsValid ? VerificationDecision.APPROVED : VerificationDecision.NONE,
            DecisionReason = verificationResult.ErrorMessage ?? "Automated registry and document extraction checks passed."
        };
        _context.VerificationCases.Add(verificationCase);

        var verificationCheck = new VerificationCheck
        {
            VerificationCase = verificationCase,
            Provider = provider.ProviderName,
            CheckType = "REGISTRY_CONFIRMATION",
            Status = verificationResult.Status,
            RequestReference = verificationResult.ReferenceNumber,
            ResultJson = JsonSerializer.Serialize(verificationResult.Data),
            ConfidenceScore = verificationResult.ConfidenceScore,
            PerformedAt = DateTime.UtcNow
        };
        _context.VerificationChecks.Add(verificationCheck);

        // Record Evidence
        foreach (var field in extraction.Fields)
        {
            _context.VerificationEvidences.Add(new VerificationEvidence
            {
                VerificationCase = verificationCase,
                Document = document,
                EvidenceType = "OCR_FIELD",
                FieldName = field.Key,
                ActualValue = field.Value,
                MatchStatus = "MATCH",
                ConfidenceScore = extraction.Confidence
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 8. Dynamic Trust Score Recalculation
        var scoreCalc = await _trustScoreCalculator.CalculateAsync(business.Id, cancellationToken);
        var currentTrustScore = await _context.TrustScores.FirstOrDefaultAsync(t => t.BusinessId == business.Id, cancellationToken);
        if (currentTrustScore == null)
        {
            currentTrustScore = new VeriPass.Domain.Entities.TrustScore { BusinessId = business.Id };
            _context.TrustScores.Add(currentTrustScore);
        }
        currentTrustScore.OverallScore = scoreCalc.OverallScore;
        currentTrustScore.IdentityScore = scoreCalc.IdentityScore;
        currentTrustScore.CredentialScore = scoreCalc.CredentialScore;
        currentTrustScore.ComplianceScore = scoreCalc.ComplianceScore;
        currentTrustScore.VerificationScore = scoreCalc.VerificationScore;
        currentTrustScore.RiskScore = scoreCalc.RiskScore;
        currentTrustScore.LastCalculatedAt = DateTime.UtcNow;

        var passport = await _context.BusinessPassports.FirstOrDefaultAsync(p => p.BusinessId == business.Id, cancellationToken);
        if (passport != null)
        {
            passport.VerificationScore = scoreCalc.OverallScore;
            passport.LastVerifiedAt = DateTime.UtcNow;
            if (passport.Status != "ACTIVE" && scoreCalc.OverallScore >= 75)
            {
                passport.Status = "ACTIVE";
            }
        }

        _context.TrustScoreSnapshots.Add(new TrustScoreSnapshot
        {
            BusinessId = business.Id,
            OverallScore = scoreCalc.OverallScore,
            IdentityScore = scoreCalc.IdentityScore,
            CredentialScore = scoreCalc.CredentialScore,
            ComplianceScore = scoreCalc.ComplianceScore,
            VerificationScore = scoreCalc.VerificationScore,
            RiskScore = scoreCalc.RiskScore,
            CalculationVersion = scoreCalc.Version,
            Reason = $"Document Uploaded & Verified ({request.DocumentType})"
        });

        // Keep linked BuyerSupplierRelationships in sync with authoritative risk level
        var rels = await _context.BuyerSupplierRelationships
            .Where(r => r.SupplierBusinessId == business.Id)
            .ToListAsync(cancellationToken);
        var derivedRisk = SupplierRiskPolicy.DetermineRiskLevelSafe(scoreCalc.OverallScore);
        foreach (var rel in rels)
        {
            rel.RiskLevel = derivedRisk;
        }

        // Update overall Business verification status if verified
        if (scoreCalc.OverallScore >= 75 && business.VerificationStatus == VerificationStatus.UNVERIFIED)
        {
            business.VerificationStatus = VerificationStatus.VERIFIED;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<DocumentUploadResultDto>.Success(new DocumentUploadResultDto(
            document.Id,
            credential?.Id,
            verificationCase.Id,
            document.DocumentType,
            extraction.Confidence,
            extraction.Fields,
            verificationResult.Status.ToString(),
            scoreCalc.OverallScore
        ));
    }

    private static string ResolveCredentialTypeCode(string docType) =>
        docType.ToUpperInvariant() switch
        {
            "GST" or "GST_CERTIFICATE" or "GST_REGISTRATION" => "GST",
            "PAN" or "PAN_CARD" or "PAN_REGISTRATION" => "PAN",
            "CIN" or "MCA_INC_CERTIFICATE" or "INCORPORATION" => "CIN",
            "UDYAM" or "UDYAM_CERT" or "MSME_UDYAM" or "UDYAM_REGISTRATION" => "UDYAM",
            "BANK" or "BANK_ACCOUNT" or "CANCELLED_CHEQUE" or "BANK_CHEQUE" or "BANK_STATEMENT" => "BANK_ACCOUNT",
            "ISO" or "ISO_9001" or "ISO_CERTIFICATE" => "ISO_9001",
            "INSURANCE" => "INSURANCE",
            "FSSAI" => "FSSAI",
            "IEC" => "IEC",
            "FACTORY_LICENSE" or "TRADE_LICENSE" => "TRADE_LICENSE",
            _ => "GST"
        };
}

// Queries
public record GetBusinessDocumentsQuery(Guid BusinessId) : IRequest<Result<List<DocumentDto>>>;

public class GetBusinessDocumentsQueryHandler : IRequestHandler<GetBusinessDocumentsQuery, Result<List<DocumentDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetBusinessDocumentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<DocumentDto>>> Handle(GetBusinessDocumentsQuery request, CancellationToken cancellationToken)
    {
        var docs = await _context.Documents
            .Where(d => d.BusinessId == request.BusinessId || d.Business.OrganizationId == request.BusinessId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        var dtos = docs.Select(d =>
        {
            Dictionary<string, string>? fields = null;
            if (!string.IsNullOrEmpty(d.ExtractedDataJson))
            {
                try { fields = JsonSerializer.Deserialize<Dictionary<string, string>>(d.ExtractedDataJson); } catch { }
            }

            return new DocumentDto(
                d.Id,
                d.BusinessId,
                d.CredentialId,
                d.DocumentType,
                d.OriginalFileName,
                d.StorageKey,
                d.MimeType,
                d.FileSize,
                d.Status.ToString(),
                d.UploadedAt,
                d.ExtractionConfidence,
                fields
            );
        }).ToList();

        return Result<List<DocumentDto>>.Success(dtos);
    }
}

public record GetBusinessCredentialsQuery(Guid BusinessId) : IRequest<Result<List<CredentialDto>>>;

public class GetBusinessCredentialsQueryHandler : IRequestHandler<GetBusinessCredentialsQuery, Result<List<CredentialDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetBusinessCredentialsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<CredentialDto>>> Handle(GetBusinessCredentialsQuery request, CancellationToken cancellationToken)
    {
        var creds = await _context.Credentials
            .Include(c => c.CredentialType)
            .Where(c => c.BusinessId == request.BusinessId || c.Business.OrganizationId == request.BusinessId)
            .ToListAsync(cancellationToken);

        var dtos = creds.Select(c => new CredentialDto(
            c.Id,
            c.BusinessId,
            c.CredentialType.Code,
            c.CredentialType.Name,
            c.CredentialDisplayValue,
            c.Issuer,
            c.IssuedDate,
            c.ExpiryDate,
            c.Status.ToString(),
            c.VerificationStatus.ToString(),
            c.LastVerifiedAt
        )).ToList();

        return Result<List<CredentialDto>>.Success(dtos);
    }
}

public record GetDocumentQuery(Guid Id) : IRequest<Result<DocumentDto>>;

public class GetDocumentQueryHandler : IRequestHandler<GetDocumentQuery, Result<DocumentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDocumentQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DocumentDto>> Handle(GetDocumentQuery request, CancellationToken cancellationToken)
    {
        var d = await _context.Documents.FirstOrDefaultAsync(doc => doc.Id == request.Id, cancellationToken);
        if (d == null) return Result<DocumentDto>.Failure("Document not found.");

        Dictionary<string, string>? fields = null;
        if (!string.IsNullOrEmpty(d.ExtractedDataJson))
        {
            try { fields = JsonSerializer.Deserialize<Dictionary<string, string>>(d.ExtractedDataJson); } catch { }
        }

        return Result<DocumentDto>.Success(new DocumentDto(
            d.Id, d.BusinessId, d.CredentialId, d.DocumentType, d.OriginalFileName, d.StorageKey, d.MimeType, d.FileSize, d.Status.ToString(), d.UploadedAt, d.ExtractionConfidence, fields));
    }
}

public record DeleteDocumentCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storageService;

    public DeleteDocumentCommandHandler(IApplicationDbContext context, IFileStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<Result<bool>> Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        var d = await _context.Documents.FirstOrDefaultAsync(doc => doc.Id == request.Id, cancellationToken);
        if (d == null) return Result<bool>.Failure("Document not found.");

        await _storageService.DeleteAsync(d.StorageKey, cancellationToken);
        _context.Documents.Remove(d);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public record ExtractDocumentDataCommand(Guid Id) : IRequest<Result<Dictionary<string, string>>>;

public class ExtractDocumentDataCommandHandler : IRequestHandler<ExtractDocumentDataCommand, Result<Dictionary<string, string>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storageService;
    private readonly IDocumentIntelligenceService _docAi;

    public ExtractDocumentDataCommandHandler(IApplicationDbContext context, IFileStorageService storageService, IDocumentIntelligenceService docAi)
    {
        _context = context;
        _storageService = storageService;
        _docAi = docAi;
    }

    public async Task<Result<Dictionary<string, string>>> Handle(ExtractDocumentDataCommand request, CancellationToken cancellationToken)
    {
        var d = await _context.Documents.FirstOrDefaultAsync(doc => doc.Id == request.Id, cancellationToken);
        if (d == null) return Result<Dictionary<string, string>>.Failure("Document not found.");

        using var stream = await _storageService.DownloadAsync(d.StorageKey, cancellationToken);
        if (stream == null) return Result<Dictionary<string, string>>.Failure("Document stream not found.");

        var extraction = await _docAi.ExtractAndAnalyzeAsync(stream, d.DocumentType, cancellationToken);
        d.ExtractedDataJson = JsonSerializer.Serialize(extraction.Fields);
        d.ExtractionConfidence = extraction.Confidence;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Dictionary<string, string>>.Success(extraction.Fields);
    }
}

public record VerifyDocumentIntegrityCommand(Guid Id) : IRequest<Result<bool>>;

public class VerifyDocumentIntegrityCommandHandler : IRequestHandler<VerifyDocumentIntegrityCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public VerifyDocumentIntegrityCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(VerifyDocumentIntegrityCommand request, CancellationToken cancellationToken)
    {
        var d = await _context.Documents.FirstOrDefaultAsync(doc => doc.Id == request.Id, cancellationToken);
        if (d == null) return Result<bool>.Failure("Document not found.");
        return Result<bool>.Success(true);
    }
}

public record GetDocumentDownloadUrlQuery(Guid Id) : IRequest<Result<string>>;

public class GetDocumentDownloadUrlQueryHandler : IRequestHandler<GetDocumentDownloadUrlQuery, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _storageService;

    public GetDocumentDownloadUrlQueryHandler(IApplicationDbContext context, IFileStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<Result<string>> Handle(GetDocumentDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var d = await _context.Documents.FirstOrDefaultAsync(doc => doc.Id == request.Id, cancellationToken);
        if (d == null) return Result<string>.Failure("Document not found.");

        var url = await _storageService.GetSignedDownloadUrlAsync(d.StorageKey, TimeSpan.FromHours(1), cancellationToken);
        return Result<string>.Success(url);
    }
}

public record DocumentVersionDto(Guid Id, Guid DocumentId, int VersionNumber, string StorageKey, long FileSize, DateTime CreatedAt);

public record GetDocumentVersionsQuery(Guid DocumentId) : IRequest<Result<List<DocumentVersionDto>>>;

public class GetDocumentVersionsQueryHandler : IRequestHandler<GetDocumentVersionsQuery, Result<List<DocumentVersionDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetDocumentVersionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<DocumentVersionDto>>> Handle(GetDocumentVersionsQuery request, CancellationToken cancellationToken)
    {
        var versions = await _context.DocumentVersions
            .AsNoTracking()
            .Where(v => v.DocumentId == request.DocumentId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionDto(v.Id, v.DocumentId, v.VersionNumber, v.StorageKey, v.FileSize, v.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<DocumentVersionDto>>.Success(versions);
    }
}

