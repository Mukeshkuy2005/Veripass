using System.Text.Json.Serialization;

namespace VeriPass.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrganizationType
{
    SUPPLIER,
    BUYER,
    BOTH
}
