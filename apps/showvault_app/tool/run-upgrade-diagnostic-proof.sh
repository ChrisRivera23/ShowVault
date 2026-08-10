#!/bin/bash

set -euo pipefail

usage() {
  echo "Usage: $0 <absolute-output-directory>" >&2
  exit 64
}

if [[ $# -ne 1 || "$1" != /* ]]; then
  usage
fi

output_directory=$1
if [[ -e "$output_directory" ]]; then
  echo "Output directory already exists: $output_directory" >&2
  exit 73
fi

for command_name in codesign ditto flutter python3 shasum; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required upgrade-proof command is unavailable: $command_name" >&2
    exit 69
  fi
done

if [[ -z "${DEVELOPER_DIR:-}" && -d /Applications/Xcode.app/Contents/Developer ]]; then
  export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
fi
if ! xcodebuild -version >/dev/null 2>&1; then
  echo "A working full Xcode installation is required." >&2
  exit 69
fi

script_directory=$(cd "$(dirname "$0")" && pwd)
app_directory=$(cd "$script_directory/.." && pwd)
work_root=$(mktemp -d "${TMPDIR:-/tmp}/showvault-upgrade-proof.XXXXXX")
fixture_name="showvault-upgrade-proof-$$"
ownership_marker="$work_root/.showvault-upgrade-proof-owned"
touch "$ownership_marker"
installed_directory="$work_root/Installed"
installed_app="$installed_directory/ShowVault.app"
app_executable=''

cleanup() {
  if [[ -n "$app_executable" && -x "$app_executable" ]]; then
    "$app_executable" --showvault-upgrade-phase cleanup >/dev/null 2>&1 || true
  fi
  case "$work_root" in
    /tmp/showvault-upgrade-proof.*|/private/tmp/showvault-upgrade-proof.*|/var/folders/*/showvault-upgrade-proof.*)
      if [[ -f "$ownership_marker" ]]; then
        rm -rf -- "$work_root"
      fi
      ;;
  esac
}
trap cleanup EXIT INT TERM

build_generation() {
  local generation=$1
  flutter clean >/dev/null
  flutter pub get >/dev/null
  flutter build macos --release \
    --dart-define="SHOWVAULT_SYNTHETIC_FIXTURE_HOME=$fixture_name" \
    --dart-define="SHOWVAULT_UPGRADE_HARNESS=true" \
    --dart-define="SHOWVAULT_UPGRADE_GENERATION=$generation"
}

resolve_executable() {
  local application=$1
  local executable_name
  executable_name=$(
    /usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' \
      "$application/Contents/Info.plist"
  )
  if [[ -z "$executable_name" || "$executable_name" == */* ]]; then
    echo "The upgrade-proof executable identity is unsafe." >&2
    exit 70
  fi
  echo "$application/Contents/MacOS/$executable_name"
}

cd "$app_directory"
build_generation before
build_product="$app_directory/build/macos/Build/Products/Release/ShowVault.app"
if [[ ! -d "$build_product" ]]; then
  echo "The before-upgrade application was not produced." >&2
  exit 70
fi
mkdir -p "$output_directory" "$installed_directory"
ditto "$build_product" "$output_directory/ShowVault-before.app"
ditto "$output_directory/ShowVault-before.app" "$installed_app"
codesign --verify --deep --strict "$installed_app"
app_executable=$(resolve_executable "$installed_app")
"$app_executable" --showvault-upgrade-phase prepare

build_generation after
build_product="$app_directory/build/macos/Build/Products/Release/ShowVault.app"
if [[ ! -d "$build_product" ]]; then
  echo "The after-upgrade application was not produced." >&2
  exit 70
fi
ditto "$build_product" "$output_directory/ShowVault-after.app"
mv "$installed_app" "$installed_directory/ShowVault-before-replaced.app"
ditto "$output_directory/ShowVault-after.app" "$installed_app"
codesign --verify --deep --strict "$installed_app"
app_executable=$(resolve_executable "$installed_app")
verification_output=$(
  "$app_executable" --showvault-upgrade-phase verify
)
echo "$verification_output"
report_base64=$(
  echo "$verification_output" | sed -n 's/^SHOWVAULT_UPGRADE_REPORT://p'
)
if [[ -z "$report_base64" ]]; then
  echo "The upgrade evidence report was not exported." >&2
  exit 70
fi
echo "$report_base64" | /usr/bin/base64 -D \
  >"$output_directory/upgrade-diagnostic-report.json"

python3 - "$output_directory/upgrade-diagnostic-report.json" <<'PY'
import hashlib
import json
import sys

path = sys.argv[1]
with open(path, encoding="utf-8") as handle:
    report = json.load(handle)
expected = report.pop("evidenceSha256")
actual = hashlib.sha256(
    json.dumps(report, separators=(",", ":")).encode()
).hexdigest()
if actual != expected:
    raise SystemExit("Upgrade evidence checksum validation failed.")
preservation = report["preservation"]
required = (
    "installedArtifactReplaced",
    "immutableRecoveryPointVerified",
    "independentManifestVerified",
    "queueJournalSurvived",
    "restoreEvidenceSurvived",
    "rehydratedWithoutSourceScan",
)
if not all(preservation.get(key) is True for key in required):
    raise SystemExit("Upgrade preservation evidence is incomplete.")
if preservation.get("sourcePresentDuringRehydration") is not False:
    raise SystemExit("The source-removal proof is incomplete.")
PY

if grep -E '/Users/|/private/|/var/folders/|/tmp/|file://|Bearer |accessToken|refreshToken|password|secret' \
    "$output_directory/upgrade-diagnostic-report.json" >/dev/null; then
  echo "The upgrade evidence report contains a prohibited value." >&2
  exit 70
fi

ditto -c -k --sequesterRsrc --keepParent \
  "$output_directory/ShowVault-before.app" \
  "$output_directory/ShowVault-before-macos.zip"
ditto -c -k --sequesterRsrc --keepParent \
  "$output_directory/ShowVault-after.app" \
  "$output_directory/ShowVault-after-macos.zip"
(
  cd "$output_directory"
  shasum -a 256 \
    ShowVault-before-macos.zip \
    ShowVault-after-macos.zip \
    upgrade-diagnostic-report.json >SHA256SUMS
)

"$app_executable" --showvault-upgrade-phase cleanup >/dev/null
app_executable=''

echo "Created installed macOS upgrade and diagnostic evidence in $output_directory"
echo "Verified: replacement, source-free rehydration, manifest, queue journal, restore evidence"
echo "Attended uninstall data removal: documented, not executed"
echo "Host reboot, Windows, notarization, and production provider: not executed"
