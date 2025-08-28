# AI Search Multimodal API Documentation

## Overview

This API provides comprehensive multimodal search capabilities using Azure AI Search, Azure OpenAI, and Azure Storage. The solution supports text and image indexing, vector search, and intelligent chat-based query processing.

## Base URL

```
http://localhost:5000/api
```

## Authentication

The API uses Azure Managed Identity for authentication. Ensure you're authenticated with Azure CLI or have appropriate managed identity configured.

---

## Search Endpoints

### POST /api/search/search
Performs text-based search across indexed documents.

**Request Body:**
```json
{
  "query": "What is machine learning?",
  "config": {
    "useKnowledgeAgent": false,
    "top": 10,
    "includeImages": true,
    "includeText": true,
    "threshold": 0.7,
    "filter": ["contentType eq 'text'"]
  },
  "chatHistory": []
}
```

**Response:**
```json
{
  "results": [
    {
      "id": "doc-123-chunk-0",
      "content": "Machine learning is a subset of artificial intelligence...",
      "contentType": "text",
      "score": 0.95,
      "sourcePath": "documents/ml-guide.pdf",
      "metadata": {
        "title": "Machine Learning Guide",
        "author": "John Doe"
      }
    }
  ],
  "requestId": "req-456",
  "totalCount": 15,
  "processingSteps": [
    {
      "title": "Search executed",
      "type": "search",
      "description": "Found 15 matching documents",
      "timestamp": "2024-01-01T12:00:00Z"
    }
  ]
}
```

### POST /api/search/vector-search
Performs vector-based similarity search using embeddings.

**Request Body:**
```json
{
  "query": "machine learning concepts",
  "queryVector": [0.1, 0.2, -0.3, ...], // 1536-dimensional vector
  "config": {
    "top": 5,
    "threshold": 0.8
  }
}
```

### GET /api/search/similar/{documentId}
Finds documents similar to the specified document.

**Parameters:**
- `documentId` (path): ID of the reference document
- `count` (query): Number of similar documents to return (default: 5)

---

## Chat Endpoints

### POST /api/chat
Processes a chat message using multimodal RAG.

**Request Body:**
```json
{
  "message": "Explain the key concepts in machine learning",
  "chatHistory": [
    {
      "role": "user",
      "content": "What is AI?"
    },
    {
      "role": "assistant", 
      "content": "AI is artificial intelligence..."
    }
  ],
  "searchConfig": {
    "useKnowledgeAgent": false,
    "top": 5,
    "includeImages": true,
    "includeText": true,
    "threshold": 0.7
  }
}
```

**Response:**
```json
{
  "response": "Based on your documents, machine learning has several key concepts...",
  "requestId": "chat-789",
  "citations": [
    {
      "id": "doc-123-chunk-0",
      "content": "Machine learning algorithms learn patterns...",
      "contentType": "text",
      "sourcePath": "documents/ml-guide.pdf",
      "title": "Machine Learning Guide",
      "relevance": 0.92
    }
  ],
  "processingSteps": [
    {
      "title": "Grounding user message",
      "type": "search",
      "description": "Searching for relevant content"
    },
    {
      "title": "LLM response generated", 
      "type": "llm",
      "description": "Generated response from language model"
    }
  ]
}
```

### POST /api/chat/stream
Provides streaming chat responses.

**Request Body:** Same as `/api/chat`

**Response:** Server-Sent Events stream with response chunks

### POST /api/chat/grounding
Gets grounding information without generating a response.

**Request Body:**
```json
{
  "query": "machine learning",
  "chatHistory": [],
  "searchConfig": {
    "top": 5,
    "threshold": 0.7
  }
}
```

---

## Document Endpoints

### POST /api/documents/upload
Uploads and indexes a new document.

**Request:** `multipart/form-data`
- `file`: Document file (required)
- `title`: Document title (optional)
- `description`: Document description (optional)

**Response:**
```json
{
  "documentId": "doc-abc123",
  "fileName": "research-paper.pdf",
  "contentType": "application/pdf",
  "fileSize": 1048576,
  "success": true,
  "message": "Document uploaded and indexed successfully",
  "uploadedAt": "2024-01-01T12:00:00Z"
}
```

### GET /api/documents
Lists all documents with pagination.

