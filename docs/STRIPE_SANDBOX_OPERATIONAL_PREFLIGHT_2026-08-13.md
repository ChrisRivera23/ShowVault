# Stripe sandbox operational preflight — 2026-08-13

## Outcome

ShowVault now has a direct, disabled-by-default Stripe sandbox adapter behind
the milestone-6 provider seam. Deterministic HTTP fixtures cover Checkout and
current-state reconciliation without a Stripe SDK, CLI, account, credential,
or network call. Account-backed proof remains pending Stripe authentication,
the sandbox-only catalog decision below, and a reachable HTTPS webhook URL.

The adapter pins `Stripe-Version: 2026-07-29.dahlia`. It uses the current
Dahlia shapes: subscription-item billing periods,
`invoice.parent.subscription_details.subscription`, invoice payments, and
invoice-line `pricing.price_details.price`. It also sets Checkout expiry to the
configured 30–60 minute window; Stripe rejects shorter expirations.

## Fail-closed runtime contract

Provider billing remains unavailable unless every item is present and valid:

- `Billing__Enabled=true`;
- `Billing__Environment=Sandbox`;
- `Billing__ProviderApiVersion=2026-07-29.dahlia`;
- an HTTPS origin with only `/` as its path in `Billing__ReturnOrigin`;
- `Billing__CheckoutLifetimeMinutes` from 30 through 60;
- a restricted sandbox key beginning with `rk_test_` in
  `Billing__Stripe__SecretKey`;
- distinct sandbox `price_` identifiers for the recurring plan and one-time
  license; and
- all internal offering codes and policy version.

Webhook receipt separately remains unavailable unless
`Billing__Webhook__EndpointSecrets__0` contains the sandbox endpoint's
`whsec_` value. At most two endpoint secrets are accepted so a rotation can
overlap. Secrets and keys must be injected from an encrypted local/runtime
secret store, never checked into configuration, logs, evidence, shell history,
or chat.

Checkout sends exactly two quantity-one Price references in subscription mode:
the recurring Price and the one-time license Price. It explicitly limits the
session to the `card` payment-method type, fixes success/cancel URLs, a 30–60
minute expiry, an idempotency key, and bounded opaque
organization/offering/attempt metadata. It sends no customer email or amount.
Returned URLs must use Stripe's exact hosted domains. Portal creation sends
only the already-bound Customer ID and fixed return URL.

## Proposed sandbox-only catalog decision

The smallest operational fixture is:

- product: `ShowVault Standard — sandbox proof`;
- recurring Price: USD 1.00 monthly, quantity fixed at one;
- one-time Price: USD 1.00, named `ShowVault perpetual license — sandbox proof`;
- card payments only, no tax automation, trial, coupon, promotion code,
  adjustable quantity, or customer-supplied Price;
- portal: payment-method update, invoice history, and cancellation at period
  end; no plan switch or quantity change; and
- policy version: `stripe-sandbox-proof-2026-08-13`.

These amounts and settings are proposals, not product pricing. No catalog or
portal resource may be created until the Product Owner confirms this fixture or
supplies replacements.

## Restricted runtime key

Use a sandbox restricted key (`rk_test_`) where the Dashboard permits it. The
runtime requires only the permissions needed to create/read Checkout Sessions
and Billing Portal Sessions and to read Customers, Subscriptions, Invoices,
Invoice Payments, PaymentIntents, Charges, and Disputes. Products and Prices are
configured through the Dashboard and referenced by allowlisted IDs; the
runtime does not create, update, or delete them. Prove the final restricted
permission set with the operational lifecycle, then deny every unused write or
financial-action permission. Do not grant refunds, disputes, payouts, balance,
or live-mode access.

## Event destination

Register only these sandbox event types against the public HTTPS endpoint
`/api/v1/provider-webhooks/stripe`:

- `checkout.session.completed`
- `checkout.session.async_payment_succeeded`
- `checkout.session.async_payment_failed`
- `customer.subscription.created`
- `customer.subscription.updated`
- `customer.subscription.deleted`
- `customer.subscription.paused`
- `customer.subscription.resumed`
- `invoice.paid`
- `invoice.payment_failed`
- `charge.refunded`
- `charge.dispute.created`
- `charge.dispute.updated`
- `charge.dispute.closed`

The endpoint verifies the signature against exact raw bytes, rejects the wrong
environment, stores no raw payload, deduplicates event IDs, and retrieves
current Stripe state rather than trusting delivery order or event contents.
The event destination must use the same Dahlia API version.

No public ShowVault API URL is currently authorized or available. A Dashboard
event destination therefore cannot yet complete end-to-end delivery. A later
deployment or an explicitly approved, authenticated Stripe CLI forwarding
session is required for that portion of proof.

## Operational proof sequence

After account authentication and the fixture decision:

1. Confirm a true Stripe sandbox/test context and record only non-secret object
   IDs needed by configuration.
2. Create the two sandbox-only Products/Prices and configure the sandbox portal.
3. Create the least-privilege restricted sandbox key and store it outside Git.
4. Configure the disabled local seam, verify the allowlisted offering appears,
   and create one Checkout Session using synthetic organization data only.
5. Complete Checkout with Stripe test data; prove the redirect grants no access.
6. Deliver signed events, reconcile current state, and prove the normalized
   subscription/license projection grants access only after reconciliation.
7. Open a short-lived portal session; cancel at period end and prove the current
   subscription state is re-read.
8. Exercise failed payment, full/partial refund, and dispute sandbox paths and
   prove fail-closed attention/denial without deleting recovery data.
9. Rotate the webhook secret through the bounded two-secret overlap, then remove
   the old value.
10. Disable the integration and retain only redacted IDs, test counts, response
    statuses, state transitions, and hashes as evidence.

No live-mode object, real customer/payment data, real charge/refund, deployment,
or production enablement is part of this sandbox slice.

## Current blockers

- Neither the in-app browser nor Chrome has an authenticated Stripe session.
- The Product Owner has not confirmed the proposed sandbox-only fixture.
- There is no public HTTPS ShowVault webhook endpoint, installed Stripe CLI, or
  configured provider credential.

Until those inputs exist, the checked-in configuration remains explicitly
disabled and contains no Stripe key, secret, account data, or object ID.
