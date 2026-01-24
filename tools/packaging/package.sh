#!/bin/bash
set -e

# Package script for DistroNexus
# Usage: ./package.sh [version]

VERSION=${1:-"1.0.2"}
PROJECT_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
BUILD_DIR="$PROJECT_ROOT/build"
RELEASE_DIR="$PROJECT_ROOT/release"
PACKAGE_DIR="$PROJECT_ROOT/tools/packaging"

echo "Packaging DistroNexus v$VERSION..."

# 1. Clean and Build
echo "Building project..."
cd "$PROJECT_ROOT"
./tools/build.sh

# 2. Check build output
if [ ! -f "$BUILD_DIR/DistroNexus.exe" ]; then
    echo "Error: Build failed. DistroNexus.exe not found."
    exit 1
fi

# Clean potentially stale artifacts from build (logs, etc if copied)
# (scripts/ directory inside source might contain logs/ from dev runs)

# 3. Create Release Directory
mkdir -p "$RELEASE_DIR"

# 4. Create ZIP Archive (Portable)
echo "Creating Portable ZIP..."
ZIP_NAME="DistroNexus_v${VERSION}_portable.zip"
TMP_ZIP_DIR="$RELEASE_DIR/tmp_zip/DistroNexus"

rm -rf "$TMP_ZIP_DIR"
mkdir -p "$TMP_ZIP_DIR"

# Copy files
cp "$BUILD_DIR/DistroNexus.exe" "$TMP_ZIP_DIR/"
cp -r "$PROJECT_ROOT/scripts" "$TMP_ZIP_DIR/"
cp -r "$PROJECT_ROOT/config" "$TMP_ZIP_DIR/"
cp "$PROJECT_ROOT/README.md" "$TMP_ZIP_DIR/"
cp "$PROJECT_ROOT/README_CN.md" "$TMP_ZIP_DIR/"
# Copy specific release note if exists
if [ -f "$PROJECT_ROOT/docs/release_notes/v$VERSION.md" ]; then
    cp "$PROJECT_ROOT/docs/release_notes/v$VERSION.md" "$TMP_ZIP_DIR/RELEASE_NOTES.md"
fi

# Cleanup dev artifacts from copy
rm -rf "$TMP_ZIP_DIR/scripts/logs"
rm -f "$TMP_ZIP_DIR/config/instances.json" # Ensure we don't ship my local instances
# Keep settings.json (it's the template)

# Create zip
cd "$RELEASE_DIR/tmp_zip"
zip -r "$RELEASE_DIR/$ZIP_NAME" "DistroNexus"
echo "Portable ZIP created at $RELEASE_DIR/$ZIP_NAME"

# Clean up temp
cd "$PROJECT_ROOT"
rm -rf "$RELEASE_DIR/tmp_zip"

# 5. Build Installer (Inno Setup)
# Check for ISCC
if command -v iscc &> /dev/null; then
    echo "Compiling Installer with Inno Setup..."
    cd "$PACKAGE_DIR"
    iscc "/dMyAppVersion=$VERSION" DistroNexus.iss
    echo "Installer created in $RELEASE_DIR"
else
    echo "Warning: 'iscc' (Inno Setup Compiler) not found in PATH."
    echo "Skipping Installer generation. (If on CI, ensure Inno Setup is installed)"
    echo "Artifacts generated: ZIP only."
fi

echo "Packaging Complete."
