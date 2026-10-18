using VeriPass.Domain.TrustScoring;
using Xunit;

namespace VeriPass.Domain.UnitTests;

public class BuyerTrustScoreCalculatorTests
{
    // 1. Buyer A example: weights 30/15/15/25/15, bank REJECTED -> 75, High.
    [Fact]
    public void Calculate_BuyerAExample_Weights30_15_15_25_15_BankRejected_Returns75High()
    {
        // Arrange: Weights 30/15/15/25/15 summing to 100. Bank (weight 25) is REJECTED.
        var inputs = new List<CredentialInput>
        {
            new(Weight: 30, IsRequired: true, Status: "APPROVED", CredentialType: "GST"),
            new(Weight: 15, IsRequired: true, Status: "APPROVED", CredentialType: "PAN"),
            new(Weight: 15, IsRequired: true, Status: "APPROVED", CredentialType: "UDYAM"),
            new(Weight: 25, IsRequired: false, Status: "REJECTED", CredentialType: "BANK"),
            new(Weight: 15, IsRequired: false, Status: "APPROVED", CredentialType: "ISO_9001")
        };

        // Act
        var result = TrustScoreCalculator.Calculate(inputs);

        // Assert
        Assert.Equal(75, result.Score);
        Assert.Equal(TrustLevel.High, result.TrustLevel);
        Assert.Equal(25, result.RiskScore);
        Assert.Equal(ComplianceStatus.Compliant, result.ComplianceStatus);
    }

    // 2. Buyer B example, same statuses, weights 20/10/10/40/20 -> 60, Medium.
    [Fact]
    public void Calculate_BuyerBExample_SameStatuses_Weights20_10_10_40_20_BankRejected_Returns60Medium()
    {
        // Arrange: Weights 20/10/10/40/20 summing to 100. Same statuses (Bank is weight 40 and REJECTED).
        var inputs = new List<CredentialInput>
        {
            new(Weight: 20, IsRequired: true, Status: "APPROVED", CredentialType: "GST"),
            new(Weight: 10, IsRequired: true, Status: "APPROVED", CredentialType: "PAN"),
            new(Weight: 10, IsRequired: true, Status: "APPROVED", CredentialType: "UDYAM"),
            new(Weight: 40, IsRequired: false, Status: "REJECTED", CredentialType: "BANK"),
            new(Weight: 20, IsRequired: false, Status: "APPROVED", CredentialType: "ISO_9001")
        };

        // Act
        var result = TrustScoreCalculator.Calculate(inputs);

        // Assert
        Assert.Equal(60, result.Score);
        Assert.Equal(TrustLevel.Medium, result.TrustLevel);
        Assert.Equal(40, result.RiskScore);
        Assert.Equal(ComplianceStatus.Compliant, result.ComplianceStatus);
    }

    // 3. Boundaries: scores of 34 vs 35 and 70 vs 71 land in the right band; verify rounding rule at x.5
    [Theory]
    [InlineData(34, TrustLevel.Low)]
    [InlineData(35, TrustLevel.Medium)]
    [InlineData(70, TrustLevel.Medium)]
    [InlineData(71, TrustLevel.High)]
    public void Calculate_BandingBoundaries_LandInExactCorrectBands(int score, TrustLevel expectedBand)
    {
        var inputs = new List<CredentialInput>
        {
            new(Weight: score, IsRequired: false, Status: "APPROVED"),
            new(Weight: 100 - score, IsRequired: false, Status: "MISSING")
        };

        var result = TrustScoreCalculator.Calculate(inputs);

        Assert.Equal(score, result.Score);
        Assert.Equal(expectedBand, result.TrustLevel);
        Assert.Equal(100 - score, result.RiskScore);
    }

