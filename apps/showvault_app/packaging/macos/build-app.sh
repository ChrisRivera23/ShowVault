#!/bin/bash

set -euo pipefail

usage() {
  echo "Usage: $0 <absolute-output-directory> [api-base-url] [--personal-beta-no-login]" >&2
  exit 64
}

if [[ $# -lt 1 || $# -gt 3 ]]; then
  usage
fi

output_directory=$1
api_base_url=${2:-https://api.showvault.app}
personal_beta_option=${3:-}

if [[ -n "$personal_beta_option" && "$personal_beta_option" != "--personal-beta-no-login" ]]; then
  usage
fi
if [[ "$output_directory" != /* || -e "$output_directory" ]]; then
  echo "Output must be an absolute path that does not already exist." >&2
  exit 73
fi

case "$api_base_url" in
  https://*) ;;
  http://127.0.0.1|http://127.0.0.1:*|http://localhost|http://localhost:*|http://\[::1\]|http://\[::1\]:*) ;;
  *)
    echo "API origin must use HTTPS; only loopback HTTP is allowed for controlled testing." >&2
    exit 64
    ;;
esac

if [[ "$personal_beta_option" == "--personal-beta-no-login" ]]; then
  case "$api_base_url" in
    http://127.0.0.1|http://127.0.0.1:*|http://localhost|http://localhost:*|http://\[::1\]|http://\[::1\]:*) ;;
    *)
      echo "The no-login personal beta requires a loopback HTTP API origin." >&2
      exit 64
      ;;
  esac
fi

if [[ -z "${DEVELOPER_DIR:-}" && -d /Applications/Xcode.app/Contents/Developer ]]; then
  export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
fi
for command_name in flutter xcodebuild ditto shasum; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required build command is unavailable: $command_name" >&2
    exit 69
  fi
done
if ! xcodebuild -version >/dev/null; then
  echo "A working full Xcode installation is required." >&2
  exit 69
fi

script_directory=$(cd "$(dirname "$0")" && pwd)
app_directory=$(cd "$script_directory/../.." && pwd)
build_product="$app_directory/build/macos/Build/Products/Release/ShowVault.app"

cd "$app_directory"
flutter pub get
flutter_build_args=(
  macos
  --release
  --dart-define="SHOWVAULT_API_BASE_URL=$api_base_url"
)
if [[ "$personal_beta_option" == "--personal-beta-no-login" ]]; then
  flutter_build_args+=(--dart-define="SHOWVAULT_PERSONAL_BETA_BYPASS_AUTH=true")
fi
flutter build "${flutter_build_args[@]}"

if [[ ! -d "$build_product" ]]; then
  echo "Expected release application was not produced: $build_product" >&2
  exit 70
fi

mkdir -p "$output_directory"
ditto "$build_product" "$output_directory/ShowVault.app"
ditto -c -k --sequesterRsrc --keepParent \
  "$output_directory/ShowVault.app" \
  "$output_directory/ShowVault-macos.zip"
(
  cd "$output_directory"
  shasum -a 256 ShowVault-macos.zip > SHA256SUMS
)

echo "Created ad hoc personal-test artifacts in $output_directory"
echo "These artifacts are not signed or notarized for distribution."
