#!/bin/bash

echo "Starting AI Search Solution..."

echo ""
echo "Starting .NET Backend..."
cd src/backend/AISearch.Api
dotnet run &
BACKEND_PID=$!

echo ""
echo "Waiting for backend to start..."
sleep 10

echo ""
echo "Starting React Frontend..."
cd ../../../src/frontend
npm start &
FRONTEND_PID=$!

echo ""
echo "Both services are starting..."
echo "Backend will be available at: http://localhost:5000"
echo "Frontend will be available at: http://localhost:3000"
echo "Swagger UI will be available at: http://localhost:5000"
echo ""
echo "Press Ctrl+C to stop both services"

# Function to handle cleanup
cleanup() {
    echo "Stopping services..."
    kill $BACKEND_PID 2>/dev/null
    kill $FRONTEND_PID 2>/dev/null
    exit 0
}

# Set trap for cleanup
trap cleanup SIGINT

# Wait for both processes
wait
