namespace VeriPass.Domain.Enums;

public enum OnboardingStatus
{
    NEW = 0,
    REQUESTED = 1,
    IN_VERIFICATION = 2,
    ACTION_REQUIRED = 3,
    APPROVED = 4,
    REJECTED = 5,
    EXPIRED = 6,
    WITHDRAWN = 7
}
