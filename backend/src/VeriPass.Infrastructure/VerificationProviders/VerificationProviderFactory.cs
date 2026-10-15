using VeriPass.Application.Common.Interfaces;

namespace VeriPass.Infrastructure.VerificationProviders;

public class VerificationProviderFactory : IVerificationProviderFactory
{
    private readonly Dictionary<string, IVerificationProvider> _providers;

    public VerificationProviderFactory()
    {
        var providers = new IVerificationProvider[]
        {
            new GstVerificationProvider(),
            new PanVerificationProvider(),
            new McaVerificationProvider(),
            new UdyamVerificationProvider(),
            new BankVerificationProvider()
        };

        _providers = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
    }

    public IVerificationProvider GetProvider(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var provider))
            return provider;

        return _providers["GSTN"];
    }
}
