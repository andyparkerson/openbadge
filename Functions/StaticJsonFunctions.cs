using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenBadge.Services;

namespace OpenBadge.Functions;

/// <summary>
/// Azure Functions for serving static JSON files (issuer, badge class, assertion)
/// </summary>
public class StaticJsonFunctions
{
    private readonly ILogger<StaticJsonFunctions> _logger;
    private readonly PublishingService _publishingService;

    public StaticJsonFunctions(
        ILogger<StaticJsonFunctions> logger,
        PublishingService publishingService)
    {
        _logger = logger;
        _publishingService = publishingService;
    }

    [Function("GetIssuer")]
    public async Task<IActionResult> GetIssuer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "issuer/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Retrieving issuer {IssuerId}", id);

        var json = await _publishingService.GetJsonAsync("issuer", id);
        
        if (json == null)
        {
            return new NotFoundObjectResult(new { error = "Issuer not found" });
        }

        return new ContentResult
        {
            Content = json,
            ContentType = "application/json",
            StatusCode = 200
        };
    }

    [Function("GetBadgeClass")]
    public async Task<IActionResult> GetBadgeClass(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "badgeclass/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Retrieving badge class {BadgeClassId}", id);

        var json = await _publishingService.GetJsonAsync("badgeclass", id);
        
        if (json == null)
        {
            return new NotFoundObjectResult(new { error = "Badge class not found" });
        }

        return new ContentResult
        {
            Content = json,
            ContentType = "application/json",
            StatusCode = 200
        };
    }

    [Function("GetAssertion")]
    public async Task<IActionResult> GetAssertion(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "assertion/{id}")] HttpRequest req,
        string id)
    {
        _logger.LogInformation("Retrieving assertion {AssertionId}", id);

        var json = await _publishingService.GetJsonAsync("assertion", id);
        
        if (json == null)
        {
            return new NotFoundObjectResult(new { error = "Assertion not found" });
        }

        return new ContentResult
        {
            Content = json,
            ContentType = "application/json",
            StatusCode = 200
        };
    }
}
