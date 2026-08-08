#!/bin/zsh
set -euo pipefail

readonly script_dir="${0:A:h}"
readonly agent_project="${script_dir:h:h}/src/ShowVault.Agent/ShowVault.Agent.csproj"
readonly runtime="${1:-osx-arm64}"
readonly output="${2:-${script_dir}/out/$runtime}"

case "$runtime" in
  osx-arm64|osx-x64) ;;
  *) print -u2 "runtime must be osx-arm64 or osx-x64"; exit 64 ;;
esac

/usr/bin/install -d "$output/payload"
dotnet publish "$agent_project" \
  --configuration Release \
  --runtime "$runtime" \
  --self-contained true \
  -p:PublishSingleFile=true \
  --output "$output/payload"
/usr/bin/install -m 0755 "$script_dir/install.sh" "$output/install.sh"
/usr/bin/install -m 0755 "$script_dir/validate.sh" "$output/validate.sh"
/usr/bin/install -m 0644 "$script_dir/com.showvault.venue-agent.plist" \
  "$output/com.showvault.venue-agent.plist"
/usr/bin/plutil -lint "$output/com.showvault.venue-agent.plist" >/dev/null
print "Package staged at $output"
