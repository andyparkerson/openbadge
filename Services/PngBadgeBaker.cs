using System.Text;
using Microsoft.Extensions.Logging;

namespace OpenBadge.Services;

/// <summary>
/// Service for baking Open Badges assertions into PNG images using iTXt chunks
/// Implements the Open Badges Baking Specification
/// Reference: https://www.imsglobal.org/sites/default/files/Badges/OBv2p0Final/baking/index.html
/// Reference: https://github.com/mozilla/openbadges-backpack/wiki/Badge-Baking
/// </summary>
public class PngBadgeBaker
{
    private readonly ILogger<PngBadgeBaker> _logger;
    
    // PNG signature bytes
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    
    // Chunk type identifiers
    private static readonly byte[] IhdrType = Encoding.ASCII.GetBytes("IHDR");
    private static readonly byte[] ItxtType = Encoding.ASCII.GetBytes("iTXt");
    private static readonly byte[] IendType = Encoding.ASCII.GetBytes("IEND");
    
    // Open Badges iTXt keyword
    private const string OpenBadgesKeyword = "openbadges";

    public PngBadgeBaker(ILogger<PngBadgeBaker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Bakes an assertion JSON into a PNG image
    /// </summary>
    /// <param name="sourcePng">The source PNG image bytes</param>
    /// <param name="assertionJson">The assertion JSON to bake into the image</param>
    /// <returns>The baked PNG image bytes with embedded assertion</returns>
    public byte[] Bake(byte[] sourcePng, string assertionJson)
    {
        if (sourcePng == null || sourcePng.Length < 8)
            throw new ArgumentException("Invalid PNG data", nameof(sourcePng));

        if (string.IsNullOrEmpty(assertionJson))
            throw new ArgumentException("Assertion JSON cannot be empty", nameof(assertionJson));

        // Validate PNG signature
        if (!ValidatePngSignature(sourcePng))
            throw new InvalidOperationException("Invalid PNG signature");

        using var outputStream = new MemoryStream();
        using var inputStream = new MemoryStream(sourcePng);
        
        // Write PNG signature
        outputStream.Write(PngSignature, 0, PngSignature.Length);
        inputStream.Seek(PngSignature.Length, SeekOrigin.Begin);

        // Read and write IHDR chunk
        var ihdrChunk = ReadChunk(inputStream);
        if (!IsChunkType(ihdrChunk.type, IhdrType))
            throw new InvalidOperationException("IHDR chunk not found after PNG signature");
        
        WriteChunk(outputStream, ihdrChunk.type, ihdrChunk.data);

        // Create and write iTXt chunk with assertion
        var itxtData = CreateItxtChunkData(OpenBadgesKeyword, assertionJson);
        WriteChunk(outputStream, ItxtType, itxtData);

        // Copy remaining chunks
        while (inputStream.Position < inputStream.Length)
        {
            var chunk = ReadChunk(inputStream);
            WriteChunk(outputStream, chunk.type, chunk.data);
        }

        _logger.LogInformation("Baked assertion into PNG ({Size} bytes)", outputStream.Length);
        
        return outputStream.ToArray();
    }

    /// <summary>
    /// Unbakes (extracts) an assertion JSON from a baked PNG image
    /// </summary>
    /// <param name="bakedPng">The baked PNG image bytes</param>
    /// <returns>The extracted assertion JSON, or null if not found</returns>
    public string? Unbake(byte[] bakedPng)
    {
        if (bakedPng == null || bakedPng.Length < 8)
            throw new ArgumentException("Invalid PNG data", nameof(bakedPng));

        if (!ValidatePngSignature(bakedPng))
            throw new InvalidOperationException("Invalid PNG signature");

        using var inputStream = new MemoryStream(bakedPng);
        inputStream.Seek(PngSignature.Length, SeekOrigin.Begin);

        // Read chunks looking for iTXt with openbadges keyword
        while (inputStream.Position < inputStream.Length)
        {
            var chunk = ReadChunk(inputStream);
            
            if (IsChunkType(chunk.type, ItxtType))
            {
                var (keyword, text) = ParseItxtChunk(chunk.data);
                if (keyword == OpenBadgesKeyword)
                {
                    _logger.LogInformation("Found openbadges iTXt chunk with {Length} bytes", text?.Length ?? 0);
                    return text;
                }
            }
            
            // Stop at IEND
            if (IsChunkType(chunk.type, IendType))
                break;
        }

        _logger.LogWarning("No openbadges iTXt chunk found in PNG");
        return null;
    }

    private bool ValidatePngSignature(byte[] data)
    {
        if (data.Length < PngSignature.Length)
            return false;

        for (int i = 0; i < PngSignature.Length; i++)
        {
            if (data[i] != PngSignature[i])
                return false;
        }

        return true;
    }

    private (byte[] type, byte[] data) ReadChunk(Stream stream)
    {
        // Read chunk length (4 bytes, big-endian)
        var lengthBytes = new byte[4];
        stream.Read(lengthBytes, 0, 4);
        var length = ReadBigEndianInt32(lengthBytes);

        // Read chunk type (4 bytes)
        var type = new byte[4];
        stream.Read(type, 0, 4);

        // Read chunk data
        var data = new byte[length];
        if (length > 0)
            stream.Read(data, 0, length);

        // Read CRC (4 bytes) - we don't validate it during read
        var crc = new byte[4];
        stream.Read(crc, 0, 4);

        return (type, data);
    }

    private void WriteChunk(Stream stream, byte[] type, byte[] data)
    {
        // Write length (big-endian)
        var lengthBytes = WriteBigEndianInt32(data.Length);
        stream.Write(lengthBytes, 0, 4);

        // Write type
        stream.Write(type, 0, 4);

        // Write data
        if (data.Length > 0)
            stream.Write(data, 0, data.Length);

        // Calculate and write CRC
        var crc = CalculateCrc(type, data);
        var crcBytes = WriteBigEndianInt32((int)crc);
        stream.Write(crcBytes, 0, 4);
    }

    private byte[] CreateItxtChunkData(string keyword, string text)
    {
        using var ms = new MemoryStream();
        
        // Keyword (null-terminated)
        var keywordBytes = Encoding.Latin1.GetBytes(keyword);
        ms.Write(keywordBytes, 0, keywordBytes.Length);
        ms.WriteByte(0); // null terminator

        // Compression flag (0 = uncompressed)
        ms.WriteByte(0);

        // Compression method (0 when uncompressed)
        ms.WriteByte(0);

        // Language tag (null-terminated, empty for default)
        ms.WriteByte(0);

        // Translated keyword (null-terminated, empty)
        ms.WriteByte(0);

        // Text (UTF-8, not null-terminated)
        var textBytes = Encoding.UTF8.GetBytes(text);
        ms.Write(textBytes, 0, textBytes.Length);

        return ms.ToArray();
    }

    private (string keyword, string? text) ParseItxtChunk(byte[] data)
    {
        var pos = 0;

        // Read keyword (null-terminated Latin1)
        var keywordEnd = Array.IndexOf(data, (byte)0, pos);
        if (keywordEnd == -1) return (string.Empty, null);
        
        var keyword = Encoding.Latin1.GetString(data, pos, keywordEnd - pos);
        pos = keywordEnd + 1;

        // Skip compression flag and method
        if (pos + 2 > data.Length) return (keyword, null);
        pos += 2;

        // Skip language tag (null-terminated)
        var langEnd = Array.IndexOf(data, (byte)0, pos);
        if (langEnd == -1) return (keyword, null);
        pos = langEnd + 1;

        // Skip translated keyword (null-terminated)
        var transEnd = Array.IndexOf(data, (byte)0, pos);
        if (transEnd == -1) return (keyword, null);
        pos = transEnd + 1;

        // Read text (UTF-8, rest of data)
        if (pos >= data.Length) return (keyword, string.Empty);
        
        var text = Encoding.UTF8.GetString(data, pos, data.Length - pos);
        return (keyword, text);
    }

    private bool IsChunkType(byte[] actual, byte[] expected)
    {
        if (actual.Length != expected.Length)
            return false;

        for (int i = 0; i < actual.Length; i++)
        {
            if (actual[i] != expected[i])
                return false;
        }

        return true;
    }

    private int ReadBigEndianInt32(byte[] bytes)
    {
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    private byte[] WriteBigEndianInt32(int value)
    {
        return new byte[]
        {
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF)
        };
    }

    private uint CalculateCrc(byte[] type, byte[] data)
    {
        // CRC is calculated over type + data
        var combined = new byte[type.Length + data.Length];
        Array.Copy(type, 0, combined, 0, type.Length);
        Array.Copy(data, 0, combined, type.Length, data.Length);

        return Crc32(combined);
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

    private uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        
        foreach (byte b in data)
        {
            var index = (crc ^ b) & 0xFF;
            crc = Crc32Table[index] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }
}
