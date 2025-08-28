# AI Search - ASP.NET Core Razor Pages Frontend

This is the ASP.NET Core 9 Razor Pages frontend application for the AI Search Multimodal Solution, converted from the original React/TypeScript frontend while maintaining UI consistency.

> **📝 Attribution:** This .NET implementation is inspired by and based on the [Azure AI Search Multimodal Sample](https://github.com/Azure-Samples/azure-ai-search-multimodal-sample) Python repository. The original sample demonstrates multimodal search capabilities with Python/FastAPI and Streamlit. This .NET version adapts those concepts to provide an enterprise-ready Razor Pages frontend with Azure AD authentication.

## Architecture Overview

<!-- TODO: Add Frontend Architecture Diagram Here -->
![Frontend Architecture](../docs/images/frontend-architecture-diagram.png)
*Razor Pages frontend architecture showing authentication flow, API integration, and user interface components*

### **Frontend Components:**
- **Authentication Layer**: Azure AD integration with token management
- **UI Layer**: Bootstrap 5 + Material Design components
- **API Integration**: HttpClient with authentication headers
- **Client-Side Logic**: jQuery for dynamic interactions
- **Security**: CSRF protection, XSS prevention, input validation

## Features

- **Search Interface**: Semantic search with configurable parameters
- **Chat Interface**: Interactive chat with document grounding
- **Document Management**: Upload, view, and manage documents
- **Index Management**: Create and manage search indexes
- **Material Design UI**: Consistent with the original React application
- **Authentication**: Azure AD integration support
- **Responsive Design**: Works on desktop and mobile devices

## Technology Stack

- **ASP.NET Core 9**: Web framework with Razor Pages
- **C#**: Backend language
- **Bootstrap 5**: CSS framework
- **Material Icons**: Icon set
- **jQuery**: JavaScript library
- **Azure AD**: Authentication provider

## Getting Started

### Prerequisites

- .NET 9 SDK
- Visual Studio 2022 or VS Code
- Access to the AI Search API backend

### Installation

1. Clone the repository
2. Navigate to the AISearch.Web directory:
   ```bash
   cd dotnet/AISearch.Web
   ```

3. Restore dependencies:
   ```bash
   dotnet restore
   ```

4. Update the `appsettings.json` file with your API endpoint and Azure AD configuration:
   ```json
   {
     "ApiSettings": {
       "BaseUrl": "https://localhost:7001"
     },
     "AzureAd": {
       "Instance": "https://login.microsoftonline.com/",
       "Domain": "your-domain.onmicrosoft.com",
       "TenantId": "your-tenant-id",
       "ClientId": "your-client-id",
       "CallbackPath": "/signin-oidc"
     }
   }
   ```

5. Run the application:
   ```bash
   dotnet run
   ```

6. Open your browser and navigate to `https://localhost:7002`

## Project Structure

```
AISearch.Web/
├── Controllers/           # MVC Controllers
│   ├── HomeController.cs
│   ├── SearchController.cs
│   ├── ChatController.cs
│   ├── DocumentsController.cs
│   └── IndexManagementController.cs
├── Models/               # View Models and DTOs
│   └── ViewModels.cs
├── Views/                # Razor Views
│   ├── Home/
│   └── Shared/
├── wwwroot/              # Static files
│   ├── css/
│   ├── js/
│   └── favicon.ico
├── Properties/
│   └── launchSettings.json
├── Program.cs            # Application entry point
└── appsettings.json      # Configuration
```

## User Interface Architecture

<!-- TODO: Add UI Component Architecture Diagram Here -->
![UI Components Diagram](../docs/images/ui-components-diagram.png)
*User interface component hierarchy and interaction patterns*

### **UI Component Structure:**
- **Layout System**: Bootstrap grid with responsive breakpoints
- **Navigation**: Material Design navigation with authentication state
- **Search Components**: Advanced search forms with real-time feedback
- **Chat Interface**: Message bubbles with document citations
- **Document Cards**: Material Design cards for document display
- **Loading States**: Spinners and progress indicators

## Features Comparison with Original Python Sample

### ✅ Implemented Features

| Feature | Python/Streamlit | Razor Pages Frontend | Status |
|---------|-------------------|---------------------|---------|
| Search Interface | ✅ | ✅ | Complete |
| Chat Interface | ✅ | ✅ | Complete |
| Document Management | ✅ | ✅ | Complete |
| Index Management | ✅ | ✅ | Complete |
| Material Design UI | ✅ | ✅ | Complete |
| Responsive Design | ✅ | ✅ | Complete |
| Configuration Panel | ✅ | ✅ | Complete |
| Processing Steps Display | ✅ | ✅ | Complete |
| Error Handling | ✅ | ✅ | Complete |
| Loading States | ✅ | ✅ | Complete |

### 🔄 Enhanced Features (.NET Version)

- **Authentication**: Built-in Azure AD integration (vs. no auth in original)
- **Server-side rendering**: Better SEO and initial load performance
- **Type safety**: Strong typing with C# models
- **CSRF protection**: Built-in security features
- **Enterprise readiness**: Production-ready authentication and security

## API Integration

The Razor Pages frontend communicates with the same backend API patterns as the original Python sample:

- **Search**: `POST /api/search`
- **Chat**: `POST /api/chat`
- **Documents**: `GET/POST/DELETE /api/documents`
- **Indexes**: `GET/POST/DELETE /api/indexes`

## Authentication Flow

<!-- TODO: Add Authentication Flow Diagram Here -->
![Authentication Flow](../docs/images/auth-flow-diagram.png)
*Azure AD authentication flow with token management and API integration*

### **Authentication Process:**
1. **User Access** → Redirect to Azure AD login
2. **Azure AD Authentication** → Token acquisition
3. **Token Storage** → Secure token caching
4. **API Calls** → Authenticated requests with bearer tokens
5. **Token Refresh** → Automatic token renewal
6. **Logout** → Secure token cleanup

## Configuration

### API Settings

Configure the backend API endpoint in `appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7001"
  }
}
```

### Authentication

Configure Azure AD settings in `appsettings.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "your-domain.onmicrosoft.com",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "CallbackPath": "/signin-oidc"
  }
}
```

## Development

### Running in Development Mode

```bash
dotnet run --environment Development
```

### Building for Production

```bash
dotnet publish -c Release -o ./publish
```

### Docker Support

To add Docker support, create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["AISearch.Web.csproj", "."]
RUN dotnet restore "AISearch.Web.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "AISearch.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AISearch.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AISearch.Web.dll"]
```

## Contributing

1. Follow the existing code style and patterns
2. Ensure all features maintain UI consistency with the original Python sample
3. Add appropriate error handling and loading states
4. Test across different browsers and device sizes
5. Update this README if you add new features

## UI Components

The Razor Pages frontend uses the same design principles as the original Python sample:

- **Material Design**: Consistent color scheme and components
- **Bootstrap Grid**: Responsive layout system
- **Material Icons**: Same icon set as original sample
- **Card-based Layout**: Material Design cards for content organization
- **Color Scheme**: 
  - Primary: #2196f3 (Blue)
  - Secondary: #ff9800 (Orange)

## JavaScript Architecture

The frontend uses jQuery for DOM manipulation and AJAX calls:

- `app.js`: Main application logic for all features
- `site.js`: Utility functions and general enhancements
- Modular approach with separate functions for each feature
- Consistent error handling and loading states

## Styling

Custom CSS builds upon Bootstrap 5:

- Material Design principles
- Consistent spacing and typography
- Responsive breakpoints
- Accessibility features
- Dark mode support (optional)

## Browser Support

- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)
- Mobile browsers

## Performance Considerations

- Server-side rendering for better initial load
- Minified CSS and JavaScript in production
- Optimized images and icons
- Efficient API calls with proper error handling
- Progressive enhancement for JavaScript features

## Security

- CSRF protection for all forms
- XSS prevention with proper encoding
- Azure AD authentication integration
- Secure cookie handling
- Input validation and sanitization

## Accessibility

- ARIA labels and roles
- Keyboard navigation support
- Screen reader compatibility
- High contrast mode support
- Focus management

## .NET 9 Features

This application takes advantage of .NET 9 features including:

- **Enhanced Performance**: Improved runtime performance and memory management
- **C# 13 Support**: Latest language features and syntax improvements
- **ASP.NET Core 9**: Enhanced web framework capabilities
- **Modern Security**: Latest security features and vulnerability mitigations
- **Improved Tooling**: Better development experience with enhanced debugging and diagnostics

## Related Resources

### Original Azure Sample
- **[Azure AI Search Multimodal Sample](https://github.com/Azure-Samples/azure-ai-search-multimodal-sample)**: The original Python implementation that inspired this .NET version
- **Microsoft Learn**: [Build a multimodal search solution](https://docs.microsoft.com/en-us/azure/search/tutorial-multiple-data-sources)
- **Azure AI Search**: [Official documentation](https://docs.microsoft.com/en-us/azure/search/)

### .NET Resources
- **ASP.NET Core**: [Official documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- **Azure SDK for .NET**: [Documentation and samples](https://docs.microsoft.com/en-us/dotnet/azure/)
- **.NET 9**: [What's new documentation](https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9)
