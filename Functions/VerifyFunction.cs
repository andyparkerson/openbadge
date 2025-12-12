using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenBadge.Models;
using OpenBadge.Services;
using System.Text.Json;

namespace OpenBadge.Functions;

/// <summary>
/// Azure Function for verifying Open Badges
/// Validates badge assertions from baked PNGs or assertion URLs
/// </summary>
public class VerifyFunction
{
    private readonly ILogger<VerifyFunction> _logger;
    private readonly PngBadgeBaker _badgeBaker;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNameCaseInsensitive = true 
    };

    public VerifyFunction(
        ILogger<VerifyFunction> logger,
        PngBadgeBaker badgeBaker,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _badgeBaker = badgeBaker;
        _httpClientFactory = httpClientFactory;
    }

    [Function("Verify")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "verify")] HttpRequest req)
    {
        _logger.LogInformation("Verify endpoint called");

        try
        {
            var response = new VerifyResponse();
            string? assertionJson = null;

            // Check if request contains a file (baked PNG) or URL
            if (req.HasFormContentType)
            {
                var form = await req.ReadFormAsync();
                var pngFile = form.Files["png"];

                if (pngFile != null && pngFile.Length > 0)
                {
                    // Extract assertion from baked PNG
                    _logger.LogInformation("Extracting assertion from baked PNG");
                    
                    byte[] pngBytes;
                    using (var ms = new MemoryStream())
                    {
                        await pngFile.CopyToAsync(ms);
                        pngBytes = ms.ToArray();
                    }

                    try
                    {
                        assertionJson = _badgeBaker.Unbake(pngBytes);
                        if (string.IsNullOrEmpty(assertionJson))
                        {
                            response.Valid = false;
                            response.Message = "No assertion found in PNG";
                            response.Errors.Add("The provided PNG does not contain a baked Open Badges assertion");
                            return new OkObjectResult(response);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error extracting assertion from PNG");
                        response.Valid = false;
                        response.Message = "Invalid PNG file";
                        response.Errors.Add("Failed to extract assertion from PNG");
                        return new OkObjectResult(response);
                    }
                }
                else
                {
                    // Check for URL in form data
                    var url = form["url"].ToString();
                    if (!string.IsNullOrEmpty(url))
                    {
                        assertionJson = await FetchAssertionFromUrlAsync(url, response);
                    }
                }
            }
            else
            {
                // Try to read JSON body
                using var reader = new StreamReader(req.Body);
                var body = await reader.ReadToEndAsync();
                
                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        var requestData = JsonSerializer.Deserialize<Dictionary<string, string>>(body, JsonOptions);
                        
                        if (requestData != null && requestData.TryGetValue("url", out var url))
                        {
                            assertionJson = await FetchAssertionFromUrlAsync(url, response);
                        }
                    }
                    catch (JsonException)
                    {
                        // Body might be the assertion JSON itself
                        assertionJson = body;
                    }
                }
            }

            if (string.IsNullOrEmpty(assertionJson))
            {
                response.Valid = false;
                response.Message = "No assertion provided";
                response.Errors.Add("Please provide either a baked PNG file, assertion URL, or assertion JSON");
                return new BadRequestObjectResult(response);
            }

            // Validate the assertion
            await ValidateAssertionAsync(assertionJson, response);

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing verify request");
            return new ObjectResult(new
            {
                valid = false,
                message = "Internal server error",
                errors = new[] { ex.Message }
            })
            {
                StatusCode = 500
            };
        }
    }

    private async Task<string?> FetchAssertionFromUrlAsync(string url, VerifyResponse response)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            response.Errors.Add($"Invalid URL format: {url}");
            return null;
        }

        try
        {
            _logger.LogInformation("Fetching assertion from URL: {Url}", url);
            using var httpClient = _httpClientFactory.CreateClient();
            var assertionJson = await httpClient.GetStringAsync(uri);
            return assertionJson;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error fetching assertion from URL");
            response.Errors.Add("Failed to fetch assertion from URL");
            return null;
        }
    }

    private async Task ValidateAssertionAsync(string assertionJson, VerifyResponse response)
    {
        try
        {
            // Parse the assertion JSON
            using var doc = JsonDocument.Parse(assertionJson);
            var root = doc.RootElement;

            response.Details = new VerificationDetails();

            // Check for required fields based on OB 2.0 spec
            if (!root.TryGetProperty("type", out var typeElement) || 
                typeElement.GetString() != "Assertion")
            {
                response.Errors.Add("Missing or invalid 'type' field (expected 'Assertion')");
            }

            // Extract assertion ID
            if (root.TryGetProperty("id", out var idElement))
            {
                response.Details.AssertionId = idElement.GetString();
            }
            else
            {
                response.Errors.Add("Missing required 'id' field");
            }

            // Check recipient
            if (!root.TryGetProperty("recipient", out var recipientElement))
            {
                response.Errors.Add("Missing required 'recipient' field");
            }
            else
            {
                if (recipientElement.TryGetProperty("identity", out var identityElement))
                {
                    response.Details.RecipientIdentity = identityElement.GetString();
                }
            }

            // Check badge reference
            string? badgeUrl = null;
            if (root.TryGetProperty("badge", out var badgeElement))
            {
                badgeUrl = badgeElement.GetString();
            }
            else
            {
                response.Errors.Add("Missing required 'badge' field");
            }

            // Check verification info
            if (!root.TryGetProperty("verification", out var verificationElement))
            {
                response.Warnings.Add("Missing 'verification' field");
            }

            // Check issuedOn date
            if (root.TryGetProperty("issuedOn", out var issuedOnElement))
            {
                if (DateTimeOffset.TryParse(issuedOnElement.GetString(), out var issuedOn))
                {
                    response.Details.IssuedOn = issuedOn;
                }
            }

            // Check expiration
            if (root.TryGetProperty("expires", out var expiresElement))
            {
                if (DateTimeOffset.TryParse(expiresElement.GetString(), out var expires))
                {
                    response.Details.Expires = expires;
                    response.Details.IsExpired = expires < DateTimeOffset.UtcNow;
                    
                    if (response.Details.IsExpired == true)
                    {
                        response.Errors.Add($"Badge expired on {expires:yyyy-MM-dd}");
                    }
                }
            }

            // Fetch and validate badge class if URL is valid
            if (!string.IsNullOrEmpty(badgeUrl) && Uri.TryCreate(badgeUrl, UriKind.Absolute, out _))
            {
                await ValidateBadgeClassAsync(badgeUrl, response);
            }

            // Set overall validation result
            response.Valid = response.Errors.Count == 0;
            response.Message = response.Valid 
                ? "Badge assertion is valid" 
                : "Badge assertion validation failed";
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing assertion JSON");
            response.Valid = false;
            response.Message = "Invalid assertion JSON";
            response.Errors.Add($"Failed to parse assertion: {ex.Message}");
        }
    }

    private async Task ValidateBadgeClassAsync(string badgeUrl, VerifyResponse response)
    {
        try
        {
            _logger.LogInformation("Fetching badge class from: {BadgeUrl}", badgeUrl);
            using var httpClient = _httpClientFactory.CreateClient();
            var badgeJson = await httpClient.GetStringAsync(badgeUrl);
            
            using var doc = JsonDocument.Parse(badgeJson);
            var root = doc.RootElement;

            // Extract badge class name
            if (root.TryGetProperty("name", out var nameElement))
            {
                response.Details!.BadgeClassName = nameElement.GetString();
            }

            // Get issuer reference
            string? issuerUrl = null;
            if (root.TryGetProperty("issuer", out var issuerElement))
            {
                issuerUrl = issuerElement.GetString();
            }
            else
            {
                response.Warnings.Add("Badge class missing 'issuer' field");
            }

            // Fetch and validate issuer if URL is valid
            if (!string.IsNullOrEmpty(issuerUrl) && Uri.TryCreate(issuerUrl, UriKind.Absolute, out _))
            {
                await ValidateIssuerAsync(issuerUrl, response);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not fetch badge class");
            response.Warnings.Add("Could not verify badge class");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid badge class JSON");
            response.Warnings.Add("Invalid badge class JSON");
        }
    }

    private async Task ValidateIssuerAsync(string issuerUrl, VerifyResponse response)
    {
        try
        {
            _logger.LogInformation("Fetching issuer from: {IssuerUrl}", issuerUrl);
            using var httpClient = _httpClientFactory.CreateClient();
            var issuerJson = await httpClient.GetStringAsync(issuerUrl);
            
            using var doc = JsonDocument.Parse(issuerJson);
            var root = doc.RootElement;

            // Extract issuer name
            if (root.TryGetProperty("name", out var nameElement))
            {
                response.Details!.IssuerName = nameElement.GetString();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not fetch issuer");
            response.Warnings.Add("Could not verify issuer");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid issuer JSON");
            response.Warnings.Add("Invalid issuer JSON");
        }
    }
}
