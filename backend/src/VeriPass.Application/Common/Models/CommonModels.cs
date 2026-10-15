namespace VeriPass.Application.Common.Models;

public record LookupDto(string Code, string Name, string? Category = null);

public record KeyValuePairDto<TKey, TValue>(TKey Key, TValue Value);
