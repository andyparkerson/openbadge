namespace OpenBadge.Models;

/// <summary>
/// Represents an Open Badges issuer organization
/// </summary>
public class IssuerDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Image { get; set; }
}

/// <summary>
/// Represents an Open Badges badge class definition
/// </summary>
public class BadgeClassDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string[] Criteria { get; set; } = Array.Empty<string>();
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Represents a badge recipient
/// </summary>
public class RecipientDto
{
    public string Type { get; set; } = "email";
    public string Identity { get; set; } = string.Empty;
    public bool Hashed { get; set; }
    public string? Salt { get; set; }
}

/// <summary>
/// Request to award a badge to a recipient
/// </summary>
public class AwardRequestDto
{
    public IssuerDto Issuer { get; set; } = new();
    public BadgeClassDto BadgeClass { get; set; } = new();
    public RecipientDto Recipient { get; set; } = new();
    public DateTimeOffset? IssuedOn { get; set; }
    public DateTimeOffset? Expires { get; set; }
    public string? Evidence { get; set; }
}

/// <summary>
/// Request to bake a badge into a PNG image
/// </summary>
public class BakeRequest
{
    public string Standard { get; set; } = "ob2";
    public AwardRequestDto Award { get; set; } = new();
}

/// <summary>
/// Response from baking a badge
/// </summary>
public class BakeResponse
{
    public string IssuerUrl { get; set; } = string.Empty;
    public string BadgeClassUrl { get; set; } = string.Empty;
    public string AssertionUrl { get; set; } = string.Empty;
    public string BakedPngUrl { get; set; } = string.Empty;
}

/// <summary>
/// Response from verifying a badge
/// </summary>
public class VerifyResponse
{
    public bool Valid { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public VerificationDetails? Details { get; set; }
}

/// <summary>
/// Details about the verified badge
/// </summary>
public class VerificationDetails
{
    public string? AssertionId { get; set; }
    public string? BadgeClassName { get; set; }
    public string? IssuerName { get; set; }
    public string? RecipientIdentity { get; set; }
    public DateTimeOffset? IssuedOn { get; set; }
    public DateTimeOffset? Expires { get; set; }
    public bool? IsExpired { get; set; }
}
