# Open Badges 2.0 Azure Functions Backend

A .NET 8 Azure Functions backend for baking **Open Badges 2.0** assertions into PNG images. This service implements the Open Badges Baking Specification and is designed to support Open Badges 3.0 (Verifiable Credentials) in the future.

## 🎯 Features

- ✅ **Open Badges 2.0 Support**: Full implementation with hosted verification
- 🖼️ **PNG Baking**: Embeds assertion JSON into PNG iTXt chunks without external dependencies
- ☁️ **Azure Integration**: Uses Azure Blob Storage for public JSON files and private baked PNGs
- 🔐 **Privacy-First**: Hashes recipient emails with salt (SHA-256)
- 🚀 **Production-Ready**: Isolated worker process, dependency injection, comprehensive logging
- 🧪 **Well-Tested**: Unit tests for PNG baking/unbaking
- 📦 **Future-Proof**: Architecture ready for Open Badges 3.0 (VC-signed) implementation

## 📚 What is Badge Baking?

Badge baking embeds Open Badges assertion data directly into PNG images using the **iTXt chunk** with keyword `openbadges`. This allows the badge image itself to be portable and verifiable—anyone with the PNG can extract and verify the credential without needing access to the original issuer's server.

**Key Benefits**:
- Badge images are self-contained and portable
- Recipients can share badges without exposing their email addresses (hashed)
- Verification data travels with the image
- Compatible with the Open Badges ecosystem

## 🏗️ Architecture

### Components

- **DTOs**: Standard-agnostic data models (`IssuerDto`, `BadgeClassDto`, `RecipientDto`, `BakeRequest`)
- **Emitters**: 
  - `Ob20Emitter`: Generates OB 2.0 assertions with hashed recipients
  - `Ob30Emitter`: Stub for future OB 3.0 VC-signed implementation
- **PngBadgeBaker**: Pure C# PNG chunk reader/writer with iTXt embedding
- **PublishingService**: Manages Azure Blob Storage (public JSON, private PNGs)
- **Azure Functions**: HTTP endpoints for baking, verification, and serving artifacts

### API Endpoints

| Method | Endpoint | Description | Auth Level |
|--------|----------|-------------|------------|
| POST | `/api/bake` | Bake assertion into PNG | Function Key |
| GET | `/api/issuer/{id}` | Get issuer JSON | Anonymous |
| GET | `/api/badgeclass/{id}` | Get badge class JSON | Anonymous |
| GET | `/api/assertion/{id}` | Get assertion JSON | Anonymous |
| POST | `/api/verify` | Verify badge (stub) | Function Key |
| GET | `/api/health` | Health check | Anonymous |

## 🚀 Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local) (v4)
- [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) (for local blob storage emulation)

### Local Development

1. **Clone the repository**:
   ```bash
   git clone https://github.com/andyparkerson/openbadge.git
   cd openbadge
   ```

2. **Install dependencies**:
   ```bash
   dotnet restore
   ```

3. **Configure local settings**:
   ```bash
   cp local.settings.json.template local.settings.json
   ```
   
   Edit `local.settings.json` to configure:
   - `BlobStorageConnectionString`: Azure Storage connection string (or `UseDevelopmentStorage=true` for Azurite)
   - `BaseUrl`: Your issuer's base URL (e.g., `https://issuer.example.org`)

4. **Start Azurite** (in a separate terminal):
   ```bash
   azurite --silent --location /tmp/azurite --debug /tmp/azurite/debug.log
   ```

5. **Run the Function App**:
   ```bash
   func start
   ```

   The API will be available at `http://localhost:7071`

### Running Tests

```bash
cd Tests
dotnet test
```

## 📝 Usage Examples

### 1. Bake a Badge into a PNG

**Request**:
```bash
curl -X POST http://localhost:7071/api/bake \
  -H "Content-Type: multipart/form-data" \
  -F "png=@badge-template.png" \
  -F 'json={
    "standard": "ob2",
    "award": {
      "issuer": {
        "id": "my-org",
        "name": "Example Organization",
        "url": "https://example.org",
        "email": "contact@example.org"
      },
      "badgeClass": {
        "id": "awesome-badge",
        "name": "Awesome Achievement",
        "description": "Awarded for completing the awesome course",
        "image": "https://example.org/images/awesome-badge.png",
        "issuer": "my-org",
        "criteria": ["Completed the course", "Passed the final exam"],
        "tags": ["achievement", "learning"]
      },
      "recipient": {
        "type": "email",
        "identity": "learner@example.com",
        "hashed": false
      },
      "issuedOn": "2024-01-15T00:00:00Z",
      "evidence": "https://example.org/evidence/123"
    }
  }'
```

