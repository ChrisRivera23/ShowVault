# Milestone-1 local runbook

1. Run `flutter analyze` and the complete Flutter test suite.
2. Run contracts, platform, Agent, and API tests.
3. Run the EF pending-model check against the generated migration snapshot.
4. Inspect the diff for paths, broad enumeration, content/network access,
   Keychain calls, bypass gaps, tenant gaps, and recovery-state overclaims.

For a controlled local no-login exercise, compile the Flutter app with the
explicit bypass flag and a loopback HTTP API origin. Run the API in Development
with the explicit server flag and an existing bounded identity. All five guards
are required. This is test scaffolding, not distribution or venue authorization.
