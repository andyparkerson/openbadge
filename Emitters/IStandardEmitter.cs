namespace OpenBadge.Emitters;

/// <summary>
/// Interface for emitting Open Badges assertions in different standards (OB 2.0, OB 3.0)
/// </summary>
public interface IStandardEmitter
{
    /// <summary>
    /// Emits an assertion JSON for the specified standard
    /// </summary>
    /// <param name="request">The bake request containing award information</param>
    /// <param name="issuerUrl">The stable URL for the issuer JSON</param>
    /// <param name="badgeClassUrl">The stable URL for the badge class JSON</param>
    /// <returns>A tuple containing the assertion ID and the assertion JSON</returns>
    Task<(string assertionId, string assertionJson)> EmitAsync(
        Models.BakeRequest request,
        Uri issuerUrl,
        Uri badgeClassUrl,
        Uri assertionUrl);
}
