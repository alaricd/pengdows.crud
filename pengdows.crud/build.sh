#!/bin/bash
set -e

# Example: YYYY.MM.DD.BUILDNUM
BUILD_DATE=$(date +%Y.%m.%d)
BUILD_NUM=$(git rev-list --count HEAD)  # Optional, gives you incrementing number
VERSION="${BUILD_DATE}.${BUILD_NUM}"

echo "Building pengdows.crud version $VERSION"

# Update the .csproj with the new version
sed -i "s|<Version>.*</Version>|<Version>$VERSION</Version>|" pengdows.crud.csproj

# Build and pack
dotnet pack -c Release

