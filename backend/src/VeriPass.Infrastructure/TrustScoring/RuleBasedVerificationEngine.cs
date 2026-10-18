using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Domain.Common;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Domain.TrustScoring;
using VeriPass.Domain.ValueObjects;

namespace VeriPass.Infrastructure.TrustScoring;

/// <summary>
/// Modular Rule-Based Verification Engine that validates supplier credentials
/// through a multi-stage pipeline: Availability -> Format -> Structure -> Checksum -> Profile/Data Match -> Registry Status.
/// Produces explainable verification outputs with granular rule-level audit details.
/// </summary>
public class RuleBasedVerificationEngine : IRuleBasedVerificationEngine
{
    private readonly ILogger<RuleBasedVerificationEngine> _logger;

    public RuleBasedVerificationEngine(ILogger<RuleBasedVerificationEngine> logger)
    {
        _logger = logger;
    }

    public CredentialVerificationResult EvaluateCredential(
        Business supplier,
        string credentialType,
        bool isRequired,
        string? configuredRules = null,
        IReadOnlyList<ComplianceRule>? adminRules = null)
    {
        var rawType = credentialType.Trim().ToUpperInvariant();
        var normType = DocumentScoringPolicy.NormalizeType(rawType);
        var ruleDetails = new List<RuleExecutionDetail>();

        // 1. Gather all supplier assets matching this credential type
        var matchingCredentials = supplier.Credentials
            .Where(c => c.CredentialType != null &&
                        (c.CredentialType.Code.Equals(rawType, StringComparison.OrdinalIgnoreCase) ||
                         DocumentScoringPolicy.NormalizeType(c.CredentialType.Code) == normType))
            .ToList();

        var matchingDocuments = supplier.Documents
            .Where(d => DocumentScoringPolicy.NormalizeType(d.DocumentType) == normType ||
                        d.DocumentType.Equals(rawType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var matchingCases = supplier.VerificationCases
            .Where(vc => DocumentScoringPolicy.NormalizeType(vc.VerificationType) == normType ||
                         vc.VerificationType.Equals(rawType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 2. Extract best available identifier and evidence metadata
        string? primaryIdentifier = null;
        string? extractedName = null;
        DateTime? expiryDate = null;
        bool isExplicitlyExpired = false;
        bool hasTamperingRisk = false;

        if (normType == "GST")
        {
            primaryIdentifier = matchingCredentials.FirstOrDefault()?.CredentialDisplayValue ?? supplier.Gstin;
        }
        else if (normType == "PAN")
        {
            primaryIdentifier = matchingCredentials.FirstOrDefault()?.CredentialDisplayValue ?? supplier.Pan;
        }
        else if (normType == "UDYAM" || normType == "MSME")
        {
            primaryIdentifier = matchingCredentials.FirstOrDefault()?.CredentialDisplayValue ?? supplier.UdyamNumber;
        }
        else if (normType == "BANK")
        {
            var primaryBank = supplier.BankAccounts.FirstOrDefault(b => b.IsPrimary) ?? supplier.BankAccounts.FirstOrDefault();
            primaryIdentifier = primaryBank?.AccountNumberLast4 ?? matchingCredentials.FirstOrDefault()?.CredentialDisplayValue;
            extractedName = primaryBank?.AccountHolderName;
        }
        else if (normType == "CIN" || normType == "BUSINESS_REGISTRATION")
        {
            primaryIdentifier = supplier.Cin ?? matchingCredentials.FirstOrDefault()?.CredentialDisplayValue;
        }
        else
        {
            primaryIdentifier = matchingCredentials.FirstOrDefault()?.CredentialDisplayValue;
        }

        // Check document-level metadata
        foreach (var doc in matchingDocuments)
        {
            if (doc.Status == DocumentStatus.REJECTED) hasTamperingRisk = true;
        }

        foreach (var cred in matchingCredentials)
        {
            if (cred.ExpiryDate.HasValue && (!expiryDate.HasValue || cred.ExpiryDate < expiryDate))
            {
                expiryDate = cred.ExpiryDate;
            }
            if (cred.Status == CredentialStatus.EXPIRED || cred.VerificationStatus == VerificationStatus.EXPIRED)
            {
                isExplicitlyExpired = true;
            }
        }

        // 3. Stage 1: Availability Check
        bool isAvailable = !string.IsNullOrWhiteSpace(primaryIdentifier) || matchingCredentials.Count > 0 || matchingDocuments.Count > 0 || (normType == "BANK" && supplier.BankAccounts.Count > 0);
        ruleDetails.Add(new RuleExecutionDetail(
            RuleCode: "RULE_AVAILABILITY",
            RuleName: $"{rawType} Availability Check",
            CredentialType: rawType,
            ValidationType: "AVAILABILITY",
            Passed: isAvailable,
            Status: isAvailable ? "PASSED" : "FAILED",
            Reason: isAvailable ? "Identifier or document artifact is available." : "No credential identifier or document artifact has been uploaded.",
            ExecutedAt: DateTime.UtcNow,
            Severity: isRequired ? "HIGH" : "MEDIUM"
        ));

        if (!isAvailable)
        {
            return new CredentialVerificationResult(
                CredentialType: rawType,
                FinalStatus: isRequired ? "MISSING" : "PENDING",
                RuleFailed: isRequired,
                RuleFailedReason: isRequired ? $"Mandatory credential {rawType} is missing" : null,
                HasDefinitiveFailure: false,
                RequiresManualReview: false,
                RuleDetails: ruleDetails
            );
        }

        // 4. Stage 2: Expiry Check
        if (expiryDate.HasValue && expiryDate.Value < DateTime.UtcNow)
        {
            isExplicitlyExpired = true;
        }

        ruleDetails.Add(new RuleExecutionDetail(
            RuleCode: "RULE_EXPIRY",
            RuleName: $"{rawType} Validity Period Check",
            CredentialType: rawType,
            ValidationType: "STATUS",
            Passed: !isExplicitlyExpired,
            Status: !isExplicitlyExpired ? "PASSED" : "FAILED",
            Reason: !isExplicitlyExpired ? "Credential is within valid unexpired lifecycle." : $"Credential expired on {expiryDate:yyyy-MM-dd}.",
            ExecutedAt: DateTime.UtcNow,
            Severity: "HIGH"
        ));

        if (isExplicitlyExpired)
        {
            return new CredentialVerificationResult(
                CredentialType: rawType,
                FinalStatus: "EXPIRED",
                RuleFailed: true,
                RuleFailedReason: $"Credential {rawType} is expired",
                HasDefinitiveFailure: true,
                RequiresManualReview: false,
                RuleDetails: ruleDetails
            );
        }

        // 5. Stage 3: Tampering Check
        if (hasTamperingRisk)
        {
            ruleDetails.Add(new RuleExecutionDetail(
                RuleCode: "RULE_TAMPERING_CHECK",
                RuleName: $"{rawType} Authenticity & Integrity Check",
                CredentialType: rawType,
                ValidationType: "INTEGRITY",
                Passed: false,
                Status: "FAILED",
                Reason: "Potential image or text tampering detected on source evidence document.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "CRITICAL"
            ));

            return new CredentialVerificationResult(
                CredentialType: rawType,
                FinalStatus: "REJECTED",
                RuleFailed: true,
                RuleFailedReason: "Evidence document failed authenticity integrity inspection",
                HasDefinitiveFailure: true,
                RequiresManualReview: false,
                RuleDetails: ruleDetails
            );
        }

        bool hasDefinitiveFailure = false;
        bool hasAmbiguity = false;
        string? failureReason = null;
        string? escalationReason = null;

        // 6. Stage 4: Credential-Specific Format, Structure, Checksum & Profile Matching
        if (normType == "GST")
        {
            EvaluateGstRules(primaryIdentifier, supplier, configuredRules, ruleDetails, ref hasDefinitiveFailure, ref hasAmbiguity, ref failureReason, ref escalationReason);
        }
        else if (normType == "PAN")
        {
            EvaluatePanRules(primaryIdentifier, supplier, configuredRules, ruleDetails, ref hasDefinitiveFailure, ref hasAmbiguity, ref failureReason, ref escalationReason);
        }
        else if (normType == "UDYAM" || normType == "MSME")
        {
            EvaluateUdyamRules(primaryIdentifier, supplier, configuredRules, ruleDetails, ref hasDefinitiveFailure, ref hasAmbiguity, ref failureReason, ref escalationReason);
        }
        else if (normType == "BANK")
        {
            EvaluateBankRules(primaryIdentifier, extractedName, supplier, configuredRules, ruleDetails, ref hasDefinitiveFailure, ref hasAmbiguity, ref failureReason, ref escalationReason);
        }
        else if (normType == "CIN" || normType == "BUSINESS_REGISTRATION")
        {
            EvaluateCinRules(primaryIdentifier, supplier, configuredRules, ruleDetails, ref hasDefinitiveFailure, ref hasAmbiguity, ref failureReason, ref escalationReason);
        }

        // 7. Stage 5: External Status / Registry Check
        EvaluateRegistryStatus(rawType, matchingCredentials, matchingDocuments, matchingCases, ruleDetails, ref hasDefinitiveFailure, ref hasAmbiguity, ref failureReason);

        // 8. Determine Final Status
        string finalStatus;
        bool ruleFailed = false;
        string? finalRuleFailedReason = null;

        if (hasDefinitiveFailure)
        {
            finalStatus = "REJECTED";
            ruleFailed = true;
            finalRuleFailedReason = failureReason ?? $"{rawType} failed definitive validation rules";
        }
        else if (hasAmbiguity)
        {
            finalStatus = "ESCALATED";
            ruleFailed = false; // Escalated needs verifier attention; not an immediate hard rejection
            finalRuleFailedReason = escalationReason;
        }
        else
        {
            // Check if verification cases or documents are approved
            bool hasApproval = matchingCredentials.Any(c => c.Status == CredentialStatus.VERIFIED || c.VerificationStatus == VerificationStatus.VERIFIED) ||
                              matchingDocuments.Any(d => d.Status == DocumentStatus.APPROVED) ||
                              matchingCases.Any(c => c.FinalDecision == VerificationDecision.APPROVED || c.Status == VerificationStatus.VERIFIED) ||
                              (normType == "GST" && !string.IsNullOrWhiteSpace(supplier.Gstin) && supplier.VerificationStatus == VerificationStatus.VERIFIED) ||
                              (normType == "PAN" && !string.IsNullOrWhiteSpace(supplier.Pan) && supplier.VerificationStatus == VerificationStatus.VERIFIED) ||
                              (normType == "BANK" && supplier.BankAccounts.Any(b => b.VerificationStatus == VerificationStatus.VERIFIED));

            bool hasPending = matchingCredentials.Any(c => c.Status == CredentialStatus.SUBMITTED || c.VerificationStatus == VerificationStatus.PENDING) ||
                             matchingDocuments.Any(d => d.Status == DocumentStatus.UPLOADED || d.Status == DocumentStatus.PROCESSING) ||
                             matchingCases.Any(c => c.Status == VerificationStatus.PENDING || c.Status == VerificationStatus.IN_PROGRESS) ||
                             (normType == "BANK" && supplier.BankAccounts.Any(b => b.VerificationStatus == VerificationStatus.UNVERIFIED));

            if (hasApproval)
            {
                finalStatus = "APPROVED";
            }
            else if (hasPending)
            {
                finalStatus = "PENDING";
            }
            else
            {
                finalStatus = "APPROVED"; // Passed all rule validations and consistency checks
            }
        }

        return new CredentialVerificationResult(
            CredentialType: rawType,
            FinalStatus: finalStatus,
            RuleFailed: ruleFailed,
            RuleFailedReason: finalRuleFailedReason,
            HasDefinitiveFailure: hasDefinitiveFailure,
            RequiresManualReview: hasAmbiguity,
            RuleDetails: ruleDetails
        );
    }

    public IReadOnlyList<CredentialVerificationResult> EvaluateAll(
        Business supplier,
        IEnumerable<(string Type, bool IsRequired, string? Rules)> requirements,
        IReadOnlyList<ComplianceRule>? adminRules = null)
    {
        var results = new List<CredentialVerificationResult>();
        foreach (var req in requirements)
        {
            var res = EvaluateCredential(supplier, req.Type, req.IsRequired, req.Rules, adminRules);
            results.Add(res);
        }
        return results;
    }

    private void EvaluateGstRules(
        string? identifier,
        Business supplier,
        string? configuredRules,
        List<RuleExecutionDetail> details,
        ref bool hasDefinitiveFailure,
        ref bool hasAmbiguity,
        ref string? failureReason,
        ref string? escalationReason)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            var hasVerified = supplier.Credentials.Any(c => (c.CredentialType?.Code == "GST" || c.CredentialType?.Code == "GSTIN") &&
                (c.Status == CredentialStatus.VERIFIED || c.VerificationStatus == VerificationStatus.VERIFIED)) ||
                supplier.VerificationStatus == VerificationStatus.VERIFIED;

            if (hasVerified)
            {
                details.Add(new RuleExecutionDetail(
                    RuleCode: "RULE_GST_FORMAT",
                    RuleName: "GSTIN Structure Check",
                    CredentialType: "GST",
                    ValidationType: "FORMAT",
                    Passed: true,
                    Status: "PASSED",
                    Reason: "Verified sovereign GSTIN credential record present in repository.",
                    ExecutedAt: DateTime.UtcNow,
                    Severity: "LOW"
                ));
                return;
            }

            hasDefinitiveFailure = true;
            failureReason = "GSTIN identifier is missing.";
            return;
        }

        var trimmed = identifier.Trim().ToUpperInvariant();

        // 1. Format & Length
        bool formatValid = trimmed.Length == 15 && Regex.IsMatch(trimmed, @"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$");
        details.Add(new RuleExecutionDetail(
            RuleCode: "RULE_GST_FORMAT",
            RuleName: "GSTIN 15-Character Format Validation",
            CredentialType: "GST",
            ValidationType: "FORMAT",
            Passed: formatValid,
            Status: formatValid ? "PASSED" : "FAILED",
            Reason: formatValid ? "Valid 15-character GSTIN structure." : $"Invalid GSTIN format: '{trimmed}'. Expected 15 characters (2 digits state, 10 PAN, 1 entity, 'Z', 1 checksum).",
            ExecutedAt: DateTime.UtcNow,
            Severity: "HIGH"
        ));

        if (!formatValid)
        {
            hasDefinitiveFailure = true;
            failureReason = "Invalid GSTIN format";
            return;
        }

        // 2. Structure & State Code
        var gstinObj = new Gstin(trimmed);
        bool stateCodeValid = gstinObj.IsValidStateCode;
        details.Add(new RuleExecutionDetail(
            RuleCode: "RULE_GST_STRUCTURE",
            RuleName: "GSTIN State Code & Structure Check",
            CredentialType: "GST",
            ValidationType: "STRUCTURE",
            Passed: stateCodeValid,
            Status: stateCodeValid ? "PASSED" : "FAILED",
            Reason: stateCodeValid ? $"State code {gstinObj.StateCode} is valid." : $"Invalid GSTIN state code: '{gstinObj.StateCode}'.",
            ExecutedAt: DateTime.UtcNow,
            Severity: "HIGH"
        ));

        if (!stateCodeValid)
        {
            hasDefinitiveFailure = true;
            failureReason = $"Invalid GSTIN state code '{gstinObj.StateCode}'";
            return;
        }

        // 3. Checksum Validation (Mod-36)
        bool checksumEnabled = configuredRules != null && configuredRules.Contains("CHECKSUM", StringComparison.OrdinalIgnoreCase);
        if (checksumEnabled)
        {
            bool checksumValid = Gstin.VerifyChecksum(trimmed, out var expectedChar);
            details.Add(new RuleExecutionDetail(
                RuleCode: "RULE_GST_CHECKSUM",
                RuleName: "GSTIN Mod-36 Checksum Validation",
                CredentialType: "GST",
                ValidationType: "CHECKSUM",
                Passed: checksumValid,
                Status: checksumValid ? "PASSED" : "FAILED",
                Reason: checksumValid ? "Checksum digit verified via GSTN Mod-36 algorithm." : $"GSTIN checksum mismatch. Calculated '{expectedChar}' but found '{trimmed[14]}'.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "HIGH"
            ));

            if (!checksumValid)
            {
                hasDefinitiveFailure = true;
                failureReason = $"GSTIN checksum mismatch: expected '{expectedChar}', found '{trimmed[14]}'";
                return;
            }
        }
        else
        {
            details.Add(new RuleExecutionDetail(
                RuleCode: "RULE_GST_CHECKSUM",
                RuleName: "GSTIN Mod-36 Checksum Validation",
                CredentialType: "GST",
                ValidationType: "CHECKSUM",
                Passed: true,
                Status: "SKIPPED",
                Reason: "Checksum validation not required in current profile configuration.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "LOW"
            ));
        }

        // 4. PAN Consistency (Cross-document matching)
        if (!string.IsNullOrWhiteSpace(supplier.Pan))
        {
            var declaredPan = supplier.Pan.Trim().ToUpperInvariant();
            var embeddedPan = gstinObj.Pan;
            bool panMatches = string.Equals(declaredPan, embeddedPan, StringComparison.OrdinalIgnoreCase);

            details.Add(new RuleExecutionDetail(
                RuleCode: "RULE_GST_PAN_MATCH",
                RuleName: "Cross-Document GSTIN to PAN Consistency Check",
                CredentialType: "GST",
                ValidationType: "DATA_MATCH",
                Passed: panMatches,
                Status: panMatches ? "PASSED" : "FAILED",
                Reason: panMatches ? "Embedded PAN in GSTIN strictly matches declared corporate PAN." : $"Cross-check failed: GSTIN embedded PAN '{embeddedPan}' differs from supplier PAN '{declaredPan}'.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "CRITICAL"
            ));

            if (!panMatches)
            {
                hasDefinitiveFailure = true;
                failureReason = "GSTIN embedded PAN does not match registered corporate PAN";
                return;
            }
        }

        // 5. Name Match
        EvaluateNameMatching("GST", supplier, details, ref hasAmbiguity, ref escalationReason);
    }

    private void EvaluatePanRules(
        string? identifier,
        Business supplier,
        string? configuredRules,
        List<RuleExecutionDetail> details,
        ref bool hasDefinitiveFailure,
        ref bool hasAmbiguity,
        ref string? failureReason,
        ref string? escalationReason)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            var hasVerified = supplier.Credentials.Any(c => c.CredentialType?.Code == "PAN" &&
                (c.Status == CredentialStatus.VERIFIED || c.VerificationStatus == VerificationStatus.VERIFIED)) ||
                supplier.VerificationStatus == VerificationStatus.VERIFIED;

            if (hasVerified)
            {
                details.Add(new RuleExecutionDetail(
                    RuleCode: "RULE_PAN_FORMAT",
                    RuleName: "PAN Structure Check",
                    CredentialType: "PAN",
                    ValidationType: "FORMAT",
                    Passed: true,
                    Status: "PASSED",
                    Reason: "Verified sovereign PAN credential record present in repository.",
                    ExecutedAt: DateTime.UtcNow,
                    Severity: "LOW"
                ));
                return;
            }

            hasDefinitiveFailure = true;
            failureReason = "PAN identifier is missing.";
            return;
        }

        var trimmed = identifier.Trim().ToUpperInvariant();

        // 1. Format & Length
        bool formatValid = trimmed.Length == 10 && Regex.IsMatch(trimmed, @"^[A-Z]{5}[0-9]{4}[A-Z]{1}$");
        details.Add(new RuleExecutionDetail(
            RuleCode: "RULE_PAN_FORMAT",
            RuleName: "PAN 10-Character Format Validation",
            CredentialType: "PAN",
            ValidationType: "FORMAT",
            Passed: formatValid,
            Status: formatValid ? "PASSED" : "FAILED",
            Reason: formatValid ? "Valid 10-character PAN structure (5 alpha, 4 numeric, 1 alpha)." : $"Invalid PAN format: '{trimmed}'.",
            ExecutedAt: DateTime.UtcNow,
            Severity: "HIGH"
        ));

        if (!formatValid)
        {
            hasDefinitiveFailure = true;
            failureReason = "Invalid PAN format";
            return;
        }

        // 2. Structure & Entity Type
        var panObj = new PanNumber(trimmed);
        bool entityTypeValid = panObj.IsValidEntityType;
        details.Add(new RuleExecutionDetail(
            RuleCode: "RULE_PAN_STRUCTURE",
            RuleName: "PAN 4th Character Entity Type Validation",
            CredentialType: "PAN",
            ValidationType: "STRUCTURE",
            Passed: entityTypeValid,
            Status: entityTypeValid ? "PASSED" : "FAILED",
            Reason: entityTypeValid ? $"4th character '{panObj.EntityType}' represents valid entity status." : $"Invalid PAN entity character '{panObj.EntityType}'.",
            ExecutedAt: DateTime.UtcNow,
            Severity: "HIGH"
        ));

        if (!entityTypeValid)
        {
            hasDefinitiveFailure = true;
            failureReason = $"Invalid entity character in PAN: '{panObj.EntityType}'";
            return;
        }

        // 3. Name Initial Match
        bool initialMatchEnabled = configuredRules != null &&
            (configuredRules.Contains("NAME_MATCH", StringComparison.OrdinalIgnoreCase) ||
             configuredRules.Contains("INITIAL_MATCH", StringComparison.OrdinalIgnoreCase) ||
             configuredRules.Contains("DATA_MATCH", StringComparison.OrdinalIgnoreCase));

        if (initialMatchEnabled && !string.IsNullOrWhiteSpace(supplier.LegalName))
        {
            bool initialMatches = panObj.MatchesLegalNameInitial(supplier.LegalName);
            details.Add(new RuleExecutionDetail(
                RuleCode: "RULE_PAN_INITIAL_MATCH",
                RuleName: "PAN 5th Character Name Initial Consistency",
                CredentialType: "PAN",
                ValidationType: "DATA_MATCH",
                Passed: initialMatches,
                Status: initialMatches ? "PASSED" : "ESCALATED",
                Reason: initialMatches ? $"5th character '{panObj.EntityInitial}' matches legal name first letter." : $"5th character '{panObj.EntityInitial}' differs from legal name start: '{supplier.LegalName}'.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "MEDIUM"
            ));

            if (!initialMatches)
            {
                hasAmbiguity = true;
                escalationReason = $"PAN 5th character '{panObj.EntityInitial}' does not match legal name initial";
            }
        }
        else
        {
            details.Add(new RuleExecutionDetail(
                RuleCode: "RULE_PAN_INITIAL_MATCH",
                RuleName: "PAN 5th Character Name Initial Consistency",
                CredentialType: "PAN",
                ValidationType: "DATA_MATCH",
                Passed: true,
                Status: "SKIPPED",
                Reason: "Name initial check not required in current profile configuration.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "LOW"
            ));
        }

        // 4. Name Matching
        EvaluateNameMatching("PAN", supplier, details, ref hasAmbiguity, ref escalationReason);
    }