**Response**:
```json
{
  "issuerUrl": "https://yourblob.blob.core.windows.net/public/issuers/{id}.json",
  "badgeClassUrl": "https://yourblob.blob.core.windows.net/public/badgeclasses/{id}.json",
  "assertionUrl": "https://yourblob.blob.core.windows.net/public/assertions/{id}.json",
  "bakedPngUrl": "https://yourblob.blob.core.windows.net/badges-baked/{id}.png?sv=..."
}
```

### 2. Retrieve Assertion JSON

```bash
curl http://localhost:7071/api/assertion/{id}
```

**Response** (OB 2.0 format):
```json
{
  "@context": "https://w3id.org/openbadges/v2",
  "type": "Assertion",
  "id": "https://issuer.example.org/api/assertion/{id}",
  "recipient": {
    "type": "email",
    "identity": "sha256$...",
    "hashed": true,
    "salt": "..."
  },
  "badge": "https://issuer.example.org/api/badgeclass/{id}",
  "verification": {
    "type": "HostedBadge"
  },
  "issuedOn": "2024-01-15T00:00:00Z"
}
```

### 3. Verify a Badge

You can verify a badge either by pointing the verifier at a published assertion URL, or by uploading a baked PNG. The verification endpoint is `POST /api/verify` and requires a function key when running with function-level auth.

- Verify by assertion URL (JSON body):

```bash
curl -X POST http://localhost:7071/api/verify \
  -H "Content-Type: application/json" \
  -H "x-functions-key: <your-function-key>" \
  -d '{"assertionUrl":"http://localhost:7071/api/assertion/{id}"}'
```

- Verify by baked PNG (multipart/form-data):

```bash
curl -X POST http://localhost:7071/api/verify \
  -H "x-functions-key: <your-function-key>" \
  -F "png=@path/to/baked-badge.png"
```

Response (current implementation):

The `Verify` function is currently a stub. It responds with a JSON object similar to:

```json
{
  "message": "Verification endpoint is planned but not yet implemented",
  "status": "stub",
  "note": "Future implementation will validate Open Badges 2.0 and 3.0 assertions",
  "reference": "https://github.com/1EdTech/openbadges-validator-core"
}
```

When implemented, the verifier will accept either an assertion URL or a baked PNG, extract or fetch the assertion, validate its structure and evidence, and (for OB 3.0) verify cryptographic proofs.

### 4. Health Check

```bash
curl http://localhost:7071/api/health
```

## 🎨 Frontend Integration Example

```typescript
async function bakeBadge(pngFile: File, awardData: any) {
  const formData = new FormData();
  formData.append('png', pngFile);
  formData.append('json', JSON.stringify({
    standard: 'ob2',
    award: awardData
  }));

  const response = await fetch('https://your-function-app.azurewebsites.net/api/bake', {
    method: 'POST',
    body: formData,
    headers: {
      'x-functions-key': 'your-function-key'
    }
  });

  const result = await response.json();
  console.log('Baked badge URLs:', result);
  
  return result;
}
```

## 🔧 Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `BlobStorageConnectionString` | Azure Storage connection string | `UseDevelopmentStorage=true` |
| `PublicContainerName` | Container for public JSON files | `public` |
| `BakedBadgesContainerName` | Container for baked PNGs | `badges-baked` |
| `BaseUrl` | Issuer's base URL for stable assertion URLs | `https://issuer.example.org` |

### Azure Deployment

1. **Create Azure Resources**:
   ```bash
   # Create resource group
   az group create --name openbadge-rg --location eastus
   
   # Create storage account
   az storage account create \
     --name openbadgestorage \
     --resource-group openbadge-rg \
     --location eastus \
     --sku Standard_LRS
   
   # Create function app
   az functionapp create \
     --name openbadge-functions \
     --resource-group openbadge-rg \
     --storage-account openbadgestorage \
     --runtime dotnet-isolated \
     --runtime-version 8 \
     --functions-version 4 \
     --os-type Linux
   ```

2. **Configure Application Settings**:
   ```bash
   az functionapp config appsettings set \
     --name openbadge-functions \
     --resource-group openbadge-rg \
     --settings \
       BlobStorageConnectionString="<connection-string>" \
       BaseUrl="https://openbadge-functions.azurewebsites.net"
   ```

