using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenBadge.Emitters;

/// <summary>
/// Emitter for Open Badges 2.0 assertions
/// Implements the OB 2.0 specification with hosted verification
/// Reference: https://www.imsglobal.org/sites/default/files/Badges/OBv2p0Final/index.html
/// </summary>
public class Ob20Emitter : IStandardEmitter
{
    private readonly ILogger<Ob20Emitter> _logger;
    private readonly string _baseUrl;

    public Ob20Emitter(ILogger<Ob20Emitter> logger, IConfiguration configuration)
    {
        _logger = logger;
        _baseUrl = configuration["BaseUrl"] ?? "https://issuer.example.org";
    }

    public async Task<(string assertionId, string assertionJson)> EmitAsync(
        Models.BakeRequest request,
        Uri issuerUrl,
        Uri badgeClassUrl,
        Uri assertionUrl)
    {
        var award = request.Award;
        var recipient = award.Recipient;

        // Use the assertion blob name to derive the assertion id (GUID)
        var assertionId = Path.GetFileNameWithoutExtension(assertionUrl.LocalPath);

        // Hash the recipient email with salt for privacy
        var salt = Guid.NewGuid().ToString("N");
        var hashedIdentity = HashEmailWithSalt(recipient.Identity, salt);

        // Build OB 2.0 Assertion object as a dictionary so we can use the "@context" key
        var assertion = new Dictionary<string, object?>
        {
            ["@context"] = "https://w3id.org/openbadges/v2",
            ["type"] = "Assertion",
            // Keep the assertion id as the function endpoint; verification.url points to the blob JSON
            ["id"] = $"{_baseUrl}/api/assertion/{assertionId}",
            ["recipient"] = new Dictionary<string, object?>
            {
                ["type"] = "email",
                ["identity"] = $"sha256${hashedIdentity}",
                ["hashed"] = true,
                ["salt"] = salt
            },
            ["badge"] = badgeClassUrl.ToString(),
            ["verification"] = new Dictionary<string, object?>
            {
                ["type"] = "hosted",
                ["url"] = assertionUrl.ToString()
            },
            ["issuedOn"] = (award.IssuedOn ?? DateTimeOffset.UtcNow).ToString("o"),
            ["expires"] = award.Expires?.ToString("o"),
            ["evidence"] = award.Evidence
        };

        // Serialize to JSON (remove null values)
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(assertion, options);

        _logger.LogInformation("Generated OB 2.0 assertion {AssertionId}", assertionId);

        return await Task.FromResult((assertionId, json));
    }

    /// <summary>
    /// Hashes an email address with a salt using SHA256
    /// Format: sha256(email + salt) as hex string
    /// </summary>
    private string HashEmailWithSalt(string email, string salt)
    {
        var input = email.ToLowerInvariant() + salt;
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
