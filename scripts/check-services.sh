#!/bin/bash

echo "==============================================="
echo "AI Search Solution - Service Status Check"
echo "==============================================="

echo ""
echo "Checking service status..."

echo ""
echo "[1/2] Checking API Backend (https://localhost:7001)..."
if curl -k -s -o /dev/null -w "%{http_code}" https://localhost:7001/swagger 2>/dev/null | grep -q "200\|302"; then
    echo "? API Backend is running"
else
    echo "? API Backend is not running"
fi

echo ""
echo "[2/2] Checking Web Frontend (https://localhost:7002)..."
if curl -k -s -o /dev/null -w "%{http_code}" https://localhost:7002 2>/dev/null | grep -q "200\|302"; then
    echo "? Web Frontend is running"
else
    echo "? Web Frontend is not running"
fi

echo ""
echo "Alternative HTTP endpoints check:"
echo ""
echo "Checking API Backend (http://localhost:5000)..."
if curl -s -o /dev/null -w "%{http_code}" http://localhost:5000 2>/dev/null | grep -q "200\|302"; then
    echo "? API Backend HTTP is running"
else
    echo "? API Backend HTTP is not running"
fi

echo ""
echo "Checking Web Frontend (http://localhost:5002)..."
if curl -s -o /dev/null -w "%{http_code}" http://localhost:5002 2>/dev/null | grep -q "200\|302"; then
    echo "? Web Frontend HTTP is running"
else
    echo "? Web Frontend HTTP is not running"
fi

echo ""
echo "==============================================="
echo "Service URLs:"
echo "   ?? Web Frontend:    https://localhost:7002"
echo "   ?? API Backend:     https://localhost:7001"
echo "   ?? Swagger UI:      https://localhost:7001/swagger"
echo "==============================================="
echo ""
echo "To start services: run './start.sh' from the root directory"