3. **Deploy**:
   ```bash
   func azure functionapp publish openbadge-functions
   ```

## 🧪 Testing

The project includes comprehensive unit tests for the PNG baking service:

- `Bake_ShouldEmbedAssertionInPng`: Validates assertion embedding
- `Unbake_ShouldExtractAssertionFromBakedPng`: Validates assertion extraction
- `BakeAndUnbake_RoundTrip_ShouldPreserveAssertion`: End-to-end round-trip test
- Error handling tests for invalid inputs

Run tests with coverage:
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## 🔐 Security

- **Authentication**: Function endpoints use Azure Functions key-based authentication
- **Managed Identity**: Use Azure Managed Identity for blob storage access in production
- **Recipient Privacy**: Email addresses are hashed with unique salts (SHA-256)
- **HTTPS Only**: All production endpoints should use HTTPS
- **Validation**: Input validation and sanitization on all endpoints

### Recommended Security Practices

1. Use **Azure Static Web Apps** authentication for frontend
2. Enable **Managed Identity** for Function → Blob Storage access
3. Configure **CORS** appropriately for your frontend domain
4. Rotate function keys regularly
5. Use **Azure Key Vault** for sensitive configuration

## 📖 Open Badges Specifications

This implementation follows these specifications:

- **[Open Badges Baking Specification](https://www.imsglobal.org/sites/default/files/Badges/OBv2p0Final/baking/index.html)**: PNG iTXt chunk embedding
- **[Open Badges 2.0 Specification](https://www.imsglobal.org/sites/default/files/Badges/OBv2p0Final/index.html)**: Assertion, BadgeClass, and Issuer structures
- **[Badge Baking Background](https://github.com/mozilla/openbadges-backpack/wiki/Badge-Baking)**: Historical context and implementation notes

### Future: Open Badges 3.0

Open Badges 3.0 introduces Verifiable Credentials (VCs) with cryptographic signatures. The architecture is ready for this implementation:

- **[Open Badges 3.0 Standard](https://www.1edtech.org/standards/open-badges)**
- **[OB 3.0 Implementation Guide](https://www.imsglobal.org/spec/ob/v3p0/impl/)** (Final release: April 7, 2025)

Planned enhancements:
- `Ob30Emitter` implementation with VC signing
- JSON-LD signature support
- Integration with decentralized identifier (DID) systems

## 🛠️ Development

### Project Structure

```
openbadge/
├── Models/
│   └── DTOs.cs                 # Data transfer objects
├── Emitters/
│   ├── IStandardEmitter.cs     # Emitter interface
│   ├── Ob20Emitter.cs          # OB 2.0 implementation
│   └── Ob30Emitter.cs          # OB 3.0 stub
├── Services/
│   ├── PngBadgeBaker.cs        # PNG chunk manipulation
│   └── PublishingService.cs    # Azure Blob Storage
├── Functions/
│   ├── BakeFunction.cs         # POST /api/bake
│   ├── StaticJsonFunctions.cs  # GET endpoints for JSON
│   ├── VerifyFunction.cs       # POST /api/verify (stub)
│   └── HealthFunction.cs       # GET /api/health
├── Tests/
│   ├── OpenBadge.Tests.csproj
│   └── PngBadgeBakerTests.cs
├── Program.cs                  # DI configuration
├── OpenBadge.csproj
├── host.json
└── local.settings.json.template
```

### Adding New Features

1. Create feature branch: `git checkout -b feature/my-feature`
2. Implement changes with tests
3. Run tests: `dotnet test`
4. Update documentation
5. Submit pull request

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Update documentation
6. Submit a pull request

## 📄 License

See [LICENSE](LICENSE) file for details.

## 🔗 Resources

- **Validation**: [Open Badges Validator](https://github.com/1EdTech/openbadges-validator-core)
- **Community**: [Open Badges Community](https://openbadges.org/)
- **Azure Functions**: [Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/)
- **Verification Tool**: [IACET Badge Verification](https://www.iacet.org/resources/iacet-openbadge-verification-tool/)

## 🆘 Support

For issues, questions, or contributions, please:
- Open an [issue](https://github.com/andyparkerson/openbadge/issues)
- Submit a [pull request](https://github.com/andyparkerson/openbadge/pulls)
- Review the [Open Badges documentation](https://openbadges.org/build)