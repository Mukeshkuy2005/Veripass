using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using VeriPass.Application.Common.Interfaces;
using VeriPass.Application.Verification;
using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Infrastructure.Persistence;
using Xunit;

namespace VeriPass.Application.UnitTests;

public class EscalatedCasesTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetEscalatedCases_ReturnsCorrectSummaryCountsAndServerTimeUtc()
    {
        using var context = CreateDbContext();
        var currentUserId = Guid.NewGuid();
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.UserId).Returns(currentUserId);

        var biz1 = new Business { Id = Guid.NewGuid(), LegalName = "ABC Manufacturing Pvt Ltd", Gstin = "09ABCDE1234F1Z5" };
        var biz2 = new Business { Id = Guid.NewGuid(), LegalName = "XYZ Industries Limited", Gstin = "27AAACX1234F1Z8" };
        context.Businesses.AddRange(biz1, biz2);

        // Case 1: Assigned to current user
        context.VerificationCases.Add(new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz1.Id,
            Business = biz1,
            VerificationType = "GST",
            Status = VerificationStatus.FLAGGED,
            FinalDecision = VerificationDecision.ESCALATED,
            DecisionReason = "Conflicting GST identity information",
            Priority = "HIGH",
            RiskLevel = RiskLevel.HIGH,
            AssignedVerifierId = currentUserId,
            EscalatedAt = DateTime.UtcNow.AddMinutes(-30)
        });

        // Case 2: Unassigned
        context.VerificationCases.Add(new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz2.Id,
            Business = biz2,
            VerificationType = "MSME",
            Status = VerificationStatus.FLAGGED,
            FinalDecision = VerificationDecision.ESCALATED,
            DecisionReason = "Evidence unclear on manufacturing turnover",
            Priority = "MEDIUM",
            RiskLevel = RiskLevel.MEDIUM,
            AssignedVerifierId = null,
            EscalatedAt = DateTime.UtcNow.AddMinutes(-10)
        });

        // Case 3: Regular case (not escalated)
        context.VerificationCases.Add(new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz1.Id,
            Business = biz1,
            VerificationType = "PAN",
            Status = VerificationStatus.IN_PROGRESS,
            FinalDecision = VerificationDecision.NONE,
            Priority = "NORMAL",
            RiskLevel = RiskLevel.LOW
        });

        await context.SaveChangesAsync();

        var handler = new GetEscalatedCasesQueryHandler(context, mockUserService.Object);

        // Act - Query All
        var resultAll = await handler.Handle(new GetEscalatedCasesQuery(View: "all"), CancellationToken.None);

        // Assert
        resultAll.IsSuccess.Should().BeTrue();
        resultAll.Value.TotalCount.Should().Be(2);
        resultAll.Value.Summary.TotalCount.Should().Be(2);
        resultAll.Value.Summary.AssignedCount.Should().Be(1);
        resultAll.Value.Summary.UnassignedCount.Should().Be(1);
        resultAll.Value.ServerTimeUtc.Kind.Should().Be(DateTimeKind.Utc);

        // Act - Query Mine
        var resultMine = await handler.Handle(new GetEscalatedCasesQuery(View: "mine"), CancellationToken.None);
        resultMine.IsSuccess.Should().BeTrue();
        resultMine.Value.TotalCount.Should().Be(1);
        resultMine.Value.Items[0].BusinessName.Should().Be("ABC Manufacturing Pvt Ltd");

        // Act - Query Unassigned
        var resultUnassigned = await handler.Handle(new GetEscalatedCasesQuery(View: "unassigned"), CancellationToken.None);
        resultUnassigned.IsSuccess.Should().BeTrue();
        resultUnassigned.Value.TotalCount.Should().Be(1);
        resultUnassigned.Value.Items[0].BusinessName.Should().Be("XYZ Industries Limited");
    }

    [Fact]
    public async Task GetEscalatedCases_FiltersBySearch_Priority_And_Reason()
    {
        using var context = CreateDbContext();
        var mockUserService = new Mock<ICurrentUserService>();

        var biz1 = new Business { Id = Guid.NewGuid(), LegalName = "Apex Global Corp", Gstin = "29ABCDE9999F1Z1" };
        var biz2 = new Business { Id = Guid.NewGuid(), LegalName = "Zenith Chemicals", Gstin = "33XYZDE8888F1Z2" };
        context.Businesses.AddRange(biz1, biz2);

        context.VerificationCases.Add(new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz1.Id,
            Business = biz1,
            VerificationType = "GST",
            Status = VerificationStatus.FLAGGED,
            FinalDecision = VerificationDecision.ESCALATED,
            DecisionReason = "Conflicting sovereign tax filing data",
            Priority = "HIGH",
            RiskLevel = RiskLevel.HIGH,
            EscalatedAt = DateTime.UtcNow
        });

        context.VerificationCases.Add(new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz2.Id,
            Business = biz2,
            VerificationType = "MSME",
            Status = VerificationStatus.FLAGGED,
            FinalDecision = VerificationDecision.ESCALATED,
            DecisionReason = "Address mismatch with utility proof",
            Priority = "LOW",
            RiskLevel = RiskLevel.LOW,
            EscalatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var handler = new GetEscalatedCasesQueryHandler(context, mockUserService.Object);

        // Search by GSTIN
        var resGst = await handler.Handle(new GetEscalatedCasesQuery(Search: "29ABCDE9999"), CancellationToken.None);
        resGst.Value.TotalCount.Should().Be(1);
        resGst.Value.Items[0].BusinessName.Should().Be("Apex Global Corp");

        // Priority filter
        var resPriority = await handler.Handle(new GetEscalatedCasesQuery(Priority: "HIGH"), CancellationToken.None);
        resPriority.Value.TotalCount.Should().Be(1);
        resPriority.Value.Items[0].Priority.Should().Be("HIGH");

        // Reason filter
        var resReason = await handler.Handle(new GetEscalatedCasesQuery(Reason: "utility proof"), CancellationToken.None);
        resReason.Value.TotalCount.Should().Be(1);
        resReason.Value.Items[0].BusinessName.Should().Be("Zenith Chemicals");
    }

    [Fact]
    public async Task GetEscalationDetail_ReturnsAccurateDetailWithAuditAttemptsAndEvidences()
    {
        using var context = CreateDbContext();
        var mockUserService = new Mock<ICurrentUserService>();
        var verifierId = Guid.NewGuid();
        var verifierUser = new User { Id = verifierId, FirstName = "Rahul", LastName = "Sharma", Email = "rahul@veripass.in" };
        context.Users.Add(verifierUser);

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "ABC Manufacturing Pvt Ltd", Gstin = "09ABCDE1234F1Z5" };
        context.Businesses.Add(biz);

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            DocumentType = "GST_CERTIFICATE",
            OriginalFileName = "gst_certificate_signed.pdf",
            Status = DocumentStatus.PROCESSED,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        context.Documents.Add(doc);

        var caseItem = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Business = biz,
            VerificationType = "GST",
            Status = VerificationStatus.FLAGGED,
            FinalDecision = VerificationDecision.ESCALATED,
            DecisionReason = "Conflicting GST registration details",
            Priority = "HIGH",
            RiskLevel = RiskLevel.HIGH,
            AssignedVerifierId = verifierId,
            AssignedVerifier = verifierUser,
            EscalatedAt = DateTime.UtcNow.AddMinutes(-45)
        };
        context.VerificationCases.Add(caseItem);

        var evidence = new VerificationEvidence
        {
            Id = Guid.NewGuid(),
            VerificationCaseId = caseItem.Id,
            DocumentId = doc.Id,
            Document = doc,
            EvidenceType = "GST_CERTIFICATE",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        context.VerificationEvidences.Add(evidence);

        // Audit events for previous review attempts
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = verifierId,
            Action = "VERIFICATION_REVIEW_STARTED",
            Category = "VERIFICATION",
            ResourceType = "VERIFICATION_CASE",
            ResourceId = caseItem.Id.ToString(),
            TargetName = biz.LegalName,
            Result = "SUCCESS",
            EventCode = "AUD-101",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        });

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = verifierId,
            Action = "VERIFICATION_ESCALATED",
            Category = "VERIFICATION",
            ResourceType = "VERIFICATION_CASE",
            ResourceId = caseItem.Id.ToString(),
            TargetName = biz.LegalName,
            Result = "SUCCESS",
            EventCode = "AUD-102",
            CreatedAt = DateTime.UtcNow.AddMinutes(-45)
        });

        await context.SaveChangesAsync();

        var handler = new GetEscalationDetailQueryHandler(context, mockUserService.Object);

        // Act
        var detailResult = await handler.Handle(new GetEscalationDetailQuery(caseItem.Id.ToString()), CancellationToken.None);

        // Assert
        detailResult.IsSuccess.Should().BeTrue();
        var d = detailResult.Value;
        d.BusinessName.Should().Be("ABC Manufacturing Pvt Ltd");
        d.EscalationReason.Should().Be("Conflicting GST registration details");
        d.RiskLevel.Should().Be("HIGH");
        d.RiskScore.Should().Be(72);
        d.AssignedVerifierName.Should().Be("Rahul Sharma");
        d.Evidences.Should().HaveCount(1);
        d.Evidences[0].FileName.Should().Be("gst_certificate_signed.pdf");
        d.PreviousReview.AttemptCount.Should().Be(2);
        d.PreviousReview.VerifierName.Should().Be("Rahul Sharma");
        d.CanSeniorReview.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnEscalatedCase_EnforcesReason_And_TransitionsCaseBackToNormalQueue()
    {
        using var context = CreateDbContext();
        var seniorReviewerId = Guid.NewGuid();
        var mockUserService = new Mock<ICurrentUserService>();
        mockUserService.Setup(s => s.UserId).Returns(seniorReviewerId);

        var biz = new Business { Id = Guid.NewGuid(), LegalName = "PQR Corp", Gstin = "19AABCP1234F1Z9" };
        context.Businesses.Add(biz);

        var verifierId = Guid.NewGuid();
        var caseItem = new VerificationCase
        {
            Id = Guid.NewGuid(),
            BusinessId = biz.Id,
            Business = biz,
            VerificationType = "GST",
            Status = VerificationStatus.FLAGGED,
            FinalDecision = VerificationDecision.ESCALATED,
            DecisionReason = "Requires senior compliance concurrence",
            Priority = "HIGH",
            AssignedVerifierId = verifierId,
            EscalatedAt = DateTime.UtcNow.AddHours(-1)
        };
        context.VerificationCases.Add(caseItem);
        await context.SaveChangesAsync();

        var handler = new ReturnEscalatedCaseCommandHandler(context, mockUserService.Object);

        // Test 1: Empty reason fails
        var failResult = await handler.Handle(new ReturnEscalatedCaseCommand(caseItem.Id, "   "), CancellationToken.None);
        failResult.IsSuccess.Should().BeFalse();
        failResult.Error.Should().Contain("return reason is required");

        // Test 2: Concurrency mismatch fails
        var staleTokenResult = await handler.Handle(new ReturnEscalatedCaseCommand(caseItem.Id, "Missing bank stamp", 999999999L), CancellationToken.None);
        staleTokenResult.IsSuccess.Should().BeFalse();
        staleTokenResult.Error.Should().Contain("modified by another user");

        // Test 3: Successful return
        var currentToken = caseItem.UpdatedAt?.Ticks ?? caseItem.CreatedAt.Ticks;
        var successResult = await handler.Handle(new ReturnEscalatedCaseCommand(caseItem.Id, "Please re-verify with alternate tax challan.", currentToken), CancellationToken.None);
        successResult.IsSuccess.Should().BeTrue();

        // Verify Database state
        var reloadedCase = await context.VerificationCases.FindAsync(caseItem.Id);
        reloadedCase!.Status.Should().Be(VerificationStatus.IN_PROGRESS);
        reloadedCase.FinalDecision.Should().Be(VerificationDecision.NONE);
        reloadedCase.DecisionReason.Should().Contain("Returned by Senior Reviewer");

        // Verify Audit Event
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.ResourceId == caseItem.Id.ToString() && a.Action == "VERIFICATION_RETURNED");
        audit.Should().NotBeNull();
        audit!.UserId.Should().Be(seniorReviewerId);
        audit.Result.Should().Be("SUCCESS");
    }
}
