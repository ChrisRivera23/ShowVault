## Summary

Complete ShowVault's smallest internal Support administration slice with a
separate staff identity plane, explicit organization grants, one minimized
audited read-only overview API, and an isolated disabled-by-default Support
BFF.

Checked-in Support configuration remains disabled. This pull request does not
configure identity, provision staff, apply the migration, deploy, or enable
production behavior.

## What changed

- add the closed `SupportReader` assignment and organization-grant lifecycle;
- add append-only minimized Support-access evidence and one restrictive schema
  migration;
- add the distinct exact-issuer/audience `ShowVault-Support` bearer boundary,
  fresh scope/MFA authorization, and a bounded direct-peer limiter;
- add strict `POST /api/v1/support/organization-overview` with one joined
  grant/organization decision, a fixed minimized projection, and serializable
  audit-before-disclosure;
- add a separate Razor Pages Support BFF with exact-origin enforcement, Code +
  PKCE, isolated cookies and sessions, antiforgery, strict typed API response
  validation, generic bounded failures, and no result persistence;
- normalize the portal/API scheme, host, and effective port comparison so
  implicit and explicit default-port aliases fail closed;
- suppress handled-exception diagnostics while preserving fail-safe logging for
  unhandled failures; and
- reconcile the roadmap and retain the complete staged implementation/review
  evidence.

## Security and privacy boundary

Customer authentication, organization membership, customer roles, personal
beta, portal cookies, route values, JWT roles, and email domains never grant
Support authority. Access requires the exact Support issuer and distinct
audience, stable subject, exact read scope, fresh MFA, active server-owned
`SupportReader`, and an explicit active organization grant.

The response contains only the frozen aggregate overview. It excludes identity
subjects, member and invitation identifiers, provider IDs or payloads, payment
details, credentials, tokens, correlation IDs, filesystem paths, filenames,
manifests, backup or restore content, and signed URLs. There is no organization
directory, search, list, export, impersonation, write action, provider access,
or staff-provisioning surface.

## Validation

The exact reviewed local product, repair, and closeout chain passed:

- focused handled-exception proof: 1 passed;
- focused normalized-origin configuration proof: 5 passed;
- Support BFF Release tests: 17 passed;
- account portal Release tests: 15 passed;
- platform tests: 40 passed;
- API tests: 170 passed;
- Support BFF, account portal, and API Release builds: zero warnings/errors;
- formatting for all eight relevant source/test projects and the EF pending-
  model gate: clean; and
- exact ancestry, tree, path, binary-diff, disabled-configuration, route,
  privacy, and secret inventories: clean.

At exact published head
`cc27f9ef5fa5c8028ee9d0332fe03d40744b0a81`, automatic push run
`31767741253` and pull-request run `31767770175` each passed their API and
Flutter jobs without dispatch or rerun. Both API jobs included the Support BFF
Release restore, test, and build sequence. The subsequent origin-isolation
repair passed independent local review; its exact publication must trigger
fresh automatic push and pull-request CI at the new head, and both API and
Flutter jobs must pass before any ready transition.

## Operational boundaries

This remains local synthetic implementation and evidence. Durable encrypted
non-Development sessions, a separate Support identity application/audience,
staff/grant provisioning, database migration application, DNS/TLS/deployment,
monitoring, audit retention, revocation operations, production safety, and
real-person onboarding remain separately gated.

Keep this pull request as a draft. Publication alone must not authorize a ready
transition, merge, workflow rerun, provider or identity mutation, deployment,
release, production enablement, native operation, or cleanup.
