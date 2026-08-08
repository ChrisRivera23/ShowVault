#!/bin/zsh
set -euo pipefail

readonly label="com.showvault.venue-agent"
readonly service_user="_showvault"
readonly install_root="/Library/Application Support/ShowVault"
readonly agent_dir="$install_root/Agent"
readonly config_dir="$install_root/Configuration"
readonly state_dir="$install_root/State"
readonly package_dir="$install_root/Packages"
readonly secrets_dir="$install_root/Secrets"
readonly logs_dir="/Library/Logs/ShowVault"
readonly plist_path="/Library/LaunchDaemons/$label.plist"
readonly keychain_path="$secrets_dir/venue-agent.keychain-db"
readonly password_path="$secrets_dir/keychain-password"

usage() {
  print -u2 "usage: sudo ./install.sh --payload DIR --config FILE --enrollment-code CODE"
  exit 64
}

payload=""
config=""
enrollment_code=""
while (( $# > 0 )); do
  case "$1" in
    --payload) payload="${2:-}"; shift 2 ;;
    --config) config="${2:-}"; shift 2 ;;
    --enrollment-code) enrollment_code="${2:-}"; shift 2 ;;
    *) usage ;;
  esac
done

[[ "$(uname -s)" == "Darwin" ]] || { print -u2 "macOS is required"; exit 1; }
(( EUID == 0 )) || { print -u2 "run this installer with sudo"; exit 1; }
[[ -d "$payload" && -x "$payload/ShowVault.Agent" ]] || usage
[[ -f "$config" && -n "$enrollment_code" ]] || usage

if ! /usr/bin/dscl . -read "/Users/$service_user" >/dev/null 2>&1; then
  uid=399
  while /usr/bin/dscl . -search /Users UniqueID "$uid" | /usr/bin/grep -q .; do
    (( uid-- ))
    (( uid >= 350 )) || { print -u2 "no service UID available in 350-399"; exit 1; }
  done
  /usr/bin/dscl . -create "/Groups/$service_user"
  /usr/bin/dscl . -create "/Groups/$service_user" PrimaryGroupID "$uid"
  /usr/bin/dscl . -create "/Users/$service_user"
  /usr/bin/dscl . -create "/Users/$service_user" UniqueID "$uid"
  /usr/bin/dscl . -create "/Users/$service_user" PrimaryGroupID "$uid"
  /usr/bin/dscl . -create "/Users/$service_user" UserShell /usr/bin/false
  /usr/bin/dscl . -create "/Users/$service_user" NFSHomeDirectory "$state_dir"
  /usr/bin/dscl . -create "/Users/$service_user" IsHidden 1
fi

/bin/launchctl bootout system "$plist_path" >/dev/null 2>&1 || true
/usr/bin/install -d -o root -g wheel -m 0755 "$install_root" "$agent_dir" "$config_dir"
/usr/bin/install -d -o "$service_user" -g "$service_user" -m 0750 "$state_dir" "$package_dir" "$secrets_dir" "$logs_dir"
/usr/bin/ditto "$payload" "$agent_dir"
/usr/sbin/chown -R root:wheel "$agent_dir"
/bin/chmod -R go-w "$agent_dir"
/usr/bin/install -o root -g "$service_user" -m 0640 "$config" "$config_dir/appsettings.json"

if [[ ! -f "$password_path" ]]; then
  /usr/bin/openssl rand -base64 48 > "$password_path"
  /usr/sbin/chown "$service_user:$service_user" "$password_path"
  /bin/chmod 0600 "$password_path"
fi

if [[ ! -f "$keychain_path" ]]; then
  keychain_password="$(<"$password_path")"
  /usr/bin/sudo -u "$service_user" HOME="$state_dir" \
    /usr/bin/security create-keychain -p "$keychain_password" "$keychain_path"
  /usr/bin/sudo -u "$service_user" HOME="$state_dir" \
    /usr/bin/security set-keychain-settings -lut 21600 "$keychain_path"
  /bin/chmod 0600 "$keychain_path"
fi

agent_environment=(
  "HOME=$state_dir"
  "Agent__EnrollmentCode=$enrollment_code"
  "Agent__EnrollOnly=true"
  "Agent__DataDirectory=$state_dir"
  "Agent__PackageDirectory=$package_dir"
  "Agent__MacOsKeychainPath=$keychain_path"
  "Agent__MacOsKeychainPasswordFile=$password_path"
)
/usr/bin/sudo -u "$service_user" /usr/bin/env "${agent_environment[@]}" \
  "$agent_dir/ShowVault.Agent" --contentRoot "$config_dir"
unset enrollment_code

/usr/bin/install -o root -g wheel -m 0644 \
  "${0:A:h}/com.showvault.venue-agent.plist" "$plist_path"
/usr/bin/plutil -lint "$plist_path" >/dev/null
/bin/launchctl bootstrap system "$plist_path"
/bin/launchctl enable "system/$label"
/bin/launchctl kickstart "system/$label"

print "ShowVault Venue Agent installed and enrolled."
print "Run ${0:A:h}/validate.sh to validate restart and Keychain access."