**Query Parameters:**
- `skip`: Number of documents to skip (default: 0)
- `take`: Number of documents to return (default: 20, max: 100)

**Response:**
```json
[
  {
    "id": "doc-abc123",
    "title": "Research Paper",
    "description": "A comprehensive study on...",
    "contentType": "application/pdf",
    "fileSize": 1048576,
    "createdAt": "2024-01-01T12:00:00Z",
    "lastModified": "2024-01-01T12:00:00Z",
    "metadata": {}
  }
]
```

### GET /api/documents/{documentId}
Gets information about a specific document.

### GET /api/documents/{documentId}/content
Gets the content of a specific document with chunks.

**Response:**
```json
{
  "id": "doc-abc123",
  "content": "Full document content...",
  "contentType": "text",
  "chunks": [
    {
      "id": "doc-abc123-chunk-0",
      "content": "First chunk content...",
      "contentType": "text",
      "chunkIndex": 0,
      "sourcePath": "documents/doc-abc123/file.pdf",
      "vector": [0.1, 0.2, -0.3, ...],
      "metadata": {}
    }
  ]
}
```

### DELETE /api/documents/{documentId}
Deletes a document and removes it from the index.

### POST /api/documents/{documentId}/reindex
Re-indexes a specific document.

---

## Index Management Endpoints

### GET /api/index
Lists all search indexes.

**Response:**
```json
[
  {
    "name": "multimodal-index",
    "documentCount": 150,
    "storageSize": 52428800,
    "lastModified": "2024-01-01T12:00:00Z"
  }
]
```

### GET /api/index/{indexName}
Gets information about a specific index.

### POST /api/index
Creates a new search index.

**Request Body:**
```json
{
  "name": "new-index",
  "schema": {
    "fields": [
      {
        "name": "custom_field",
        "type": "string",
        "isSearchable": true,
        "isFilterable": false,
        "isSortable": false,
        "isFacetable": false,
        "isRetrievable": true
      }
    ],
    "scoringProfiles": [],
    "corsOptions": null
  }
}
```

### DELETE /api/index/{indexName}
Deletes a search index.

---

## Data Models

### SearchConfig
```json
{
  "useKnowledgeAgent": false,
  "top": 10,
  "includeImages": true,
  "includeText": true,
  "threshold": 0.7,
  "filter": ["field eq 'value'"]
}
```

### ChatMessage
```json
{
  "role": "user|assistant|system",
  "content": "Message content"
}
```

### ProcessingStep
```json
{
  "title": "Step title",
  "type": "search|llm|data|error",
  "description": "Optional description",
  "content": {}, // Optional structured content
  "timestamp": "2024-01-01T12:00:00Z"
}
```

---

## Error Handling

All endpoints return standard HTTP status codes:

- **200 OK**: Request successful
- **201 Created**: Resource created successfully
- **400 Bad Request**: Invalid request parameters
- **404 Not Found**: Resource not found
- **500 Internal Server Error**: Server error

**Error Response Format:**
```json
{
  "message": "Error description",
  "details": "Additional error details",
  "timestamp": "2024-01-01T12:00:00Z"
}
```

---

## Rate Limits

The API respects Azure service rate limits:
- Azure AI Search: 1000 requests per minute
- Azure OpenAI: Model-specific limits
- Azure Storage: 20,000 requests per second

---

## SDKs and Examples

### JavaScript/TypeScript
```javascript
import { apiService } from './apiService';

// Search documents
const results = await apiService.search({
  query: "machine learning",
  config: { top: 10, threshold: 0.7 },
  chatHistory: []
});

// Upload document
const uploadResult = await apiService.uploadDocument(
  file, 
  "Document Title", 
  "Document description"
);
```

### cURL Examples

**Search:**
```bash
curl -X POST "http://localhost:5000/api/search/search" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "machine learning",
    "config": {"top": 5, "threshold": 0.7},
    "chatHistory": []
  }'
```

**Upload Document:**
```bash
curl -X POST "http://localhost:5000/api/documents/upload" \
  -F "file=@document.pdf" \
  -F "title=Research Paper" \
  -F "description=ML research"
```

---

## Swagger UI

Interactive API documentation is available at:
```
http://localhost:5000
```

The Swagger UI provides:
- Interactive endpoint testing
- Request/response schema documentation
- Authentication configuration
- Example requests and responses
