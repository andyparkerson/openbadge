using Microsoft.Extensions.Logging;
using Moq;
using OpenBadge.Services;
using Xunit;

namespace OpenBadge.Tests;

/// <summary>
/// Tests for PngBadgeBaker service
/// Validates PNG chunk manipulation and badge baking/unbaking
/// </summary>
public class PngBadgeBakerTests
{
    private readonly PngBadgeBaker _baker;
    private readonly Mock<ILogger<PngBadgeBaker>> _mockLogger;

    public PngBadgeBakerTests()
    {
        _mockLogger = new Mock<ILogger<PngBadgeBaker>>();
        _baker = new PngBadgeBaker(_mockLogger.Object);
    }

    /// <summary>
    /// Creates a minimal valid PNG image for testing
    /// Contains PNG signature, IHDR, IDAT, and IEND chunks
    /// </summary>
    private byte[] CreateMinimalPng()
    {
        using var ms = new MemoryStream();
        
        // PNG signature
        ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
        
        // IHDR chunk (13 bytes data: width=1, height=1, bit depth=8, color type=2 RGB, compression=0, filter=0, interlace=0)
        WriteChunk(ms, "IHDR", new byte[] { 
            0, 0, 0, 1,  // width: 1
            0, 0, 0, 1,  // height: 1
            8,           // bit depth
            2,           // color type (RGB)
            0,           // compression method
            0,           // filter method
            0            // interlace method
        });
        
        // IDAT chunk (minimal compressed data for 1x1 RGB pixel)
        // This is a valid deflate stream for a single black pixel
        WriteChunk(ms, "IDAT", new byte[] { 
            0x78, 0x9c, 0x62, 0x00, 0x00, 0x00, 0x04, 0x00, 0x01 
        });
        
        // IEND chunk (no data)
        WriteChunk(ms, "IEND", Array.Empty<byte>());
        
        return ms.ToArray();
    }

    private void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        
        // Length
        var lengthBytes = new byte[4];
        lengthBytes[0] = (byte)((data.Length >> 24) & 0xFF);
        lengthBytes[1] = (byte)((data.Length >> 16) & 0xFF);
        lengthBytes[2] = (byte)((data.Length >> 8) & 0xFF);
        lengthBytes[3] = (byte)(data.Length & 0xFF);
        stream.Write(lengthBytes, 0, 4);
        
        // Type
        stream.Write(typeBytes, 0, 4);
        
        // Data
        if (data.Length > 0)
            stream.Write(data, 0, data.Length);
        
        // CRC (simplified - just write zeros for test PNG)
        var crc = CalculateCrc(typeBytes, data);
        var crcBytes = new byte[4];
        crcBytes[0] = (byte)((crc >> 24) & 0xFF);
        crcBytes[1] = (byte)((crc >> 16) & 0xFF);
        crcBytes[2] = (byte)((crc >> 8) & 0xFF);
        crcBytes[3] = (byte)(crc & 0xFF);
        stream.Write(crcBytes, 0, 4);
    }

    private uint CalculateCrc(byte[] type, byte[] data)
    {
        var combined = new byte[type.Length + data.Length];
        Array.Copy(type, 0, combined, 0, type.Length);
        Array.Copy(data, 0, combined, type.Length, data.Length);
        
        uint crc = 0xFFFFFFFF;
        foreach (byte b in combined)
        {
            var index = (crc ^ b) & 0xFF;
            crc = Crc32Table[index] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] Crc32Table = InitializeCrc32Table();

    private static uint[] InitializeCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
            {
                if ((c & 1) == 1)
                    c = 0xEDB88320 ^ (c >> 1);
                else
                    c >>= 1;
            }
            table[i] = c;
        }
        return table;
    }

    [Fact]
    public void Bake_ShouldEmbedAssertionInPng()
    {
        // Arrange
        var sourcePng = CreateMinimalPng();
        var assertionJson = "{\"type\":\"Assertion\",\"id\":\"test-assertion\"}";

        // Act
        var bakedPng = _baker.Bake(sourcePng, assertionJson);

        // Assert
        Assert.NotNull(bakedPng);
        Assert.True(bakedPng.Length > sourcePng.Length, "Baked PNG should be larger than source");
    }

    [Fact]
    public void Unbake_ShouldExtractAssertionFromBakedPng()
    {
        // Arrange
        var sourcePng = CreateMinimalPng();
        var assertionJson = "{\"type\":\"Assertion\",\"id\":\"test-assertion\"}";

        // Act
        var bakedPng = _baker.Bake(sourcePng, assertionJson);
        var extractedJson = _baker.Unbake(bakedPng);

        // Assert
        Assert.NotNull(extractedJson);
        Assert.Equal(assertionJson, extractedJson);
    }

    [Fact]
    public void BakeAndUnbake_RoundTrip_ShouldPreserveAssertion()
    {
        // Arrange
        var sourcePng = CreateMinimalPng();
        var assertionJson = @"{
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

        // Act
        var bakedPng = _baker.Bake(sourcePng, assertionJson);
        var extractedJson = _baker.Unbake(bakedPng);

        // Assert
        Assert.Equal(assertionJson, extractedJson);
    }

    [Fact]
    public void Unbake_WhenNoBadgeData_ShouldReturnNull()
    {
        // Arrange
        var sourcePng = CreateMinimalPng();

        // Act
        var extractedJson = _baker.Unbake(sourcePng);

        // Assert
        Assert.Null(extractedJson);
    }

    [Fact]
    public void Bake_WithInvalidPng_ShouldThrowException()
    {
        // Arrange
        var invalidPng = new byte[] { 1, 2, 3, 4, 5 };
        var assertionJson = "{\"test\":\"data\"}";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _baker.Bake(invalidPng, assertionJson));
    }

    [Fact]
    public void Bake_WithNullPng_ShouldThrowException()
    {
        // Arrange
        byte[]? nullPng = null;
        var assertionJson = "{\"test\":\"data\"}";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _baker.Bake(nullPng!, assertionJson));
    }

    [Fact]
    public void Bake_WithEmptyAssertion_ShouldThrowException()
    {
        // Arrange
        var sourcePng = CreateMinimalPng();
        var emptyAssertion = "";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _baker.Bake(sourcePng, emptyAssertion));
    }

    [Fact]
    public void Unbake_WithInvalidPng_ShouldThrowException()
    {
        // Arrange
        var invalidPng = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _baker.Unbake(invalidPng));
    }
}
