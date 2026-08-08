#!/bin/zsh
set -euo pipefail

readonly label="com.showvault.venue-agent"
readonly service_user="_showvault"
readonly install_root="/Library/Application Support/ShowVault"
readonly secrets_dir="$install_root/Secrets"
readonly keychain_path="$secrets_dir/venue-agent.keychain-db"
readonly password_path="$secrets_dir/keychain-password"

(( EUID == 0 )) || { print -u2 "run validation with sudo"; exit 1; }
/bin/launchctl print "system/$label" >/dev/null

[[ "$(/usr/bin/stat -f '%Su:%Sg:%Lp' "$password_path")" == "$service_user:$service_user:600" ]] || {
  print -u2 "Keychain password file permissions are invalid"
  exit 1
}

keychain_password="$(<"$password_path")"
/usr/bin/sudo -u "$service_user" HOME="$install_root/State" \
  /usr/bin/security unlock-keychain -p "$keychain_password" "$keychain_path"
/usr/bin/sudo -u "$service_user" HOME="$install_root/State" \
  /usr/bin/security find-generic-password \
    -s com.showvault.venue-agent -a identity "$keychain_path" >/dev/null

/bin/launchctl kickstart -k "system/$label"
/bin/sleep 3
/bin/launchctl print "system/$label" | /usr/bin/grep -q 'state = running'
print "LaunchDaemon restart and dedicated Keychain validation passed."
