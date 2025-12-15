using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpenBadge.Services;

/// <summary>
/// Service for publishing Open Badges artifacts to Azure Blob Storage
/// Handles both public JSON files (issuer, badge class, assertions) and private baked PNGs
/// </summary>
public class PublishingService
{
    private readonly ILogger<PublishingService> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _publicContainerName;
    private readonly string _bakedBadgesContainerName;
    private readonly string _baseUrl;

    public PublishingService(
        ILogger<PublishingService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        
        var connectionString = configuration["BlobStorageConnectionString"] 
            ?? throw new InvalidOperationException("BlobStorageConnectionString not configured");
        
        _blobServiceClient = new BlobServiceClient(connectionString);
        _publicContainerName = configuration["PublicContainerName"] ?? "public";
        _bakedBadgesContainerName = configuration["BakedBadgesContainerName"] ?? "badges-baked";
        _baseUrl = configuration["BaseUrl"] ?? "https://issuer.example.org";
    }

    /// <summary>
    /// Publishes issuer JSON to public blob storage
    /// </summary>
    public async Task<string> PublishIssuerAsync(string issuerId, Models.IssuerDto issuer)
    {
        var json = JsonSerializer.Serialize(new
        {
            context = "https://w3id.org/openbadges/v2",
            type = "Issuer",
            id = $"{_baseUrl}/api/issuer/{issuerId}",
            name = issuer.Name,
            url = issuer.Url,
            email = issuer.Email,
            description = issuer.Description,
            image = issuer.Image
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var blobName = $"issuers/{issuerId}.json";
        var url = await PublishJsonAsync(blobName, json);
        
        _logger.LogInformation("Published issuer {IssuerId} to {Url}", issuerId, url);
        return url;
    }

    /// <summary>
    /// Publishes badge class JSON to public blob storage
    /// </summary>
    public async Task<string> PublishBadgeClassAsync(string badgeClassId, Models.BadgeClassDto badgeClass, string issuerUrl)
    {
        var criteria = new { narrative = string.Join("; ", badgeClass.Criteria) };
        
        var json = JsonSerializer.Serialize(new
        {
            context = "https://w3id.org/openbadges/v2",
            type = "BadgeClass",
            id = $"{_baseUrl}/api/badgeclass/{badgeClassId}",
            name = badgeClass.Name,
            description = badgeClass.Description,
            image = badgeClass.Image,
            criteria = criteria,
            issuer = issuerUrl,
            tags = badgeClass.Tags
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var blobName = $"badgeclasses/{badgeClassId}.json";
        var url = await PublishJsonAsync(blobName, json);
        
        _logger.LogInformation("Published badge class {BadgeClassId} to {Url}", badgeClassId, url);
        return url;
    }

    /// <summary>
    /// Publishes assertion JSON to public blob storage
    /// </summary>
    public async Task<string> PublishAssertionAsync(string assertionId, string assertionJson)
    {
        var blobName = $"assertions/{assertionId}.json";
        var url = await PublishJsonAsync(blobName, assertionJson);
        
        _logger.LogInformation("Published assertion {AssertionId} to {Url}", assertionId, url);
        return url;
    }

    /// <summary>
    /// Returns the public blob URI for a JSON artifact (without uploading).
    /// Useful for constructing stable public URLs before publishing the content.
    /// </summary>
    public Uri GetPublicBlobUri(string blobName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_publicContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        return blobClient.Uri;
    }

    /// <summary>
    /// Stores a baked PNG in private blob storage and returns a SAS URL
    /// </summary>
    public async Task<string> StoreBakedPngAsync(string assertionId, byte[] bakedPng)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_bakedBadgesContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobName = $"{assertionId}.png";
        var blobClient = containerClient.GetBlobClient(blobName);

        using var stream = new MemoryStream(bakedPng);
        var uploadOptions = new Azure.Storage.Blobs.Models.BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "image/png"
            }
        };
        await blobClient.UploadAsync(stream, uploadOptions);

        // Generate SAS URL valid for 24 hours
        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(24));
        
        _logger.LogInformation("Stored baked PNG for assertion {AssertionId} ({Size} bytes)", 
            assertionId, bakedPng.Length);
        
        return sasUri.ToString();
    }

    /// <summary>
    /// Retrieves JSON from public blob storage
    /// </summary>
    public async Task<string?> GetJsonAsync(string type, string id)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_publicContainerName);
        
        var blobName = type.ToLowerInvariant() switch
        {
            "issuer" => $"issuers/{id}.json",
            "badgeclass" => $"badgeclasses/{id}.json",
            "assertion" => $"assertions/{id}.json",
            _ => throw new ArgumentException($"Unknown type: {type}", nameof(type))
        };

        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
        {
            _logger.LogWarning("Blob not found: {BlobName}", blobName);
            return null;
        }

        var response = await blobClient.DownloadContentAsync();
        return response.Value.Content.ToString();
    }

    private async Task<string> PublishJsonAsync(string blobName, string json)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_publicContainerName);
        
        // Create container with public blob access if it doesn't exist
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var blobClient = containerClient.GetBlobClient(blobName);

        var bytes = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(bytes);
        
        var uploadOptions = new Azure.Storage.Blobs.Models.BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/json",
                CacheControl = "public, max-age=31536000" // Cache for 1 year
            }
        };
        await blobClient.UploadAsync(stream, uploadOptions);

        return blobClient.Uri.ToString();
    }
}