    [Fact]
    public void Calculate_RoundingAtHalfAwayFromZero_34Point5RoundsTo35Medium()
    {
        // 69 / 200 = 0.345 * 100 = 34.5 -> rounds to 35 (Medium)
        var inputs = new List<CredentialInput>
        {
            new(Weight: 69, IsRequired: false, Status: "APPROVED"),
            new(Weight: 131, IsRequired: false, Status: "MISSING")
        };

        var result = TrustScoreCalculator.Calculate(inputs);

        Assert.Equal(35, result.Score);
        Assert.Equal(TrustLevel.Medium, result.TrustLevel);
        Assert.Equal(65, result.RiskScore);
    }

    [Fact]
    public void Calculate_RoundingAtHalfAwayFromZero_34Point4RoundsTo34Low()
    {
        // 172 / 500 = 0.344 * 100 = 34.4 -> rounds to 34 (Low)
        var inputs = new List<CredentialInput>
        {
            new(Weight: 172, IsRequired: false, Status: "APPROVED"),
            new(Weight: 328, IsRequired: false, Status: "MISSING")
        };

        var result = TrustScoreCalculator.Calculate(inputs);

        Assert.Equal(34, result.Score);
        Assert.Equal(TrustLevel.Low, result.TrustLevel);
        Assert.Equal(66, result.RiskScore);
    }

    [Fact]
    public void Calculate_RoundingAtHalfAwayFromZero_70Point5RoundsTo71High()
    {
        // 141 / 200 = 0.705 * 100 = 70.5 -> rounds to 71 (High)
        var inputs = new List<CredentialInput>
        {
            new(Weight: 141, IsRequired: false, Status: "APPROVED"),
            new(Weight: 59, IsRequired: false, Status: "MISSING")
        };

        var result = TrustScoreCalculator.Calculate(inputs);

        Assert.Equal(71, result.Score);
        Assert.Equal(TrustLevel.High, result.TrustLevel);
        Assert.Equal(29, result.RiskScore);
    }

    [Fact]
    public void Calculate_RoundingAtHalfAwayFromZero_70Point4RoundsTo70Medium()
    {
        // 352 / 500 = 0.704 * 100 = 70.4 -> rounds to 70 (Medium)
        var inputs = new List<CredentialInput>
        {
            new(Weight: 352, IsRequired: false, Status: "APPROVED"),
            new(Weight: 148, IsRequired: false, Status: "MISSING")
        };

        var result = TrustScoreCalculator.Calculate(inputs);

        Assert.Equal(70, result.Score);
        Assert.Equal(TrustLevel.Medium, result.TrustLevel);
        Assert.Equal(30, result.RiskScore);
    }

    // 4. All PENDING -> 0, Low. All APPROVED -> 100, High.
    [Fact]
    public void Calculate_AllPending_ReturnsScoreZeroAndLowTrust()
    {
        var inputs = new List<CredentialInput>
        {
            new(Weight: 50, IsRequired: true, Status: "PENDING", CredentialType: "GST"),
            new(Weight: 50, IsRequired: false, Status: "PENDING", CredentialType: "PAN")
        };

        var result = TrustScoreCalculator.Calculate(inputs);

        Assert.Equal(0, result.Score);
        Assert.Equal(TrustLevel.Low, result.TrustLevel);
        Assert.Equal(100, result.RiskScore);
        Assert.Equal(ComplianceStatus.Incomplete, result.ComplianceStatus);
        Assert.All(result.Breakdown, b => Assert.Equal(0, b.Contribution));
    }

    [Fact]
    public void Calculate_AllApproved_ReturnsScore100AndHighTrust()
    {
        var inputs = new List<CredentialInput>
        {
            new(Weight: 30, IsRequired: true, Status: "APPROVED", CredentialType: "GST"),
            new(Weight: 30, IsRequired: true, Status: "APPROVED", CredentialType: "PAN"),
            new(Weight: 40, IsRequired: false, Status: "APPROVED", CredentialType: "ISO")
        };

        var result = TrustScoreCalculator.Calculate(inputs);

        Assert.Equal(100, result.Score);
        Assert.Equal(TrustLevel.High, result.TrustLevel);
        Assert.Equal(0, result.RiskScore);
        Assert.Equal(ComplianceStatus.Compliant, result.ComplianceStatus);
        Assert.All(result.Breakdown, b => Assert.Equal(1, b.Contribution));
    }

