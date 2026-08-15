#!/bin/bash
set -euo pipefail

# Configuration
GIT_REPO="https://github.com/wiglenet/wigle-wifi-wardriving.git"
GIT_TAG=${GIT_TAG:-foss-2.104}
SOURCE_DIR="/app/source"
OUTPUT_DIR="/app/source/wiglewifiwardriving/build/outputs/apk/debug"

echo "🚀 Starting WiGLE build process..."

# Set default MAPS_API_KEY if not defined
if [ -z "${MAPS_API_KEY:-}" ]; then
    MAPS_API_KEY="undefined"
    echo "⚠️ MAPS_API_KEY not defined. Using default value: ${MAPS_API_KEY}"
fi

# Check if SERVER_URL is defined
if [ -z "${SERVER_URL:-}" ]; then
    echo "❌ ERROR: SERVER_URL is not defined. Exiting..."
    exit 1
fi

# Check if API_HOST is defined, if not, use SERVER_URL
if [ -z "${API_HOST:-}" ]; then
    API_HOST=$(echo "$SERVER_URL" | sed 's|https\?://||;s|/$||')
    echo "⚠️ API_HOST not defined. Using SERVER_URL as API_HOST: $API_HOST"
fi

# Check if source directory exists
if [ ! -d "$SOURCE_DIR/.git" ]; then
    echo "📥 Downloading source code from ${GIT_REPO} (tag: ${GIT_TAG})..."

    # Try to clone specific tag first, fall back to main branch
    if git clone --depth 1 --branch "${GIT_TAG}" "${GIT_REPO}" "${SOURCE_DIR}"; then
        echo "✅ Successfully cloned tag ${GIT_TAG}"
    else
        echo "⚠️ Tag ${GIT_TAG} not found, falling back to main branch..."
        git clone --depth 1 "${GIT_REPO}" "${SOURCE_DIR}"
    fi
else
    echo "✅ Source code already exists at ${SOURCE_DIR}"
fi

# Change to source directory
cd "${SOURCE_DIR}"

echo "🔧 Patching source code..."

# Make gradlew executable
dos2unix gradlew && chmod +x gradlew

# Update UrlConfig.java with custom API URLs
CONFIG_FILE_PATH="wiglewifiwardriving/src/main/java/net/wigle/wigleandroid/util/UrlConfig.java"

REG_URL="${SERVER_URL}/register"

# Update the config fields
sed -i "s|\( API_DOMAIN = \)[^;]*\(;\)|\1\"${API_HOST}\"\2|" "$CONFIG_FILE_PATH"
sed -i "s|\( WIGLE_BASE_URL = \)[^;]*\(;\)|\1\"${SERVER_URL}\"\2|" "$CONFIG_FILE_PATH"
sed -i "s|\( REG_URL = \)[^;]*\(;\)|\1\"${REG_URL}\"\2|" "$CONFIG_FILE_PATH"

# Update the mapping fragment with the custom tile host.
# The class was renamed MappingFragment -> AbstractMappingFragment in the foss-* tags,
# so only one of these exists in any given revision. Patch whichever is present.
patched_fragment=0
for fragment in MappingFragment AbstractMappingFragment; do
    fragment_path="wiglewifiwardriving/src/main/java/net/wigle/wigleandroid/${fragment}.java"
    if [ -f "$fragment_path" ]; then
        sed -Ei "s#https?://[^/]+(/clientTile)#${SERVER_URL}\1#g" "$fragment_path"
        patched_fragment=1
    fi
done

if [ "$patched_fragment" -eq 0 ]; then
    echo "❌ ERROR: no mapping fragment found to patch in ${GIT_TAG}. Exiting..."
    exit 1
fi

# Generate local.properties
cat > local.properties <<EOF
MAPS_API_KEY=${MAPS_API_KEY}
sdk.dir=${ANDROID_HOME}
API_HOST=${API_HOST}
SERVER_URL=${SERVER_URL}
EOF

echo "✅ Source code patched."

# Build the project
echo "🛠️ Building project..."
./gradlew assembleDebug --no-daemon

echo "Copy to output directory..."
cp -r $OUTPUT_DIR/ /app/output/

echo "✅ Build done."