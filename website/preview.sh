#!/bin/bash

# Navigate to the script's directory
cd "$(dirname "$0")"

echo "Checking if build exists..."
if [ ! -d "build" ]; then
    echo "Build directory not found. Running build..."
    npm run build
fi

echo "Starting Production Preview Server..."
echo "This server supports ALL languages (English/Chinese) and true switching."
echo "Access the site at http://localhost:3000/DistroNexus/"

# Serve the build folder
npm run serve -- --port 3000 --host 0.0.0.0 --no-open