    // 5. sum(W)=0 -> no exception, returns score 0 and Incomplete
    [Fact]
    public void Calculate_WhenSumOfWeightsIsZero_DoesNotThrow_ReturnsScoreZeroAndIncomplete()
    {
        // Empty list
        var emptyInputs = new List<(int weight, bool isRequired, string status, bool ruleFailed)>();
        var emptyResult = TrustScoreCalculator.Calculate(emptyInputs);

        Assert.Equal(0, emptyResult.Score);
        Assert.Equal(TrustLevel.Low, emptyResult.TrustLevel);
        Assert.Equal(100, emptyResult.RiskScore);
        Assert.Equal(ComplianceStatus.Incomplete, emptyResult.ComplianceStatus);

        // List with explicit zero weights
        var zeroWeightInputs = new List<CredentialInput>
        {
            new(Weight: 0, IsRequired: true, Status: "APPROVED"),
            new(Weight: 0, IsRequired: false, Status: "PENDING")
        };
        var zeroResult = TrustScoreCalculator.Calculate(zeroWeightInputs);

        Assert.Equal(0, zeroResult.Score);
        Assert.Equal(TrustLevel.Low, zeroResult.TrustLevel);
        Assert.Equal(100, zeroResult.RiskScore);
        Assert.Equal(ComplianceStatus.Incomplete, zeroResult.ComplianceStatus);
    }

    // 6. Required credential REJECTED with score 75 -> High Trust AND VerificationFailed (the contradiction case).
    [Fact]
    public void Calculate_ContradictionCase_RequiredCredentialRejectedWithScore75_YieldsHighTrustAndVerificationFailed()
    {
        // Arrange: Weights 30/15/15/25/15 = 100. Bank is REQUIRED and REJECTED.
        // Score = 30 + 15 + 15 + 0 + 15 = 75 (High Trust)
        // But Bank is Required and REJECTED -> ComplianceStatus MUST be VerificationFailed
        var inputs = new List<CredentialInput>
        {
            new(Weight: 30, IsRequired: true, Status: "APPROVED", CredentialType: "GST"),
            new(Weight: 15, IsRequired: true, Status: "APPROVED", CredentialType: "PAN"),
            new(Weight: 15, IsRequired: true, Status: "APPROVED", CredentialType: "UDYAM"),
            new(Weight: 25, IsRequired: true, Status: "REJECTED", CredentialType: "BANK"),
            new(Weight: 15, IsRequired: false, Status: "APPROVED", CredentialType: "ISO_9001")
        };

        // Act
        var result = TrustScoreCalculator.Calculate(inputs);

        // Assert
        Assert.Equal(75, result.Score);
        Assert.Equal(TrustLevel.High, result.TrustLevel);
        Assert.Equal(25, result.RiskScore);
        Assert.Equal(ComplianceStatus.VerificationFailed, result.ComplianceStatus);
    }

    // 7. Required PENDING -> Incomplete. Optional missing -> score drops, compliance unaffected.
    [Fact]
    public void Calculate_RequiredPending_ReturnsIncomplete()
    {
        var inputs = new List<CredentialInput>
        {
            new(Weight: 60, IsRequired: true, Status: "APPROVED", CredentialType: "GST"),
            new(Weight: 40, IsRequired: true, Status: "PENDING", CredentialType: "PAN")
        };

        var result = TrustScoreCalculator.Calculate(inputs);

        Assert.Equal(60, result.Score);
        Assert.Equal(40, result.RiskScore);
        Assert.Equal(TrustLevel.Medium, result.TrustLevel);
        Assert.Equal(ComplianceStatus.Incomplete, result.ComplianceStatus);
    }

