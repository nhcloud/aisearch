# Build and Start Scripts

This directory contains various build, start, and utility scripts for the AI Search Solution (.NET 9).

## Available Scripts

### Main Scripts (Root Directory)

- **`build.bat`** - Main build script that builds the entire solution
- **`start.bat`** - Starts both API and Web applications in separate windows
- **`start.sh`** - Linux/macOS version of the start script

### Individual Project Scripts

#### Build Scripts
- **`build-core.bat`** - Builds only the AISearch.Core library
- **`build-api.bat`** - Builds only the AISearch.Api backend
- **`build-web.bat`** - Builds only the AISearch.Web frontend

#### Start Scripts
- **`start-api.bat`** - Starts only the AISearch.Api backend
- **`start-api.sh`** - Linux/macOS version of API start script
- **`start-web.bat`** - Starts only the AISearch.Web frontend
- **`start-web.sh`** - Linux/macOS version of Web start script

#### Utility Scripts
- **`clean.bat`** - Cleans the solution (removes bin/obj directories)
- **`publish.bat`** - Publishes both applications for production deployment

## Usage

### Development Workflow

1. **Initial Setup:**
   ```bat
   # From repository root
   build.bat
   ```

2. **Start Both Services:**
   ```bat
   start.bat
   ```

3. **Start Individual Services:**
   ```bat
   # Start API only
   scripts\start-api.bat
   
   # Start Web only (requires API to be running)
   scripts\start-web.bat
   ```

4. **Individual Project Development:**
   ```bat
   # Build specific project
   scripts\build-api.bat
   scripts\build-web.bat
   scripts\build-core.bat
   ```

### Linux/macOS Usage

1. **Make scripts executable:**
   ```bash
   chmod +x start.sh scripts/*.sh
   ```

2. **Start services:**
   ```bash
   # Start both services
   ./start.sh
   
   # Start individual services
   ./scripts/start-api.sh
   ./scripts/start-web.sh
   ```

### Production Deployment

1. **Clean Build:**
   ```bat
   scripts\clean.bat
   build.bat
   ```

2. **Publish for Production:**
   ```bat
   scripts\publish.bat
   ```

## Service URLs

When running the applications, they will be available at:

### Development URLs (HTTPS - Recommended)
- **Web Frontend:** https://localhost:7002
- **API Backend:** https://localhost:7001
- **Swagger UI:** https://localhost:7001/swagger

### Alternative HTTP URLs (if HTTPS issues)
- **Web Frontend:** http://localhost:5002
- **API Backend:** http://localhost:5000

## Project Structure

```
AISearch/
??? dotnet/
?   ??? AISearch.Core/      # Core business logic library (.NET 9)
?   ??? AISearch.Api/       # Web API backend (.NET 9)
?   ??? AISearch.Web/       # Razor Pages frontend (.NET 9)
??? scripts/                # Build and utility scripts
?   ??? build-core.bat
?   ??? build-api.bat
?   ??? build-web.bat
?   ??? start-api.bat
?   ??? start-api.sh
?   ??? start-web.bat
?   ??? start-web.sh
?   ??? clean.bat
?   ??? publish.bat
??? build.bat              # Main build script
??? start.bat              # Main start script (Windows)
??? start.sh               # Main start script (Linux/macOS)
```

## Configuration Requirements

### Before Running the Applications

Both applications require proper configuration:

#### AISearch.Api Configuration
Update `dotnet/AISearch.Api/appsettings.json`:
- Azure AI Search service endpoint and key
- Azure OpenAI service endpoint and key
- Azure Storage account configuration
- Azure Document Intelligence configuration
- Azure AD authentication settings

#### AISearch.Web Configuration
Update `dotnet/AISearch.Web/appsettings.json`:
- API backend URL (https://localhost:7001)
- Azure AD authentication settings
- Authentication mode (Simple/Enhanced)

See the main README.MD and SETUP.md for detailed configuration instructions.

## Prerequisites

- .NET 9 SDK installed
- Visual Studio 2022 or VS Code (optional)
- Azure services configured (see SETUP.md)

## Troubleshooting

### Common Issues

1. **Build Failures:**
   - Run `scripts\clean.bat` first
   - Ensure .NET 9 SDK is installed
   - Check for missing Azure configuration

2. **Package Restore Issues:**
   - Clear NuGet cache: `dotnet nuget locals all --clear`
   - Restart Visual Studio if using IDE

3. **Startup Issues:**
   - Verify Azure service endpoints are correct
   - Check application logs for detailed errors
   - Ensure both API and Web apps use compatible settings
   - Make sure ports 7001, 7002, 5000, 5002 are available

4. **Authentication Issues:**
   - Verify Azure AD app registration settings
   - Check redirect URIs in Azure AD
   - Ensure client secrets are not expired

### Service Dependencies

- **Web Frontend** depends on **API Backend** being running
- Always start the API Backend first, then the Web Frontend
- Use the main `start.bat` script to start both services automatically

### Getting Help

- Check the main README.MD for comprehensive setup instructions
- Review SETUP.md for Azure service configuration
- Check application logs in the console windows
- Use Swagger UI (https://localhost:7001/swagger) to test API endpoints