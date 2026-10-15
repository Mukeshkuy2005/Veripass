using VeriPass.Domain.Entities;

namespace VeriPass.Application.Common.Interfaces;

public record RecordAuditOptions(
    string Action,
    string Category,
    string TargetType,
    string? TargetId = null,
    string? TargetName = null,
    string Result = "SUCCESS",
    string? MetadataJson = null,
    string? OldValuesJson = null,
    string? NewValuesJson = null,
    Guid? OverrideOrganizationId = null,
    Guid? OverrideUserId = null,
    string? FailureReason = null);

public interface IAuditService
{
    Task<AuditLog> RecordAsync(RecordAuditOptions options, CancellationToken cancellationToken = default);
}
