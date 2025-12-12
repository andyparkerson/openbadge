using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenBadge.Emitters;
using OpenBadge.Models;
using OpenBadge.Services;
using System.Text.Json;

namespace OpenBadge.Functions;

/// <summary>
/// Azure Function for baking Open Badges assertions into PNG images
/// </summary>
public class BakeFunction
{
    private readonly ILogger<BakeFunction> _logger;
    private readonly PngBadgeBaker _badgeBaker;
    private readonly PublishingService _publishingService;
    private readonly IStandardEmitter _emitter;

    public BakeFunction(
        ILogger<BakeFunction> logger,
        PngBadgeBaker badgeBaker,
        PublishingService publishingService,
        IStandardEmitter emitter)
    {
        _logger = logger;
        _badgeBaker = badgeBaker;
        _publishingService = publishingService;
        _emitter = emitter;
    }

    [Function("Bake")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "bake")] HttpRequest req)
    {
        try
        {
            _logger.LogInformation("Processing bake request");

            // Parse multipart form data
            if (!req.HasFormContentType)
            {
                return new BadRequestObjectResult(new { error = "Request must be multipart/form-data" });
            }

            var form = await req.ReadFormAsync();
            
            // Get PNG file
            var pngFile = form.Files["png"];
            if (pngFile == null || pngFile.Length == 0)
            {
                return new BadRequestObjectResult(new { error = "PNG file is required" });
            }

            // Get JSON payload
            var jsonString = form["json"].ToString();
            if (string.IsNullOrEmpty(jsonString))
            {
                return new BadRequestObjectResult(new { error = "JSON payload is required" });
            }

            // Deserialize bake request
            BakeRequest? bakeRequest;
            try
            {
                bakeRequest = JsonSerializer.Deserialize<BakeRequest>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                return new BadRequestObjectResult(new { error = "Invalid JSON payload", details = ex.Message });
            }

            if (bakeRequest == null)
            {
                return new BadRequestObjectResult(new { error = "Invalid bake request" });
            }

            // Validate standard
            if (bakeRequest.Standard.ToLowerInvariant() != "ob2")
            {
                return new BadRequestObjectResult(new 
                { 
                    error = "Only Open Badges 2.0 (standard='ob2') is currently supported",
                    message = "Open Badges 3.0 support is planned for future implementation"
                });
            }

            // Generate unique IDs
            var issuerId = Guid.NewGuid().ToString("N");
            var badgeClassId = Guid.NewGuid().ToString("N");

            // Publish issuer
            var issuerUrl = await _publishingService.PublishIssuerAsync(
                issuerId, 
                bakeRequest.Award.Issuer);

            // Publish badge class
            var badgeClassUrl = await _publishingService.PublishBadgeClassAsync(
                badgeClassId,
                bakeRequest.Award.BadgeClass,
                issuerUrl);

            // Generate assertion
            var (assertionId, assertionJson) = await _emitter.EmitAsync(
                bakeRequest,
                new Uri(issuerUrl),
                new Uri(badgeClassUrl));

            // Publish assertion
            var assertionUrl = await _publishingService.PublishAssertionAsync(
                assertionId,
                assertionJson);

            // Read PNG bytes
            byte[] pngBytes;
            using (var ms = new MemoryStream())
            {
                await pngFile.CopyToAsync(ms);
                pngBytes = ms.ToArray();
            }

            // Bake assertion into PNG
            var bakedPng = _badgeBaker.Bake(pngBytes, assertionJson);

            // Store baked PNG
            var bakedPngUrl = await _publishingService.StoreBakedPngAsync(assertionId, bakedPng);

            _logger.LogInformation("Successfully baked badge for assertion {AssertionId}", assertionId);

            // Return response
            var response = new BakeResponse
            {
                IssuerUrl = issuerUrl,
                BadgeClassUrl = badgeClassUrl,
                AssertionUrl = assertionUrl,
                BakedPngUrl = bakedPngUrl
            };

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bake request");
            return new ObjectResult(new { error = "Internal server error", details = ex.Message })
            {
                StatusCode = 500
            };
        }
    }
}
