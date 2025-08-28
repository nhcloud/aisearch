# AI Search Solution - Setup and Configuration Guide

## Prerequisites

Before running the solution, ensure you have the following installed:

1. **.NET 9 SDK**
   - Download from: https://dotnet.microsoft.com/download/dotnet/9.0

2. **Node.js 18+**
   - Download from: https://nodejs.org/

3. **Azure Services**
   - Azure AI Search service
   - Azure OpenAI service
   - Azure Storage Account

## Azure Setup

### 1. Azure AI Search
1. Create an Azure AI Search service in the Azure portal
2. Note the service endpoint (e.g., `https://your-search-service.search.windows.net`)
3. Get the admin API key or configure managed identity

### 2. Azure OpenAI
1. Create an Azure OpenAI service
2. Deploy the following models:
   - `gpt-4o` (for chat completion)
   - `text-embedding-ada-002` (for embeddings)
3. Note the endpoint and deployment names

### 3. Azure Storage
1. Create a storage account
2. Create the following containers:
   - `documents` (for uploaded files)
   - `artifacts` (for processed content)
   - `samples` (for sample data)

## Configuration

### Backend Configuration

Update `src/backend/AISearch.Api/appsettings.json`:

```json
{
  "AzureSearch": {
    "ServiceEndpoint": "https://your-search-service.search.windows.net",
    "IndexName": "multimodal-index",
    "OpenAIEndpoint": "https://your-openai-service.openai.azure.com",
    "OpenAIModelName": "gpt-4o",
    "OpenAIDeploymentName": "gpt-4o",
    "StorageAccountUrl": "https://yourstorageaccount.blob.core.windows.net",
    "ArtifactsContainer": "artifacts",
    "SamplesContainer": "samples",
    "KnowledgeAgentName": "your-knowledge-agent"
  }
}
```

### Environment Variables (Alternative)

You can also use environment variables:

```bash
# Azure Search
AZURE_SEARCH_SERVICE_ENDPOINT=https://your-search-service.search.windows.net
AZURE_SEARCH_INDEX_NAME=multimodal-index

# Azure OpenAI
AZURE_OPENAI_ENDPOINT=https://your-openai-service.openai.azure.com
AZURE_OPENAI_MODEL_NAME=gpt-4o
AZURE_OPENAI_DEPLOYMENT=gpt-4o

# Azure Storage
AZURE_STORAGE_ACCOUNT_URL=https://yourstorageaccount.blob.core.windows.net
ARTIFACTS_STORAGE_CONTAINER=artifacts
SAMPLES_STORAGE_CONTAINER=samples
```

### Frontend Configuration

Create `src/frontend/.env`:

```
REACT_APP_API_URL=http://localhost:5000/api
```

## Installation and Setup

### 1. Install Backend Dependencies

```bash
cd src/backend/AISearch.Api
dotnet restore
```

### 2. Install Frontend Dependencies

```bash
cd src/frontend
npm install
```

### 3. Create Search Index

The application will automatically create the required search index on first run, or you can use the Index Management page in the frontend.

## Running the Solution

### Option 1: Using Start Scripts

**Windows:**
```bash
start.bat
```

**Linux/Mac:**
```bash
chmod +x start.sh
./start.sh
```

### Option 2: Manual Start

**Backend:**
```bash
cd src/backend/AISearch.Api
dotnet run
```

**Frontend:**
```bash
cd src/frontend
npm start
```

## Accessing the Application

- **Frontend**: http://localhost:3000
- **Backend API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000 (opens by default)

## Authentication

The solution uses Azure Managed Identity for authentication. Ensure you're logged in to Azure CLI:

```bash
az login
```

For development, you can also use:
- Azure CLI authentication
- Visual Studio authentication
- Environment variables with connection strings

## Features Overview

### 1. Search Page
- Text and vector search capabilities
- Configurable search parameters
- Results with relevance scores
- Processing step visibility

### 2. Chat Page
- Interactive chat with your documents
- Multimodal RAG responses
- Citation tracking
- Real-time streaming responses

### 3. Documents Page
- Upload documents (text, images, PDFs)
- View document library
- Delete and reindex documents
- Automatic content extraction and embedding

### 4. Index Management
- Create and delete search indexes
- View index statistics
- Monitor storage usage

## Troubleshooting

### Common Issues

1. **Authentication Errors**
   - Ensure you're logged in with `az login`
   - Check that your Azure account has access to the services
   - Verify service endpoints in configuration

2. **CORS Errors**
   - Check that the frontend URL is allowed in the backend CORS configuration
   - Ensure the API base URL is correct in the frontend

3. **Search Index Issues**
   - Create the index manually using the Index Management page
   - Check Azure AI Search service status and pricing tier

4. **OpenAI Model Errors**
   - Verify model deployments in Azure OpenAI studio
   - Check deployment names match configuration
   - Ensure sufficient quota and rate limits

### Development Tips

1. **Hot Reload**: Both frontend and backend support hot reload during development
2. **Logging**: Check browser console and backend logs for detailed error information
3. **API Testing**: Use the Swagger UI at http://localhost:5000 to test API endpoints directly

## Next Steps

1. **Customize Document Processing**: Extend the document extraction logic for specific file types
2. **Add Security**: Implement proper authentication and authorization
3. **Scale for Production**: Configure for Azure deployment with proper monitoring
4. **Extend Search**: Add more sophisticated search features and filters

## Support

For issues and questions, check:
- Console logs (browser and backend)
- Azure portal for service health
- Azure AI Search portal for index status
- Azure OpenAI studio for model availability
