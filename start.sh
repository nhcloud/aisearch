#!/bin/bash

echo "==============================================="
echo "Starting AI Search Solution (.NET 9)"
echo "==============================================="

# Function to handle cleanup
cleanup() {
    echo ""
    echo "Stopping services..."
    if [ ! -z "$API_PID" ]; then
        kill $API_PID 2>/dev/null
        echo "Stopped API Backend (PID: $API_PID)"
    fi
    if [ ! -z "$WEB_PID" ]; then
        kill $WEB_PID 2>/dev/null
        echo "Stopped Web Frontend (PID: $WEB_PID)"
    fi
    echo "All services stopped."
    exit 0
}

# Set trap for cleanup on script termination
trap cleanup SIGINT SIGTERM

echo ""
echo "[1/2] Starting AI Search API Backend..."
cd dotnet/AISearch.Api
dotnet run &
API_PID=$!
echo "API Backend started with PID: $API_PID"
cd ../..

echo ""
echo "Waiting for API to initialize..."
sleep 8

echo ""
echo "[2/2] Starting AI Search Web Frontend..."
cd dotnet/AISearch.Web
dotnet run &
WEB_PID=$!
echo "Web Frontend started with PID: $WEB_PID"
cd ../..

echo ""
echo "==============================================="
echo "? Both services are starting..."
echo "==============================================="
echo ""
echo "Service URLs:"
echo "   ?? Web Frontend:    https://localhost:7002"
echo "   ?? API Backend:     https://localhost:7001"
echo "   ?? Swagger UI:      https://localhost:7001/swagger"
echo ""
echo "Alternative HTTP URLs (if HTTPS issues):"
echo "   ?? Web Frontend:    http://localhost:5002"
echo "   ?? API Backend:     http://localhost:5000"
echo ""
echo "Please wait for both services to fully start..."
echo ""

# Wait a bit more for services to start
sleep 10

echo "Service Status Check:"
# Check API status
if curl -k -s -o /dev/null -w "%{http_code}" https://localhost:7001/swagger | grep -q "200\|302"; then
    echo "? API Backend is running and accessible"
else
    echo "??  API Backend may still be starting - please wait a moment"
fi

# Check Web status
if curl -k -s -o /dev/null -w "%{http_code}" https://localhost:7002 | grep -q "200\|302"; then
    echo "? Web Frontend is running and accessible"
else
    echo "??  Web Frontend may still be starting - please wait a moment"
fi

echo ""
echo "Next steps:"
echo "   1. Open https://localhost:7002 in your browser"
echo "   2. Sign in with your Azure AD credentials"
echo "   3. Start using the AI Search interface"
echo ""
echo "To stop services: Press Ctrl+C"
echo ""
echo "Press Ctrl+C to stop both services..."

# Wait for both processes
wait
