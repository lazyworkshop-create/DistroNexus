#!/bin/bash

# Navigate to the script's directory to ensure we run from the correct location
cd "$(dirname "$0")"

echo "Checking environment..."

# Check if node_modules exists, install if not
if [ ! -d "node_modules" ]; then
    echo "Dependencies not found. Installing..."
    npm install
else
    echo "Dependencies found."
fi

echo "Starting Docusaurus Development Server..."
echo "Access the site at http://localhost:3000"

# Start the server
# --port 3000: Set port
# --host 0.0.0.0: Bind to all interfaces (useful for access from host/containers)
# --no-open: Do not try to automatically open the browser (avoids WSL/Windows interop issues)
# "$@": Pass any additional arguments to the script (e.g. --locale zh-Hans)
npm start -- --port 3000 --host 0.0.0.0 --no-open "$@"
