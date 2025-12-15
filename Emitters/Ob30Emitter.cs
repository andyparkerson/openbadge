using Microsoft.Extensions.Logging;

namespace OpenBadge.Emitters;

/// <summary>
/// Stub emitter for Open Badges 3.0 (Verifiable Credentials)
/// Future implementation will include VC signing per OB 3.0 specification
/// Reference: https://www.imsglobal.org/spec/ob/v3p0/impl/
/// </summary>
public class Ob30Emitter : IStandardEmitter
{
    private readonly ILogger<Ob30Emitter> _logger;

    public Ob30Emitter(ILogger<Ob30Emitter> logger)
    {
        _logger = logger;
    }

    public Task<(string assertionId, string assertionJson)> EmitAsync(
        Models.BakeRequest request,
        Uri issuerUrl,
        Uri badgeClassUrl,
        Uri assertionUrl)
    {
        _logger.LogWarning("OB 3.0 emitter is not yet implemented");
        
        // TODO: Implement OB 3.0 OpenBadgeCredential generation
        // TODO: Add VC signing with cryptographic keys
        // TODO: Support embedded proof using JSON-LD signatures or JWT
        
        throw new NotImplementedException(
            "Open Badges 3.0 support is planned but not yet implemented. " +
            "Please use standard='ob2' for Open Badges 2.0.");
    }
}
