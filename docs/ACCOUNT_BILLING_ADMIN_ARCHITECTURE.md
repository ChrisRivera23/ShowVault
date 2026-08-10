# Account, billing, licensing, and administration architecture

## Product surfaces

ShowVault should use three related surfaces rather than a second customer desktop application:

1. **ShowVault desktop** — scan, backup, verify, restore, and local settings.
2. **ShowVault customer account portal** — signup, organization and user management, billing, invoices, plan changes, licenses, storage usage, and device management.
3. **ShowVault Admin** — a private web console for ShowVault staff to support customers and inspect operational status.

The customer portal and internal Admin console may begin as two role-gated areas of one web application. Split them into separate deployments only when security, staffing, or scale makes that useful.

## Beta behavior

The attended personal beta may omit login entirely. Its bypass is test scaffolding with all of these gates:

- an explicit no-login build option;
- a loopback HTTP API URL;
- an API running in the Development environment;
- an explicit server-side bypass flag and existing test identity;
- a request originating from loopback.

Normal and production builds continue to require authentication. The beta must not contain a customer credential, password, enrollment code, client secret, or production bypass.

## Recommended finished customer flow

1. Install and open ShowVault.
2. Scan the computer locally without requiring an account.
3. Show supported detected systems.
4. When the customer selects **Back up**, ask them to sign in or create a ShowVault account if they are not already signed in.
5. Confirm that the account has the required paid license and an eligible subscription tier.
6. Stream the authorized backup directly to cloud storage.

This keeps the first-run experience simple while requiring identity and entitlement before ShowVault consumes paid cloud resources or exposes private backups.

The login should be branded as ShowVault. Auth0 may remain the underlying identity provider, using a ShowVault custom domain and Universal Login, so customers do not need a separate pre-existing Auth0 account. Their email/password or passkey is their ShowVault account credential.

## Password boundary

ShowVault staff and the ShowVault Admin console must never display, retrieve, export, transmit, or store a customer's plaintext password.

The identity provider owns password hashing, attack protection, MFA/passkeys, recovery, and password-reset flows. Admin capabilities should be limited to actions such as:

- invite a user;
- resend an invitation;
- trigger a password-reset email;
- require a new login or MFA;
- suspend or reactivate access;
- change organization roles;
- inspect security and administrative audit events.

## Commercial model

Represent the one-time license and monthly subscription as separate entitlements:

- **ShowVault license** — one-time paid right associated with the purchasing customer organization.
- **Service subscription** — recurring plan that controls active cloud service, storage allowance, device/venue limits, retention, and support level.

Stripe Products and Prices can model a one-time price and separate recurring monthly prices. Do not infer access from a browser redirect or email address. Signed Stripe webhook events update ShowVault's own billing projection, and the authorization layer derives entitlements from that server-side state.

Suggested initial plan dimensions:

- cloud storage quota;
- number of protected computers/devices;
- number of venues;
- backup retention period;
- verification frequency;
- support level.

Exact prices and tier limits remain product decisions. Preserve stable internal plan codes so pricing can change without rewriting historical customer records.

## Authoritative records

ShowVault's database should link provider records by stable IDs, never by mutable email address alone.

Recommended records:

- `CustomerOrganization`
  - ShowVault organization ID
  - display name
  - lifecycle status
  - Stripe customer ID
  - Auth0 organization ID when Organizations is adopted
- `UserMembership`
  - identity-provider subject
  - organization ID
  - role
  - invited/active/suspended status
- `CommercialLicense`
  - organization ID
  - license type
  - Stripe payment/checkout reference
  - paid/refunded/revoked status
  - purchased and effective timestamps
- `ServiceSubscription`
  - organization ID
  - Stripe subscription ID
  - internal plan code
  - trialing/active/past-due/paused/canceled status
  - current period and grace-period timestamps
- `EntitlementSnapshot`
  - effective feature limits calculated from license and subscription state
- `Installation`
  - opaque installation ID
  - organization assignment
  - platform/app version
  - active/revoked state
  - no general machine inventory
- `AuditEvent`
  - actor, action, target, timestamp, correlation ID, and bounded outcome
  - no credentials, tokens, absolute paths, or backup contents

## Internal ShowVault Admin console

The private Admin web console should use strong staff authentication, MFA, role-based access, and an immutable audit trail. It should show:

- customer organizations and contacts;
- invited, active, suspended, and removed users;
- license payment status;
- subscription plan and status;
- storage usage and entitlement limits;
- active/revoked installations;
- backup/verification operational status;
- support notes and audited administrative actions.

It must not show passwords, authentication tokens, payment-card data, exact customer filesystem paths, or backup file contents. Use Stripe Dashboard links for payment details and the identity-provider dashboard or narrowly scoped management APIs for identity support actions.

For the earliest commercial prototype, Auth0 Dashboard plus Stripe Dashboard can serve as the provider-level administration tools while the first ShowVault Admin page aggregates organization, entitlement, installation, and backup status. This avoids rebuilding mature identity and billing controls prematurely.

## Customer self-service

The customer portal should let organization owners:

- invite or remove team members;
- assign limited roles;
- view the paid license;
- view plan, quota, renewal, and billing status;
- open a short-lived Stripe Customer Portal session for payment methods, invoices, cancellation, and plan changes;
- view and revoke their own installations;
- see backup and verification status.

The desktop Settings section may link to this portal and show the current account and plan. Authentication can be deferred until the first cloud operation, but access to cloud backups, billing, or organization data must always require a valid account.

## Security and lifecycle rules

- Verify Stripe webhook signatures and make handlers idempotent.
- Treat webhook-driven server state as billing authority; never trust desktop claims about payment.
- Define explicit grace-period behavior for failed monthly payments.
- A refund, chargeback, cancellation, or suspension must not silently delete customer backups; retention and export behavior require a documented policy.
- Separate authentication, organization membership, commercial license, subscription state, and feature entitlement.
- Require step-up confirmation and audit logging for destructive or high-impact admin actions.
- Keep production, staging, and development identity/billing environments separate.
- Store provider secrets only in server-side secret management, never in the desktop app.

## Implementation order

1. Finish the login-free personal beta scan/backup/verify/restore loop.
2. Define tier and entitlement rules without selecting final prices.
3. Add Stripe sandbox products: one-time license plus monthly tier prices.
4. Add billing records, signed webhook processing, and entitlement evaluation.
5. Add the customer signup/account portal and branded ShowVault authentication.
6. Require authentication and entitlement at the first cloud backup action.
7. Add the smallest internal Admin page for organization, user, license, subscription, and usage status.
8. Add customer self-service and audited support actions.

Do not postpone authentication until after public distribution. Deferring it is appropriate for the controlled beta only.
