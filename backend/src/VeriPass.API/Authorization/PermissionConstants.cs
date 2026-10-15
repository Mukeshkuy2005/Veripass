namespace VeriPass.API.Authorization;

public static class PermissionConstants
{
    public const string BusinessRead = "business:read";
    public const string BusinessWrite = "business:write";
    public const string BusinessAdmin = "business:admin";

    public const string DocumentUpload = "document:upload";
    public const string DocumentRead = "document:read";
    public const string DocumentDelete = "document:delete";

    public const string VerificationApprove = "verification:approve";
    public const string VerificationReject = "verification:reject";
    public const string VerificationQueue = "verification:queue";

    public const string PassportShare = "passport:share";
    public const string PassportPublish = "passport:publish";

    public const string RelationshipManage = "relationship:manage";
    public const string RelationshipInvite = "relationship:invite";

    public const string AdminAccess = "admin:access";
}
