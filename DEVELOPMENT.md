# Local Development Guide

This guide will help you set up and run the Open Badges API locally for development and testing.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)
- [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) (for local blob storage emulation)
- Optional: [Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/) (for viewing blob storage)

## Installation

### 1. Install Azure Functions Core Tools

**macOS (Homebrew):**
```bash
brew tap azure/functions
brew install azure-functions-core-tools@4
```

**Windows (Chocolatey):**
```powershell
choco install azure-functions-core-tools-4
```

**Linux (Ubuntu/Debian):**
```bash
wget -q https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install azure-functions-core-tools-4
```

### 2. Install Azurite

**NPM (all platforms):**
```bash
npm install -g azurite
```

**Homebrew (macOS):**
```bash
brew install azurite
```

## Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/andyparkerson/openbadge.git
   cd openbadge
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Configure local settings:**
   ```bash
   cp local.settings.json.template local.settings.json
   ```

4. **Edit `local.settings.json`** (if needed):
   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "BlobStorageConnectionString": "UseDevelopmentStorage=true",
       "PublicContainerName": "public",
       "BakedBadgesContainerName": "badges-baked",
       "BaseUrl": "http://localhost:7071"
     }
   }
   ```

## Running Locally

### Option 1: Using Terminal

1. **Start Azurite** (in a separate terminal):
   ```bash
   azurite --silent --location /tmp/azurite --debug /tmp/azurite/debug.log
   ```

2. **Start the Function App** (in another terminal):
   ```bash
   cd /path/to/openbadge
   func start
   ```

3. The API will be available at `http://localhost:7071`

### Option 2: Using VS Code

1. Open the project in VS Code
2. Install the "Azure Functions" extension
3. Press `F5` to start debugging (Azurite will start automatically if configured)

### Option 3: Using Visual Studio

1. Open `OpenBadge.sln` in Visual Studio
2. Press `F5` to start debugging
3. Make sure Azurite is running separately

## Testing the API

### Health Check

```bash
curl http://localhost:7071/api/health
```

### Bake a Badge

Using the provided sample files:

```bash
cd examples
chmod +x bake-badge.sh
./bake-badge.sh badge-template.png
```

Or manually with curl:

```bash
curl -X POST http://localhost:7071/api/bake \
  -F "png=@examples/badge-template.png" \
  -F "json=$(cat examples/sample-request.json)"
```

### Expected Response

```json
{
  "issuerUrl": "http://127.0.0.1:10000/devstoreaccount1/public/issuers/{id}.json",
  "badgeClassUrl": "http://127.0.0.1:10000/devstoreaccount1/public/badgeclasses/{id}.json",
  "assertionUrl": "http://127.0.0.1:10000/devstoreaccount1/public/assertions/{id}.json",
  "bakedPngUrl": "http://127.0.0.1:10000/devstoreaccount1/badges-baked/{id}.png?..."
}
```

### Retrieve Assertion

```bash
# Use the assertion ID from the bake response
curl http://localhost:7071/api/assertion/{id}
```

## Viewing Blob Storage

### Using Azure Storage Explorer

1. Open Azure Storage Explorer
2. Connect to "Local Storage Emulator"
3. Navigate to:
   - `public` container: View issued badges, badge classes, and assertions
   - `badges-baked` container: View baked PNG files

### Using Azurite Browser

Azurite also provides a web interface (if started with `--blobHost` and `--blobPort` flags):

```bash
azurite --blobHost 127.0.0.1 --blobPort 10000 --location /tmp/azurite
```

Then visit: http://127.0.0.1:10000/devstoreaccount1

## Running Tests

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run tests with coverage
dotnet test /p:CollectCoverage=true
```

## Common Issues

### Port Already in Use

If port 7071 is already in use:

1. Change the port in `local.settings.json`:
   ```json
   {
     "Host": {
       "LocalHttpPort": 7072
     }
   }
   ```

2. Or find and kill the process using port 7071:
   ```bash
   # macOS/Linux
   lsof -ti:7071 | xargs kill -9
   
   # Windows
   netstat -ano | findstr :7071
   taskkill /PID <PID> /F
   ```

### Azurite Connection Issues

Make sure Azurite is running and accessible:

```bash
# Test connection
curl http://127.0.0.1:10000/devstoreaccount1?comp=list
```

If not working:
1. Restart Azurite
2. Clear Azurite data: `rm -rf /tmp/azurite`
3. Check firewall settings

### Build Errors

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

## Development Workflow

1. **Make changes** to the code
2. **Run tests**: `dotnet test`
3. **Test locally**: `func start`
4. **Verify changes** with manual testing
5. **Commit** and push changes

## Debugging

### VS Code

1. Set breakpoints in your code
2. Press `F5` to start debugging
3. The debugger will attach to the running Function App

### Visual Studio

1. Set breakpoints in your code
2. Press `F5` to start debugging
3. Use the Immediate Window, Watch Windows, etc.

### Logging

The Function App uses `ILogger` for logging. View logs in:

- **Console**: When running with `func start`
- **Application Insights**: If configured in production
- **Debug Output**: When debugging in VS Code or Visual Studio

Enable verbose logging in `host.json`:

```json
{
  "logging": {
    "logLevel": {
      "default": "Debug"
    }
  }
}
```

## Next Steps

- Read the [README](../README.md) for full API documentation
- Review the [sample request](examples/sample-request.json) and [shell script](examples/bake-badge.sh)
- Explore the [frontend example](examples/frontend-example.ts)
- Check out the Open Badges specifications linked in the README
