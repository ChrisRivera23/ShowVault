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

for command_name in curl ditto docker dotnet flutter python3 shasum; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required resilience command is unavailable: $command_name" >&2
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
repository_root=$(cd "$app_directory/../.." && pwd)
api_project="$repository_root/services/api/src/ShowVault.Api/ShowVault.Api.csproj"
compose_project="showvault-resilience-$$"
work_root=$(mktemp -d "${TMPDIR:-/tmp}/showvault-resilience.XXXXXX")
fixture_name="showvault-resilience-$compose_project"
api_log="$work_root/api.log"
ownership_marker="$work_root/.showvault-resilience-owned"
touch "$ownership_marker"

read -r api_port postgres_port s3_port < <(
  python3 -c 'import socket; sockets=[socket.socket() for _ in range(3)]; [item.bind(("127.0.0.1", 0)) for item in sockets]; print(*(item.getsockname()[1] for item in sockets)); [item.close() for item in sockets]'
)
api_base_url="http://127.0.0.1:$api_port"
api_pid=''

export SHOWVAULT_API_PORT="$api_port"
export SHOWVAULT_POSTGRES_TEST_PORT="$postgres_port"
export SHOWVAULT_S3_TEST_PORT="$s3_port"
export SHOWVAULT_VERSION='resilience-matrix'
export POSTGRES_DB='showvault'
export POSTGRES_USER='showvault'
export POSTGRES_PASSWORD='showvault-resilience-local-only'
export AUTH0_DOMAIN='example.auth0.com'
export AUTH0_AUDIENCE='https://api.showvault.app'
export SHOWVAULT_S3_BUCKET='showvault-prototype'
export SHOWVAULT_S3_PREFIX='showvault/resilience/v1'
export AWS_REGION='us-east-1'
export AWS_ACCESS_KEY_ID='showvault-test'
export AWS_SECRET_ACCESS_KEY='showvault-test-secret'
export ASPNETCORE_ENVIRONMENT='Development'
export ASPNETCORE_URLS="$api_base_url"
export ConnectionStrings__Platform="Host=127.0.0.1;Port=$postgres_port;Database=showvault;Username=showvault;Password=$POSTGRES_PASSWORD"
export Auth0__Domain="$AUTH0_DOMAIN"
export Auth0__Audience="$AUTH0_AUDIENCE"
export HostedSync__Provider='S3'
export HostedSync__S3__Bucket="$SHOWVAULT_S3_BUCKET"
export HostedSync__S3__Region="$AWS_REGION"
export HostedSync__S3__ServiceUrl="http://127.0.0.1:$s3_port"
export HostedSync__S3__Prefix="$SHOWVAULT_S3_PREFIX"
export HostedSync__S3__ForcePathStyle='true'
export PersonalBeta__BypassAuthentication='true'
export PersonalBeta__IdentitySubject='showvault-synthetic-resilience'

compose=(
  docker compose
  -p "$compose_project"
  --env-file "$repository_root/infra/.env.prototype.example"
  -f "$repository_root/infra/docker-compose.prototype.yml"
  -f "$repository_root/infra/docker-compose.s3-test.yml"
)

stop_api() {
  if [[ -n "$api_pid" ]] && kill -0 "$api_pid" 2>/dev/null; then
    kill "$api_pid"
    wait "$api_pid" 2>/dev/null || true
  fi
  api_pid=''
}

