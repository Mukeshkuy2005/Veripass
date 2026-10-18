using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VeriPass.Application.Businesses;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.Documents;
using VeriPass.Application.Passport;
using VeriPass.Application.Relationships;
using VeriPass.Application.Suppliers;
using VeriPass.Application.TrustScore;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VeriPass.Domain.Entities;

namespace VeriPass.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class BusinessesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<BusinessesController> _logger;
    private readonly IAuditService _auditService;

    public BusinessesController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        IApplicationDbContext context,
        ILogger<BusinessesController> logger,
        IAuditService auditService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _context = context;
        _logger = logger;
        _auditService = auditService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateBusiness([FromBody] CreateBusinessCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(new { businessId = result.Value }) : BadRequest(new { message = result.Error });
    }

    private async Task<Business?> ResolveBusinessAsync(string? id)
    {
        Guid targetId = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(id) && Guid.TryParse(id, out var parsed))
        {
            targetId = parsed;
        }

        if (targetId != Guid.Empty)
        {
            var biz = await _context.Businesses
                .Include(b => b.Passport)
                .Include(b => b.Addresses)
                .Include(b => b.Directors)
                .Include(b => b.BankAccounts)
                .Include(b => b.TrustScore)
                .Include(b => b.Organization)
                .FirstOrDefaultAsync(b => b.Id == targetId || b.OrganizationId == targetId || (b.Passport != null && b.Passport.Id == targetId), HttpContext.RequestAborted);
            if (biz != null) return biz;
        }

        if (!string.IsNullOrWhiteSpace(id) && id != "current" && id != "my")
        {
            var biz = await _context.Businesses
                .Include(b => b.Passport)
                .Include(b => b.Addresses)
                .Include(b => b.Directors)
                .Include(b => b.BankAccounts)
                .Include(b => b.TrustScore)
                .Include(b => b.Organization)
                .FirstOrDefaultAsync(b => b.BusinessIdNumber == id || b.Pan == id || b.Gstin == id || (b.Passport != null && (b.Passport.PassportNumber == id || b.Passport.PublicIdentifier == id)), HttpContext.RequestAborted);
            if (biz != null) return biz;
        }

        if (_currentUserService.OrganizationId.HasValue)
        {
            var orgId = _currentUserService.OrganizationId.Value;
            var biz = await _context.Businesses
                .Include(b => b.Passport)
                .Include(b => b.Addresses)
                .Include(b => b.Directors)
                .Include(b => b.BankAccounts)
                .Include(b => b.TrustScore)
                .Include(b => b.Organization)
                .FirstOrDefaultAsync(b => b.OrganizationId == orgId || b.Id == orgId, HttpContext.RequestAborted);
            if (biz != null) return biz;
        }

        // Development/Demo fallback: if no business found and current request is for "my" or "current" or null, return primary business
        if (string.IsNullOrWhiteSpace(id) || id == "current" || id == "my" || id.StartsWith("biz-"))
        {
            var fallback = await _context.Businesses
                .Include(b => b.Passport)
                .Include(b => b.Addresses)
                .Include(b => b.Directors)
                .Include(b => b.BankAccounts)
                .Include(b => b.TrustScore)
                .Include(b => b.Organization)
                .OrderBy(b => b.CreatedAt)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
            if (fallback != null) return fallback;
        }

        return null;
    }

    [HttpGet("my")]
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentBusiness()
    {
        var business = await ResolveBusinessAsync(null);
        if (business == null)
            return NotFound(new { message = "No registered business found for current session." });

        var result = await _mediator.Send(new GetBusinessProfileQuery(business.Id));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBusiness(string id)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return NotFound(new { message = "Business not found." });

        var result = await _mediator.Send(new GetBusinessProfileQuery(targetBusinessId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpGet("{id}/dashboard")]
    public async Task<IActionResult> GetDashboard(string id)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return NotFound(new { message = "Business not found." });

        var result = await _mediator.Send(new GetBusinessDashboardQuery(targetBusinessId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBusiness(string id, [FromBody] UpdateBusinessCommand command)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : command.BusinessId);
        if (targetBusinessId == Guid.Empty) return BadRequest(new { message = "Invalid business ID." });

        var effectiveCommand = command with { BusinessId = targetBusinessId };
        var result = await _mediator.Send(effectiveCommand);
        return result.IsSuccess ? Ok(new { success = true, businessId = targetBusinessId }) : BadRequest(new { message = result.Error });
    }

    [HttpPost("{id}/address")]
    public async Task<IActionResult> UpdateAddress(string id, [FromBody] AddBusinessAddressCommand command)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : command.BusinessId);
        if (targetBusinessId == Guid.Empty) return BadRequest(new { message = "Invalid business ID." });

        var effectiveCommand = command with { BusinessId = targetBusinessId };
        var result = await _mediator.Send(effectiveCommand);
        return result.IsSuccess ? Ok(new { addressId = result.Value }) : BadRequest(new { message = result.Error });
    }

    // Directors
    [HttpPost("{id}/directors")]
    public async Task<IActionResult> AddDirector(string id, [FromBody] AddDirectorRequest request)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return BadRequest(new { message = "Invalid business ID." });

        var result = await _mediator.Send(new AddDirectorCommand(targetBusinessId, request.FullName, request.Designation, request.Din, request.Pan));
        return result.IsSuccess ? Ok(new { directorId = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id}/directors")]
    public async Task<IActionResult> GetDirectors(string id)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return BadRequest(new { message = "Invalid business ID." });

        var result = await _mediator.Send(new GetDirectorsQuery(targetBusinessId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpPut("{id}/directors/{directorId}")]
    public async Task<IActionResult> UpdateDirector(string id, Guid directorId, [FromBody] UpdateDirectorRequest request)
    {
        var result = await _mediator.Send(new UpdateDirectorCommand(directorId, request.FullName, request.Designation, request.Din));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpDelete("{id}/directors/{directorId}")]
    public async Task<IActionResult> DeleteDirector(string id, Guid directorId)
    {
        var result = await _mediator.Send(new DeleteDirectorCommand(directorId));
        return result.IsSuccess ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    // Bank Accounts
    [HttpPost("{id}/bank-accounts")]
    public async Task<IActionResult> AddBankAccount(string id, [FromBody] AddBankAccountRequest request)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return BadRequest(new { message = "Invalid business ID." });

        var result = await _mediator.Send(new AddBankAccountCommand(targetBusinessId, request.AccountHolderName, request.BankName, request.AccountNumber, request.Ifsc, request.AccountType));
        return result.IsSuccess ? Ok(new { accountId = result.Value }) : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id}/bank-accounts")]
    public async Task<IActionResult> GetBankAccounts(string id)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return BadRequest(new { message = "Invalid business ID." });

        var result = await _mediator.Send(new GetBankAccountsQuery(targetBusinessId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    // Documents & Credentials
    [HttpGet("{id}/documents")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDocuments(string id)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return NotFound(new { message = "Business not found." });

        var result = await _mediator.Send(new GetBusinessDocumentsQuery(targetBusinessId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPost("{id}/documents")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument(
        string id, 
        [FromForm] string documentType, 
        IFormFile file,
        [FromForm] string? credentialNumber = null,
        [FromForm] string? credentialName = null,
        [FromForm] string? issueDate = null,
        [FromForm] string? issuer = null)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return BadRequest(new { message = "Invalid business ID." });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File cannot be empty." });

        const long maxSizeBytes = 10 * 1024 * 1024; // 10 MB limit
        if (file.Length > maxSizeBytes)
            return BadRequest(new { message = "File size exceeds the 10 MB limit." });

        var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest(new { message = "Invalid file type. Only PDF, PNG, and JPEG documents are permitted." });

        DateTime? parsedIssueDate = null;
        if (!string.IsNullOrWhiteSpace(issueDate))
        {
            if (DateTime.TryParse(issueDate, out var dt))
            {
                if (dt.Date > DateTime.UtcNow.Date)
                {
                    return BadRequest(new { message = "Issue date cannot be in the future. Please select a valid past or present date." });
                }
                parsedIssueDate = DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc);
            }
            else
            {
                return BadRequest(new { message = "Invalid issue date format. Please use YYYY-MM-DD." });
            }
        }

        using var stream = file.OpenReadStream();
        var command = new UploadDocumentCommand(
            targetBusinessId,
            documentType,
            file.FileName,
            file.ContentType,
            stream,
            _currentUserService.UserId,
            credentialNumber,
            credentialName,
            parsedIssueDate,
            issuer
        );

        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id}/passport")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPassport(string id)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return NotFound(new { message = "Business not found." });

        var result = await _mediator.Send(new GetBusinessPassportQuery(targetBusinessId));
        if (result.IsSuccess)
        {
            if (_currentUserService.OrganizationId.HasValue && _currentUserService.UserId.HasValue)
            {
                await _auditService.RecordAsync(new RecordAuditOptions(
                    Action: "PASSPORT_VIEWED",
                    Category: "PASSPORT",
                    TargetType: "BUSINESS_PASSPORT",
                    TargetId: result.Value.PassportNumber ?? id,
                    TargetName: result.Value.BusinessName ?? "Sovereign Passport Dossier",
                    Result: "SUCCESS",
                    OverrideOrganizationId: _currentUserService.OrganizationId.Value,
                    OverrideUserId: _currentUserService.UserId.Value
                ));
            }
        }

        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpPost("{id}/passport/generate")]
    [AllowAnonymous]
    public async Task<IActionResult> GeneratePassport(string id)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return BadRequest(new { message = "Business not found." });

        var result = await _mediator.Send(new GeneratePassportCommand(targetBusinessId));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id}/trust-score")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTrustScore(string id)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return NotFound(new { message = "Business not found." });

        var result = await _mediator.Send(new VeriPass.Application.Verification.GetTrustScoreQuery(targetBusinessId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpGet("{id}/compliance")]
    public async Task<IActionResult> GetComplianceEvents(string id)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return NotFound(new { message = "Business not found." });

        var result = await _mediator.Send(new GetComplianceEventsQuery(targetBusinessId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpGet("{id}/requests")]
    public async Task<IActionResult> GetVerificationRequests(string id)
    {
        var business = await ResolveBusinessAsync(id);
        var targetBusinessId = business?.Id ?? (Guid.TryParse(id, out var g) ? g : Guid.Empty);
        if (targetBusinessId == Guid.Empty) return NotFound(new { message = "Business not found." });

        var result = await _mediator.Send(new GetVerificationRequestsQuery(targetBusinessId));
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { message = result.Error });
    }

    [HttpGet("{id}/privacy")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPrivacySettings(string id)
    {
        var business = await ResolveBusinessAsync(id);
        if (business == null) return NotFound(new { message = "Business not found." });

        if (business.Passport != null && !string.IsNullOrWhiteSpace(business.Passport.PrivacyPreferences))
        {
            try
            {
                using var doc = JsonDocument.Parse(business.Passport.PrivacyPreferences);
                return Ok(doc.RootElement.Clone());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse PrivacyPreferences JSON. Falling back to default.");
            }
        }

        // Return default sovereign privacy preferences
        var defaultPrivacy = new
        {
            profileVisibility = "PUBLIC",
            maskPanPublic = true,
            maskBankDetails = true,
            autoApproveTier1 = true,
            requireNdaForAuditReports = true,
            searchEngineIndexing = true,
            allowDirectBuyerInquiries = true,
            allowedBuyerScopes = new
            {
                verifiedEnterpriseOnly = true,
                explicitInviteOnly = false,
                allProcurementDepts = true
            },
            attributes = new object[]
            {
                new
                {
                    id = "legal_name",
                    name = "Business Legal Name",
                    category = "Statutory Identity",
                    visibility = "PUBLIC",
                    locked = true,
                    description = "Official corporate entity title as registered with MCA / ROC.",
                    icon = "🏢"
                },
                new
                {
                    id = "industry_location",
                    name = "Industry Classification & City",
                    category = "Business Profile",
                    visibility = "PUBLIC",
                    locked = false,
                    description = "Operating manufacturing sector, state, and registered city.",
                    icon = "📍"
                },
                new
                {
                    id = "gstin",
                    name = "GST Verification Badge & GSTIN",
                    category = "Tax & Statutory",
                    visibility = "PUBLIC",
                    locked = false,
                    description = "Active GST registration status and 15-digit GSTIN reconciliation.",
                    icon = "🛡️"
                },
                new
                {
                    id = "pan",
                    name = "Corporate PAN (Unmasked)",
                    category = "Tax & Statutory",
                    visibility = "VERIFIED_BUYERS",
                    locked = false,
                    description = "Full 10-character PAN number. (Masked on public passport).",
                    icon = "🪪"
                },
                new
                {
                    id = "bank_account",
                    name = "Bank Account & Penny Verification",
                    category = "Financial Evidence",
                    visibility = "PRIVATE",
                    locked = false,
                    description = "Internal banking records and penny-drop confirmation receipt.",
                    icon = "🏦"
                },
                new
                {
                    id = "udyam",
                    name = "MSME / Udyam Certificate",
                    category = "Statutory Identity",
                    visibility = "PUBLIC",
                    locked = false,
                    description = "Micro, Small, and Medium Enterprises government verification badge.",
                    icon = "📜"
                },
                new
                {
                    id = "directors",
                    name = "Directors & Authorized Signatories",
                    category = "Governance",
                    visibility = "VERIFIED_BUYERS",
                    locked = false,
                    description = "DIN numbers, designation, and KYC status of key promoters.",
                    icon = "👥"
                },
                new
                {
                    id = "audit_reports",
                    name = "Quality & ISO Factory Audit Reports",
                    category = "Compliance Evidence",
                    visibility = "VERIFIED_BUYERS",
                    locked = false,
                    description = "Detailed audit reports and accredited third-party lab certificates.",
                    icon = "📋"
                },
                new
                {
                    id = "trust_score",
                    name = "Sovereign Trust Score Breakdown",
                    category = "Trust & Risk",
                    visibility = "PUBLIC",
                    locked = false,
                    description = "Composite Trust Score (out of 100) and watchtower reliability grade.",
                    icon = "📈"
                }
            }
        };

        return Ok(defaultPrivacy);
    }

    [HttpPut("{id}/privacy")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdatePrivacySettings(string id, [FromBody] JsonElement payload)
    {
        var business = await ResolveBusinessAsync(id);
        if (business == null) return NotFound(new { message = "Business not found." });

        if (business.Passport == null)
        {
            business.Passport = new BusinessPassport
            {
                BusinessId = business.Id,
                PassportNumber = $"VP-IN-{DateTime.UtcNow.Year}-{Random.Shared.Next(10000, 99999)}",
                PublicIdentifier = (business.LegalName.ToLower().Replace(" ", "-") + "-" + Random.Shared.Next(1000, 9999)),
                Status = "ACTIVE",
                IssuedAt = DateTime.UtcNow,
                IsPublic = true
            };
            _context.BusinessPassports.Add(business.Passport);
        }

        var jsonString = payload.GetRawText();
        business.Passport.PrivacyPreferences = jsonString;
        business.Passport.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? Guid.Empty,
            Action = "PRIVACY_PREFERENCES_UPDATED",
            ResourceType = "BusinessPassport",
            ResourceId = business.Passport.Id.ToString(),
            NewValuesJson = jsonString
        });

        await _context.SaveChangesAsync(HttpContext.RequestAborted);
        _logger.LogInformation("Updated privacy preferences for Business {BusinessId}", business.Id);

        return Ok(new
        {
            success = true,
            message = "Privacy & visibility preferences updated and synchronized with sovereign database.",
            preferences = payload
        });
    }

    [HttpGet("{id}/export")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportBusinessData(string id)
    {
        var business = await ResolveBusinessAsync(id);
        if (business == null) return NotFound(new { message = "Business not found." });

        var exportBundle = new
        {
            exportMetadata = new
            {
                exportId = Guid.NewGuid().ToString(),
                exportTimestamp = DateTime.UtcNow,
                complianceStandard = "Digital Personal Data Protection (DPDP) Act 2023 / ISO 27001",
                dataFiduciary = "VeriPass Sovereign Trust Network",
                schemaVersion = "veripass-sovereign-export-v2.1",
                dataSubjectId = business.Id.ToString(),
                generatedBy = _currentUserService.UserId?.ToString() ?? "Sovereign Administrator"
            },
            organization = new
            {
                organizationId = business.OrganizationId,
                businessId = business.Id,
                businessIdNumber = business.BusinessIdNumber,
                legalName = business.LegalName,
                tradeName = business.TradeName,
                businessType = business.BusinessType,
                incorporationDate = business.IncorporationDate,
                verificationStatus = business.VerificationStatus.ToString(),
                description = business.Description,
                website = business.Website,
                employeeCount = business.EmployeeCount
            },
            statutoryRegistry = new
            {
                pan = business.Pan,
                gstin = business.Gstin,
                cin = business.Cin,
                udyamNumber = business.UdyamNumber
            },
            addresses = business.Addresses.Select(a => new
            {
                a.Id,
                a.AddressType,
                a.Line1,
                a.Line2,
                a.City,
                a.District,
                a.State,
                a.Pincode,
                a.Country,
                a.IsPrimary,
                verificationStatus = a.VerificationStatus.ToString()
            }),
            directors = business.Directors.Select(d => new
            {
                d.Id,
                d.FullName,
                d.Designation,
                d.Din,
                d.PanMasked,
                d.AppointmentDate,
                d.Status
            }),
            bankAccounts = business.BankAccounts.Select(acc => new
            {
                acc.Id,
                acc.AccountHolderName,
                acc.BankName,
                acc.AccountNumberLast4,
                acc.Ifsc,
                acc.AccountType,
                verificationStatus = acc.VerificationStatus.ToString(),
                acc.VerifiedAt
            }),
            trustScore = business.TrustScore != null ? new
            {
                business.TrustScore.OverallScore,
                business.TrustScore.IdentityScore,
                business.TrustScore.CredentialScore,
                business.TrustScore.ComplianceScore,
                business.TrustScore.VerificationScore,
                business.TrustScore.RiskScore,
                business.TrustScore.CalculationVersion,
                business.TrustScore.LastCalculatedAt
            } : null,
            passport = business.Passport != null ? new
            {
                business.Passport.PassportNumber,
                business.Passport.PublicIdentifier,
                business.Passport.Status,
                business.Passport.IssuedAt,
                business.Passport.ExpiresAt,
                business.Passport.LastVerifiedAt,
                business.Passport.IsPublic,
                privacyPreferences = business.Passport.PrivacyPreferences
            } : null
        };

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? Guid.Parse("99999999-0000-0000-0000-000000000001"),
            OrganizationId = business.OrganizationId,
            Action = "BUSINESS_DATA_EXPORTED",
            ResourceType = "Business",
            ResourceId = business.Id.ToString(),
            NewValuesJson = JsonSerializer.Serialize(new { timestamp = DateTime.UtcNow, standard = "DPDP_2023" })
        });
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(exportBundle);
    }

    [HttpPost("{id}/toggle-freeze")]
    [AllowAnonymous]
    public async Task<IActionResult> TogglePassportFreeze(string id)
    {
        var business = await ResolveBusinessAsync(id);
        if (business == null)
            return NotFound(new { message = "Business not found." });

        if (business.Passport == null)
        {
            business.Passport = new BusinessPassport
            {
                BusinessId = business.Id,
                PassportNumber = $"VP-IN-{DateTime.UtcNow.Year}-{Random.Shared.Next(10000, 99999)}",
                PublicIdentifier = (business.LegalName.ToLower().Replace(" ", "-") + "-" + Random.Shared.Next(1000, 9999)),
                Status = "ACTIVE",
                IssuedAt = DateTime.UtcNow,
                IsPublic = true
            };
            _context.BusinessPassports.Add(business.Passport);
        }

        bool isCurrentlyActive = business.Passport.Status == "ACTIVE";
        business.Passport.Status = isCurrentlyActive ? "SUSPENDED" : "ACTIVE";
        business.Passport.UpdatedAt = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? Guid.Empty,
            OrganizationId = business.OrganizationId,
            Action = isCurrentlyActive ? "PASSPORT_TEMPORARILY_SUSPENDED" : "PASSPORT_REACTIVATED",
            ResourceType = "BusinessPassport",
            ResourceId = business.Passport.Id.ToString(),
            NewValuesJson = JsonSerializer.Serialize(new { status = business.Passport.Status })
        });
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            success = true,
            status = business.Passport.Status,
            message = isCurrentlyActive
                ? "Business Passport discovery is now temporarily paused."
                : "Business Passport discovery is now active."
        });
    }

    [HttpPost("{id}/terminate-request")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestAccountTermination(string id, [FromBody] JsonElement payload)
    {
        var business = await ResolveBusinessAsync(id);
        if (business == null)
            return NotFound(new { message = "Business not found." });

        if (business.Passport != null)
        {
            business.Passport.Status = "TERMINATION_REQUESTED";
            business.Passport.UpdatedAt = DateTime.UtcNow;
        }

        string reason = "User requested termination";
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("reason", out var r))
        {
            reason = r.GetString() ?? reason;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? Guid.Empty,
            OrganizationId = business.OrganizationId,
            Action = "ACCOUNT_TERMINATION_REQUESTED",
            ResourceType = "Business",
            ResourceId = business.Id.ToString(),
            NewValuesJson = JsonSerializer.Serialize(new
            {
                requestedAt = DateTime.UtcNow,
                coolingOffDays = 30,
                scheduledPurgeDate = DateTime.UtcNow.AddDays(30),
                reason = reason
            })
        });
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            success = true,
            status = "TERMINATION_REQUESTED",
            scheduledPurgeDate = DateTime.UtcNow.AddDays(30).ToString("o"),
            message = "Account termination request registered. Statutory 30-day cooling-off window initialized."
        });
    }

    [HttpPost("{id}/cancel-termination")]
    [AllowAnonymous]
    public async Task<IActionResult> CancelAccountTermination(string id)
    {
        var business = await ResolveBusinessAsync(id);
        if (business == null)
            return NotFound(new { message = "Business not found." });

        if (business.Passport != null)
        {
            business.Passport.Status = "ACTIVE";
            business.Passport.UpdatedAt = DateTime.UtcNow;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUserService.UserId ?? Guid.Empty,
            OrganizationId = business.OrganizationId,
            Action = "ACCOUNT_TERMINATION_CANCELLED",
            ResourceType = "Business",
            ResourceId = business.Id.ToString(),
            NewValuesJson = JsonSerializer.Serialize(new { cancelledAt = DateTime.UtcNow })
        });
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            success = true,
            status = "ACTIVE",
            message = "Account termination request cancelled. Account restored to Active Sovereign Participant."
        });
    }

    public record AddDirectorRequest(string FullName, string Designation, string? Din, string? Pan);
    public record UpdateDirectorRequest(string FullName, string Designation, string? Din);
    public record AddBankAccountRequest(string AccountHolderName, string BankName, string AccountNumber, string Ifsc, string AccountType = "CURRENT");
}
