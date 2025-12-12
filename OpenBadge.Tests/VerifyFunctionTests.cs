using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using OpenBadge.Functions;
using OpenBadge.Services;
using System.Net;
using System.Text;
using Xunit;

namespace OpenBadge.Tests;

/// <summary>
/// Tests for VerifyFunction
/// Validates badge verification logic for baked PNGs and assertion URLs
/// </summary>
public class VerifyFunctionTests
{
    private readonly Mock<ILogger<VerifyFunction>> _mockLogger;
    private readonly PngBadgeBaker _badgeBaker;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public VerifyFunctionTests()
    {
        _mockLogger = new Mock<ILogger<VerifyFunction>>();
        
        // Use actual PngBadgeBaker instance (not mock)
        var mockBakerLogger = new Mock<ILogger<PngBadgeBaker>>();
        _badgeBaker = new PngBadgeBaker(mockBakerLogger.Object);
        
        // Mock HttpClient factory
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var httpClient = new HttpClient();
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    /// <summary>
    /// Creates a valid OB 2.0 assertion JSON for testing
    /// </summary>
    private string CreateValidAssertionJson()
    {
        return @"{
            ""@context"": ""https://w3id.org/openbadges/v2"",
            ""type"": ""Assertion"",
            ""id"": ""https://example.org/assertions/123"",
            ""recipient"": {
                ""type"": ""email"",
                ""identity"": ""sha256$abc123"",
                ""hashed"": true
            },
            ""badge"": ""https://example.org/badges/awesome-badge"",
            ""verification"": {
                ""type"": ""HostedBadge""
            },
            ""issuedOn"": ""2024-01-15T00:00:00Z""
        }";
    }

    [Fact]
    public async Task Verify_WithValidAssertionJson_ShouldReturnValid()
    {
        // Arrange
        var function = new VerifyFunction(_mockLogger.Object, _badgeBaker, _mockHttpClientFactory.Object);
        var context = new DefaultHttpContext();
        var assertionJson = CreateValidAssertionJson();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(assertionJson));
        context.Request.ContentType = "application/json";

        // Act
        var result = await function.Run(context.Request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        var response = okResult.Value;
        var validProperty = response.GetType().GetProperty("Valid");
        Assert.NotNull(validProperty);
        var isValid = (bool?)validProperty.GetValue(response);
        Assert.True(isValid);
    }

    [Fact]
    public async Task Verify_WithMissingRequiredFields_ShouldReturnInvalid()
    {
        // Arrange
        var function = new VerifyFunction(_mockLogger.Object, _badgeBaker, _mockHttpClientFactory.Object);
        var context = new DefaultHttpContext();
        // Provide a valid JSON structure with type=Assertion but missing other required fields
        var invalidJson = @"{
            ""type"": ""Assertion""
        }";
        var bodyBytes = Encoding.UTF8.GetBytes(invalidJson);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;

        // Act
        var result = await function.Run(context.Request);

        // Assert - can be either BadRequest (if body read failed) or Ok with invalid response
        if (result is BadRequestObjectResult badRequestResult)
        {
            // Body was treated as empty
            Assert.NotNull(badRequestResult.Value);
        }
        else
        {
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            
            var response = okResult.Value;
            var validProperty = response.GetType().GetProperty("Valid");
            Assert.NotNull(validProperty);
            var isValid = (bool?)validProperty.GetValue(response);
            Assert.False(isValid);
        }
    }

    [Fact]
    public async Task Verify_WithExpiredBadge_ShouldReturnInvalid()
    {
        // Arrange
        var function = new VerifyFunction(_mockLogger.Object, _badgeBaker, _mockHttpClientFactory.Object);
        var context = new DefaultHttpContext();
        var expiredJson = @"{
            ""type"": ""Assertion"",
            ""id"": ""https://example.org/assertions/123"",
            ""recipient"": {
                ""type"": ""email"",
                ""identity"": ""test@example.com""
            },
            ""badge"": ""https://example.org/badges/test"",
            ""issuedOn"": ""2020-01-01T00:00:00Z"",
            ""expires"": ""2020-12-31T00:00:00Z""
        }";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(expiredJson));
        context.Request.ContentType = "application/json";

        // Act
        var result = await function.Run(context.Request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        var response = okResult.Value;
        var validProperty = response.GetType().GetProperty("Valid");
        Assert.NotNull(validProperty);
        var isValid = (bool?)validProperty.GetValue(response);
        Assert.False(isValid);
    }

    [Fact]
    public async Task Verify_WithNoInput_ShouldReturnBadRequest()
    {
        // Arrange
        var function = new VerifyFunction(_mockLogger.Object, _badgeBaker, _mockHttpClientFactory.Object);
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream();

        // Act
        var result = await function.Run(context.Request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Verify_WithInvalidJson_ShouldReturnError()
    {
        // Arrange
        var function = new VerifyFunction(_mockLogger.Object, _badgeBaker, _mockHttpClientFactory.Object);
        var context = new DefaultHttpContext();
        var invalidJson = "{this is not valid json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(invalidJson));
        context.Request.ContentType = "application/json";

        // Act
        var result = await function.Run(context.Request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        var response = okResult.Value;
        var validProperty = response.GetType().GetProperty("Valid");
        Assert.NotNull(validProperty);
        var isValid = (bool?)validProperty.GetValue(response);
        Assert.False(isValid);
    }
}