cleanup() {
  stop_api
  if [[ -n "${app_executable:-}" && -x "$app_executable" ]]; then
    "$app_executable" --showvault-resilience-phase cleanup >/dev/null 2>&1 || true
  fi
  "${compose[@]}" down --volumes --remove-orphans >/dev/null 2>&1 || true
  case "$work_root" in
    /tmp/showvault-resilience.*|/private/tmp/showvault-resilience.*|/var/folders/*/showvault-resilience.*)
      if [[ -f "$ownership_marker" ]]; then
        rm -rf -- "$work_root"
      fi
      ;;
  esac
}
trap cleanup EXIT INT TERM

wait_for_url() {
  local url=$1
  local attempts=${2:-60}
  local index
  for ((index = 0; index < attempts; index++)); do
    if curl --silent --fail --max-time 2 "$url" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  return 1
}

start_api() {
  dotnet run --project "$api_project" --no-build >>"$api_log" 2>&1 &
  api_pid=$!
  if ! wait_for_url "$api_base_url/health/ready" 60; then
    echo "The synthetic API did not become ready." >&2
    exit 70
  fi
}

run_phase() {
  "$app_executable" --showvault-resilience-phase "$1"
}

"${compose[@]}" up -d postgres minio
if ! wait_for_url "http://127.0.0.1:$s3_port/minio/health/live" 60; then
  "${compose[@]}" ps >&2 || true
  "${compose[@]}" logs --no-color minio >&2 || true
  echo "The disposable object store did not become reachable." >&2
  exit 70
fi
"${compose[@]}" run --rm s3-init >/dev/null

dotnet build "$api_project" --no-restore >/dev/null
dotnet run --project "$api_project" --no-build -- --migrate
start_api

cd "$app_directory"
flutter pub get >/dev/null
flutter build macos --release \
  --dart-define="SHOWVAULT_API_BASE_URL=$api_base_url" \
  --dart-define="SHOWVAULT_PERSONAL_BETA_BYPASS_AUTH=true" \
  --dart-define="SHOWVAULT_SYNTHETIC_FIXTURE_HOME=$fixture_name" \
  --dart-define="SHOWVAULT_SYNTHETIC_SYNC_CHUNK_BYTES=8" \
  --dart-define="SHOWVAULT_RESILIENCE_HARNESS=true"

build_product="$app_directory/build/macos/Build/Products/Release/ShowVault.app"
if [[ ! -d "$build_product" ]]; then
  echo "The installed resilience application was not produced." >&2
  exit 70
fi
mkdir -p "$output_directory"
ditto "$build_product" "$output_directory/ShowVault.app"
bundle_executable=$(
  /usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' \
    "$output_directory/ShowVault.app/Contents/Info.plist"
)
if [[ -z "$bundle_executable" || "$bundle_executable" == */* ]]; then
  echo "The installed resilience executable identity is unsafe." >&2
  exit 70
fi
app_executable="$output_directory/ShowVault.app/Contents/MacOS/$bundle_executable"
if [[ ! -x "$app_executable" ]]; then
  echo "The installed resilience executable is unavailable." >&2
  exit 70
fi

run_phase prepare
stop_api
run_phase api-unavailable
start_api
run_phase interrupt-upload
run_phase resume-upload

"${compose[@]}" stop minio >/dev/null
run_phase storage-unavailable
"${compose[@]}" start minio >/dev/null
wait_for_url "http://127.0.0.1:$s3_port/minio/health/live" 60
wait_for_url "$api_base_url/health/ready" 60
run_phase storage-resume
run_phase failure-matrix
final_output=$(run_phase finalize)
echo "$final_output"
report_base64=$(echo "$final_output" | sed -n 's/^SHOWVAULT_RESILIENCE_REPORT://p')
if [[ -z "$report_base64" ]]; then
  echo "The resilience evidence report was not exported." >&2
  exit 70
fi
echo "$report_base64" | /usr/bin/base64 -D >"$output_directory/resilience-report.json"
if grep -E '/Users/|/private/|/var/folders/|/tmp/' \
    "$output_directory/resilience-report.json" >/dev/null; then
  echo "The resilience evidence report is missing or contains a local path." >&2
  exit 70
fi
ditto -c -k --sequesterRsrc --keepParent \
  "$output_directory/ShowVault.app" \
  "$output_directory/ShowVault-macos.zip"
(
  cd "$output_directory"
  shasum -a 256 ShowVault-macos.zip resilience-report.json > SHA256SUMS
)

echo "Created installed synthetic resilience evidence in $output_directory"
echo "Executed phases: API unavailable, interrupted/resumed upload, storage unavailable/resumed, tamper, conflict, incomplete object, restore failures"
echo "Host reboot: not executed"
echo "Production provider: not executed"
