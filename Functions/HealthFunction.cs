using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace OpenBadge.Functions;

/// <summary>
/// Health check endpoint
/// </summary>
public class HealthFunction
{
    private readonly ILogger<HealthFunction> _logger;

    public HealthFunction(ILogger<HealthFunction> logger)
    {
        _logger = logger;
    }

    [Function("Health")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        _logger.LogInformation("Health check");

        return new OkObjectResult(new
        {
            status = "healthy",
            service = "Open Badges API",
            version = "1.0.0",
            timestamp = DateTimeOffset.UtcNow
        });
    }
}
