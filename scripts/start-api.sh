#!/bin/bash

echo "Starting AI Search API Backend (.NET 9)..."

cd ../dotnet/AISearch.Api

echo ""
echo "Checking for required configuration..."
if [ ! -f "appsettings.json" ]; then
    echo "WARNING: appsettings.json not found!"
    echo "Please configure Azure services before running the API."
    echo "See SETUP.md for configuration instructions."
    exit 1
fi

echo ""
echo "Starting API backend..."
echo "This will run on:"
echo "  - HTTPS: https://localhost:7001"
echo "  - HTTP:  http://localhost:5000"
echo "  - Swagger UI: https://localhost:7001/swagger"
echo ""

dotnet run

echo ""
echo "API Backend stopped."