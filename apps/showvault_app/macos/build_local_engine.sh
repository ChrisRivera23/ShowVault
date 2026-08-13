#!/bin/bash
set -euo pipefail

case "${CURRENT_ARCH:-$(uname -m)}" in
  arm64) runtime_identifier="osx-arm64" ;;
  x86_64) runtime_identifier="osx-x64" ;;
  *) echo "Unsupported local-engine architecture" >&2; exit 1 ;;
esac

project_path="$SRCROOT/../../../services/local-engine/src/ShowVault.LocalEngine.Host/ShowVault.LocalEngine.Host.csproj"
output_path="$TARGET_BUILD_DIR/$UNLOCALIZED_RESOURCES_FOLDER_PATH/local-engine"
sync_project_path="$SRCROOT/../../../services/local-engine/src/ShowVault.SyncEngine.Host/ShowVault.SyncEngine.Host.csproj"

dotnet publish "$project_path" \
  --configuration Release \
  --runtime "$runtime_identifier" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  --output "$output_path"

dotnet publish "$sync_project_path" \
  --configuration Release \
  --runtime "$runtime_identifier" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  --output "$output_path"
