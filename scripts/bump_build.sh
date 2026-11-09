#!/usr/bin/env bash
# ==============================================================================
# CherryHud Build Version Bumping Script
# - Dev mode (default): increments 4th build digit (e.g. 1.0.2.1 -> 1.0.2.2)
# - Release mode (--release / -r): sets clean 3-digit release (e.g. 1.0.2.1 -> 1.0.2)
# - Custom version: ./scripts/bump_build.sh 1.0.3
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CSPROJ="$REPO_ROOT/CherryHud/CherryHud.csproj"
PLIST="$REPO_ROOT/CherryHud/Info.plist"
MANIFEST="$REPO_ROOT/CherryHud/app.manifest"

CURRENT_VER=$(grep -o '<Version>[^<]*</Version>' "$CSPROJ" | sed -e 's/<Version>//' -e 's/<\/Version>//')

if [[ -z "$CURRENT_VER" ]]; then
    echo "❌ Error: Could not determine current version from $CSPROJ"
    exit 1
fi

ACTION="dev"
TARGET_VER=""

if [[ $# -gt 0 ]]; then
    case "$1" in
        --release|-r)
            ACTION="release"
            ;;
        --dev|-d)
            ACTION="dev"
            ;;
        *)
            TARGET_VER="$1"
            ACTION="custom"
            ;;
    esac
fi

IFS='.' read -r -a PARTS <<< "$CURRENT_VER"

if [[ "$ACTION" == "release" ]]; then
    # Format to clean 3-digit release (e.g. 1.0.2)
    NEW_VER="${PARTS[0]}.${PARTS[1]}.${PARTS[2]}"
    echo "📦 Setting official 3-digit release version: ${CURRENT_VER} -> ${NEW_VER}"
elif [[ "$ACTION" == "custom" ]]; then
    NEW_VER="$TARGET_VER"
    echo "🍒 Setting custom version: ${CURRENT_VER} -> ${NEW_VER}"
else
    # Dev tracking: increment or add 4th build digit
    if [[ ${#PARTS[@]} -eq 3 ]]; then
        NEW_VER="${PARTS[0]}.${PARTS[1]}.${PARTS[2]}.1"
    elif [[ ${#PARTS[@]} -ge 4 ]]; then
        BUILD_NUM=$((PARTS[3] + 1))
        NEW_VER="${PARTS[0]}.${PARTS[1]}.${PARTS[2]}.${BUILD_NUM}"
    else
        NEW_VER="${CURRENT_VER}.1"
    fi
    echo "🍒 Bumping dev build version: ${CURRENT_VER} -> ${NEW_VER}"
fi

# Update CherryHud.csproj
sed -i '' -e "s/<Version>[^<]*<\/Version>/<Version>${NEW_VER}<\/Version>/" "$CSPROJ"
sed -i '' -e "s/<AssemblyVersion>[^<]*<\/AssemblyVersion>/<AssemblyVersion>${NEW_VER}<\/AssemblyVersion>/" "$CSPROJ"
sed -i '' -e "s/<FileVersion>[^<]*<\/FileVersion>/<FileVersion>${NEW_VER}<\/FileVersion>/" "$CSPROJ"

# Update Info.plist
if [[ -f "$PLIST" ]]; then
    sed -i '' -e "s/<string>${CURRENT_VER}<\/string>/<string>${NEW_VER}<\/string>/g" "$PLIST"
fi

# Update app.manifest
if [[ -f "$MANIFEST" ]]; then
    sed -i '' -e "s/version=\"[^\"]*\"/version=\"${NEW_VER}\"/" "$MANIFEST"
fi

echo "✅ Successfully updated version to ${NEW_VER} in:"
echo "   - $CSPROJ"
echo "   - $PLIST"
echo "   - $MANIFEST"