    [Fact]
    public void Calculate_OptionalMissing_ScoreDrops_ComplianceUnaffected()
    {
        // Required GST (70) is APPROVED. Optional ISO (30) is MISSING.
        // Score drops from 100 to 70 (Medium Trust), but compliance remains Compliant!
        var inputs = new List<CredentialInput>
        {
            new(Weight: 70, IsRequired: true, Status: "APPROVED", CredentialType: "GST"),
            new(Weight: 30, IsRequired: false, Status: "MISSING", CredentialType: "ISO_9001")
        };

        var result = TrustScoreCalculator.Calculate(inputs);

        Assert.Equal(70, result.Score);
        Assert.Equal(30, result.RiskScore);
        Assert.Equal(TrustLevel.Medium, result.TrustLevel);
        Assert.Equal(ComplianceStatus.Compliant, result.ComplianceStatus);
        Assert.Equal(0, result.Breakdown[1].Contribution);
        Assert.Equal(0, result.Breakdown[1].PointsEarned);
    }

    // 8. Rule failure forces contribution 0 even when status is APPROVED.
    [Fact]
    public void Calculate_RuleFailureForcesContributionZero_EvenWhenStatusIsApproved()
    {
        // Status is APPROVED, but verification rule failed (e.g. legal name mismatch)
        var inputs = new List<CredentialInput>
        {
            new(Weight: 50, IsRequired: true, Status: "APPROVED", RuleFailed: true, CredentialType: "BANK", RuleFailedReason: "Account holder name mismatch"),
            new(Weight: 50, IsRequired: false, Status: "APPROVED", RuleFailed: false, CredentialType: "ISO_9001")
        };

        var result = TrustScoreCalculator.Calculate(inputs);

        // Score: Bank contribution is forced to 0 despite being APPROVED.
        // Only ISO contributes 50.
        Assert.Equal(50, result.Score);
        Assert.Equal(50, result.RiskScore);
        Assert.Equal(TrustLevel.Medium, result.TrustLevel);
        Assert.Equal(ComplianceStatus.VerificationFailed, result.ComplianceStatus);
        Assert.Equal(0, result.Breakdown[0].Contribution);
        Assert.Equal(0, result.Breakdown[0].PointsEarned);
        Assert.Equal("Account holder name mismatch", result.Breakdown[0].RuleFailedReason);
    }

    // 9. EXPIRED behaves like REJECTED for score and compliance.
    [Fact]
    public void Calculate_ExpiredBehavesLikeRejected_ForScoreAndCompliance()
    {
        // Case A: Bank is REJECTED
        var inputsRejected = new List<CredentialInput>
        {
            new(Weight: 60, IsRequired: true, Status: "APPROVED", CredentialType: "GST"),
            new(Weight: 40, IsRequired: true, Status: "REJECTED", CredentialType: "BANK")
        };
        var resultRejected = TrustScoreCalculator.Calculate(inputsRejected);

        // Case B: Bank is EXPIRED
        var inputsExpired = new List<CredentialInput>
        {
            new(Weight: 60, IsRequired: true, Status: "APPROVED", CredentialType: "GST"),
            new(Weight: 40, IsRequired: true, Status: "EXPIRED", CredentialType: "BANK")
        };
        var resultExpired = TrustScoreCalculator.Calculate(inputsExpired);

        // Assert identical score, trust level, risk score, and compliance status
        Assert.Equal(resultRejected.Score, resultExpired.Score);
        Assert.Equal(resultRejected.TrustLevel, resultExpired.TrustLevel);
        Assert.Equal(resultRejected.RiskScore, resultExpired.RiskScore);
        Assert.Equal(resultRejected.ComplianceStatus, resultExpired.ComplianceStatus);
        Assert.Equal(ComplianceStatus.VerificationFailed, resultExpired.ComplianceStatus);
        Assert.Equal(0, resultExpired.Breakdown[1].Contribution);
    }
}
