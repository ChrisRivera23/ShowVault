# Stripe sandbox account provisioning — 2026-08-13

## Outcome

The explicitly authorized, account-backed provisioning step completed the
non-endpoint sandbox resources in the authenticated `ShowVault Pro sandbox`.
The Dashboard continuously identified the account as a sandbox and stated that
changes do not affect real customers or payments. No live-mode object,
customer, Checkout Session, Portal Session, payment, refund, dispute,
deployment, or production change was created.

End-to-end operational proof remains fail-closed. No public HTTPS ShowVault
webhook endpoint is authorized or available, so no event destination or
endpoint secret was created. The restricted key token was not copied, exposed,
logged, placed in Git, or injected into a runtime.

## Catalog

The active sandbox catalog contains exactly the two authorized fixture
products and their default Prices:

- recurring product `ShowVault Standard — sandbox proof`:
  - Product `prod_V4CaobVDHSceZF`;
  - Price `price_1U44BfAHdGAkls09pu4x1pK6`;
  - USD 1.00 per month;
- one-time product `ShowVault perpetual license — sandbox proof`:
  - Product `prod_V4CbN0AgBIi0bD`;
  - Price `price_1U44BrAHdGAkls09NHsC6sWt`;
  - USD 1.00 one time.

Both products are active. No tax automation, trial, coupon, promotion code,
adjustable quantity, or customer-supplied Price was configured.

## Customer portal

The persisted default sandbox portal configuration is
`bpc_1U44DXAHdGAkls09P5H7ibSX`. It permits:

- invoice history;
- payment-method updates; and
- subscription cancellation at the end of the billing period.

It does not permit customer-information updates, immediate cancellation, plan
switching, quantity changes, cancellation-reason collection, or retention
coupons. The no-code portal test link was not activated; the ShowVault adapter
creates short-lived sessions for an already-bound Customer.

## Restricted sandbox key

Restricted key `ShowVault sandbox runtime — 2026-08-13` was created in the
sandbox. Its token remains only behind Stripe's authenticated Dashboard and was
not copied or revealed. The selected permissions are:

- write: Checkout Sessions and Customer Portal;
- read: Customers, Subscriptions, Invoices, Payment Intents, Charges and
  Refunds, and Payment Disputes;
- none: every other resource, including refund/dispute/payout/balance writes,
  Products, Prices, webhook management, Stripe CLI, and live-mode access.

`Invoices` read covers the adapter's invoice and invoice-payment retrieval
surface. The key must remain unused until an approved encrypted runtime secret
store is selected; it must never be pasted into configuration, evidence, shell
history, or chat.

## Card-only enforcement repair

The Dashboard's default payment-method configuration currently reports 18
enabled methods and does not expose customization while account activation is
incomplete. Rather than mutate 17 account-wide methods or overclaim the
fixture, the adapter now sends exactly
`payment_method_types[0]=card` when creating Checkout Sessions. The focused
request test asserts that pin. This makes the ShowVault Checkout request
card-only independently of the broader Dashboard default.

## Local validation

- focused `StripeBillingProviderTests`: 4 passed;
- complete API test project: 50 passed;
- .NET whitespace verification: passed;
- `git diff --check`: passed; and
- the changed documentation contains no Stripe key or endpoint-secret value.

## Remaining gate

Before account-backed Checkout or webhook proof:

1. select and authorize an encrypted non-Git secret store for the restricted
   key;
2. provide and authorize a reachable public HTTPS origin whose endpoint is
   `/api/v1/provider-webhooks/stripe`, or separately authorize an authenticated
   Stripe CLI installation/forwarding session;
3. register only the 14 event types in the operational preflight using Stripe
   API version `2026-07-29.dahlia`;
4. inject the resulting webhook secret outside Git; and
5. run the synthetic lifecycle while leaving production and real data alone.

Until those inputs exist, checked-in billing configuration remains disabled.