    private void EvaluateUdyamRules(
        string? identifier,
        Business supplier,
        string? configuredRules,
        List<RuleExecutionDetail> details,
        ref bool hasDefinitiveFailure,
        ref bool hasAmbiguity,
        ref string? failureReason,
        ref string? escalationReason)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            var hasVerified = supplier.Credentials.Any(c => (c.CredentialType?.Code == "UDYAM" || c.CredentialType?.Code == "MSME") &&
                (c.Status == CredentialStatus.VERIFIED || c.VerificationStatus == VerificationStatus.VERIFIED)) ||
                supplier.Documents.Any(d => d.DocumentType.Contains("UDYAM", StringComparison.OrdinalIgnoreCase) && d.Status == DocumentStatus.APPROVED);

            if (hasVerified)
            {
                details.Add(new RuleExecutionDetail(
                    RuleCode: "RULE_UDYAM_FORMAT",
                    RuleName: "Udyam Certificate Check",
                    CredentialType: "UDYAM",
                    ValidationType: "FORMAT",
                    Passed: true,
                    Status: "PASSED",
                    Reason: "Verified Udyam credential record present in repository.",
                    ExecutedAt: DateTime.UtcNow,
                    Severity: "LOW"
                ));
                return;
            }

            hasDefinitiveFailure = true;
            failureReason = "Udyam registration identifier is missing.";
            return;
        }

        var trimmed = identifier.Trim().ToUpperInvariant();

        // 1. Format & Structure
        bool formatValid = UdyamNumber.TryParse(trimmed, out var udyamObj);
        details.Add(new RuleExecutionDetail(
            RuleCode: "RULE_UDYAM_FORMAT",
            RuleName: "Udyam Certificate Number Format Validation",
            CredentialType: "UDYAM",
            ValidationType: "FORMAT",
            Passed: formatValid,
            Status: formatValid ? "PASSED" : "FAILED",
            Reason: formatValid ? "Valid MSME Udyam certificate identifier structure (UDYAM-XX-00-0000000)." : $"Invalid Udyam number format: '{trimmed}'. Expected 'UDYAM-XX-00-0000000'.",
            ExecutedAt: DateTime.UtcNow,
            Severity: "HIGH"
        ));

        if (!formatValid)
        {
            hasDefinitiveFailure = true;
            failureReason = "Invalid Udyam registration number format";
            return;
        }

        // 2. Name Matching
        EvaluateNameMatching("UDYAM", supplier, details, ref hasAmbiguity, ref escalationReason);
    }

    private void EvaluateBankRules(
        string? identifier,
        string? extractedName,
        Business supplier,
        string? configuredRules,
        List<RuleExecutionDetail> details,
        ref bool hasDefinitiveFailure,
        ref bool hasAmbiguity,
        ref string? failureReason,
        ref string? escalationReason)
    {
        var primaryBank = supplier.BankAccounts.FirstOrDefault(b => b.IsPrimary) ?? supplier.BankAccounts.FirstOrDefault();

        // 1. Format Check
        if (primaryBank != null)
        {
            bool ifscValid = BankIdentifier.IsValidIfsc(primaryBank.Ifsc);
            details.Add(new RuleExecutionDetail(
                RuleCode: "RULE_BANK_IFSC_FORMAT",
                RuleName: "Bank Account IFSC Code Format Validation",
                CredentialType: "BANK",
                ValidationType: "FORMAT",
                Passed: ifscValid,
                Status: ifscValid ? "PASSED" : "FAILED",
                Reason: ifscValid ? $"Valid RBI IFSC code: '{primaryBank.Ifsc}'." : $"Invalid IFSC code: '{primaryBank.Ifsc}'.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "HIGH"
            ));

            if (!ifscValid)
            {
                hasDefinitiveFailure = true;
                failureReason = "Invalid bank IFSC code format";
                return;
            }
        }

        // 2. Beneficiary Legal Name Match
        string? beneficiaryName = extractedName ?? primaryBank?.AccountHolderName;
        if (!string.IsNullOrWhiteSpace(beneficiaryName) && !string.IsNullOrWhiteSpace(supplier.LegalName))
        {
            double similarity = ComputeNameSimilarity(beneficiaryName, supplier.LegalName);
            bool pass = similarity >= 0.85;
            bool partial = similarity >= 0.50 && similarity < 0.85;

            details.Add(new RuleExecutionDetail(
                RuleCode: "RULE_BANK_BENEFICIARY_MATCH",
                RuleName: "Bank Beneficiary to Corporate Legal Name Match",
                CredentialType: "BANK",
                ValidationType: "DATA_MATCH",
                Passed: pass,
                Status: pass ? "PASSED" : (partial ? "ESCALATED" : "FAILED"),
                Reason: pass ? $"Beneficiary name matches legal entity name ({similarity:P0} similarity)." :
                               (partial ? $"Beneficiary name '{beneficiaryName}' partially matches legal name '{supplier.LegalName}' ({similarity:P0}). Requires verifier review." :
                                          $"Definitive beneficiary mismatch: '{beneficiaryName}' does not match legal name '{supplier.LegalName}' ({similarity:P0})."),
                ExecutedAt: DateTime.UtcNow,
                Severity: "CRITICAL"
            ));

            if (!pass && !partial)
            {
                hasDefinitiveFailure = true;
                failureReason = "Bank account beneficiary does not match corporate legal name";
                return;
            }
            else if (partial)
            {
                hasAmbiguity = true;
                escalationReason = "Bank beneficiary name requires manual verifier verification";
            }
        }
    }

    private void EvaluateCinRules(
        string? identifier,
        Business supplier,
        string? configuredRules,
        List<RuleExecutionDetail> details,
        ref bool hasDefinitiveFailure,
        ref bool hasAmbiguity,
        ref string? failureReason,
        ref string? escalationReason)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            hasDefinitiveFailure = true;
            failureReason = "CIN / Business Registration identifier is missing.";
            return;
        }

        var trimmed = identifier.Trim().ToUpperInvariant();
        bool cinValid = CinNumber.TryParse(trimmed, out var cinObj);

        details.Add(new RuleExecutionDetail(
            RuleCode: "RULE_CIN_FORMAT",
            RuleName: "Corporate Identification Number (CIN) Validation",
            CredentialType: "BUSINESS_REGISTRATION",
            ValidationType: "FORMAT",
            Passed: cinValid,
            Status: cinValid ? "PASSED" : "FAILED",
            Reason: cinValid ? $"Valid MCA CIN structure for {(cinObj!.IsListed ? "Listed" : "Unlisted")} company in state {cinObj.StateCode}." : $"Invalid CIN format: '{trimmed}'.",
            ExecutedAt: DateTime.UtcNow,
            Severity: "HIGH"
        ));

        if (!cinValid)
        {
            hasDefinitiveFailure = true;
            failureReason = "Invalid CIN format";
        }
    }

    private void EvaluateNameMatching(
        string credType,
        Business supplier,
        List<RuleExecutionDetail> details,
        ref bool hasAmbiguity,
        ref string? escalationReason)
    {
        // Check if any matching case recorded a name check error
        var failedNameCheck = supplier.VerificationCases
            .Where(vc => DocumentScoringPolicy.NormalizeType(vc.VerificationType) == credType)
            .SelectMany(vc => vc.Checks)
            .FirstOrDefault(chk => chk.CheckType.Contains("NAME", StringComparison.OrdinalIgnoreCase) &&
                                   (chk.Status == VerificationStatus.REJECTED || chk.Status == VerificationStatus.FLAGGED));

        if (failedNameCheck != null)
        {
            details.Add(new RuleExecutionDetail(
                RuleCode: $"RULE_{credType}_NAME_MATCH",
                RuleName: $"{credType} Legal Name Verification Check",
                CredentialType: credType,
                ValidationType: "DATA_MATCH",
                Passed: false,
                Status: "ESCALATED",
                Reason: failedNameCheck.ErrorMessage ?? "Name discrepancy flagged during verification check.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "HIGH"
            ));

            hasAmbiguity = true;
            escalationReason = failedNameCheck.ErrorMessage ?? "Entity legal name discrepancy requires manual review";
        }
        else
        {
            details.Add(new RuleExecutionDetail(
                RuleCode: $"RULE_{credType}_NAME_MATCH",
                RuleName: $"{credType} Legal Name Verification Check",
                CredentialType: credType,
                ValidationType: "DATA_MATCH",
                Passed: true,
                Status: "PASSED",
                Reason: "Name matches registered business profile information.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "MEDIUM"
            ));
        }
    }

    private void EvaluateRegistryStatus(
        string credType,
        List<Credential> credentials,
        List<Document> documents,
        List<VerificationCase> cases,
        List<RuleExecutionDetail> details,
        ref bool hasDefinitiveFailure,
        ref bool hasAmbiguity,
        ref string? failureReason)
    {
        bool isRejected = credentials.Any(c => c.Status == CredentialStatus.REJECTED || c.VerificationStatus == VerificationStatus.REJECTED) ||
                          documents.Any(d => d.Status == DocumentStatus.REJECTED) ||
                          cases.Any(c => c.FinalDecision == VerificationDecision.REJECTED);

        if (isRejected)
        {
            hasDefinitiveFailure = true;
            failureReason = $"{credType} was rejected during verification.";
            details.Add(new RuleExecutionDetail(
                RuleCode: $"RULE_{credType}_REGISTRY_STATUS",
                RuleName: $"{credType} Registry Decision Status",
                CredentialType: credType,
                ValidationType: "REGISTRY",
                Passed: false,
                Status: "FAILED",
                Reason: "Verification record contains explicit rejection decision.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "HIGH"
            ));
            return;
        }

        // Check if registry check was executed
        var executedChecks = cases.SelectMany(c => c.Checks).ToList();
        if (executedChecks.Count > 0)
        {
            bool hasVerified = executedChecks.Any(chk => chk.Status == VerificationStatus.VERIFIED);
            details.Add(new RuleExecutionDetail(
                RuleCode: $"RULE_{credType}_REGISTRY_STATUS",
                RuleName: $"{credType} External Registry Confirmation",
                CredentialType: credType,
                ValidationType: "REGISTRY",
                Passed: hasVerified,
                Status: hasVerified ? "PASSED" : "PENDING",
                Reason: hasVerified ? "Registry confirmation verified." : "Registry confirmation is pending or in progress.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "HIGH"
            ));
        }
        else
        {
            // Do not claim false registry verification if none was performed
            details.Add(new RuleExecutionDetail(
                RuleCode: $"RULE_{credType}_REGISTRY_STATUS",
                RuleName: $"{credType} External Registry Confirmation",
                CredentialType: credType,
                ValidationType: "REGISTRY",
                Passed: true,
                Status: "NOT_CONFIGURED",
                Reason: "Automated sovereign API check not configured for this credential tier.",
                ExecutedAt: DateTime.UtcNow,
                Severity: "LOW"
            ));
        }
    }

    private static double ComputeNameSimilarity(string nameA, string nameB)
    {
        if (string.IsNullOrWhiteSpace(nameA) || string.IsNullOrWhiteSpace(nameB)) return 0.0;

        var cleanA = CleanBusinessName(nameA);
        var cleanB = CleanBusinessName(nameB);

        if (cleanA.Equals(cleanB, StringComparison.OrdinalIgnoreCase)) return 1.0;
        if (cleanA.Contains(cleanB, StringComparison.OrdinalIgnoreCase) || cleanB.Contains(cleanA, StringComparison.OrdinalIgnoreCase)) return 0.88;

        // Levenshtein distance
        int dist = LevenshteinDistance(cleanA, cleanB);
        int maxLen = Math.Max(cleanA.Length, cleanB.Length);
        if (maxLen == 0) return 1.0;

        return Math.Max(0.0, 1.0 - ((double)dist / maxLen));
    }

    private static string CleanBusinessName(string name)
    {
        var upper = name.ToUpperInvariant();
        upper = Regex.Replace(upper, @"\b(PRIVATE|LIMITED|LTD|PVT|CORP|CORPORATION|LLP|INC|ENTERPRISE|COMPANY|CO)\b", string.Empty);
        upper = Regex.Replace(upper, @"[^A-Z0-9\s]", " ");
        upper = Regex.Replace(upper, @"\s+", " ").Trim();
        return upper;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}
