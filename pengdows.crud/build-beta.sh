#!/bin/bash
set -e

# Example version: YYYY.MM.DD.BUILDNUM
BUILD_DATE=$(date +%Y.%m.%d)
BUILD_NUM=$(git rev-list --count HEAD)  # Incrementing build number based on commits
VERSION="${BUILD_DATE}.${BUILD_NUM}-beta"

echo "Building pengdows.crud version $VERSION"

# Update the .csproj with the new version
sed -i "s|<Version>.*</Version>|<Version>$VERSION</Version>|" pengdows.crud.csproj

# Build and pack with updated version
dotnet pack -c Debug
