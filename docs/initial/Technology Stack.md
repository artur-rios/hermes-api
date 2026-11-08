# Technology Stack — Hermes API

Hermes follows the conventions of the [Heimdall API](https://github.com/artur-rios/heimdall-api) and
the Fortuna API where they apply, but it is much smaller: one operation does not justify their full
layering.

Exact versions are **not** pinned here. They are pinned once, in the formal Technology Stack
Document, which every other document links to instead of restating.

## Platform & Language

- **.NET 10** with C#.
- `ArturRios.*` packages shared with my other APIs.

## Application Type

An **HTTP API** built with ASP.NET Core, and internal only.

The solution is **one web project and its test project**. There is no Domain / Application /
Infrastructure / Presentation split and no CQRS.

## Data Storage

**None.** There is no database and no file storage.

- Rate limit counters are held **in memory**. A deploy or restart resets them. That is accepted,
  because only the owner triggers deploys, and it only stays correct with one container.
- Message content is never stored. Hermes sends each message immediately and keeps nothing.

## Data Access

**None**, because there is no persistence.

## Authentication

**A per-client API key** sent with each request. Each client can have two active keys, so a key can
be replaced without downtime.

Clients and their rules live in the environment's `hermes-api.env` on the host, which holds only
key hashes. Adding or removing a client means editing that file and redeploying. There is no admin
API.

A browser never calls Hermes and never holds a key.

## Testing

- **xUnit**, with **ArturRios.Util.Test** for the category attributes and the functional base class.
- **Moq** for doubles and **Bogus** for test data.
- Two categories, run separately: **unit** and **functional**.
- Tests are named and written **Given / When / Then**.
- **Mailgun is always faked.** No test reaches the network.
- No Testcontainers, because there is no database.
- The personal page has no test framework today. It gains **Vitest**, for its `/api/contact` route
  only, because that route holds the abuse checks on the public side.

## External Dependencies

| Dependency | Used for |
| --- | --- |
| **Mailgun** | Delivery, through `ArturRios.Messaging`. One shared sending domain, the one Heimdall already uses, with a separate sender address per client. |

`ArturRios.Messaging` has to change first. `IEmailService` only takes the recipient, subject and
body, and the sender is the process-wide `MAILGUN_FROM`. Hermes needs a sender per client and a
reply-to per message.

## Integrations

**Personal page (Next.js).** A route handler holds the Hermes key on the server and calls Hermes.
The site's public protection is a per-source limit at Traefik and **Cloudflare Turnstile**, verified
on the server. Its stack passes it no runtime environment today, so it needs an env file for the key
and the Turnstile secret.

**Heimdall (.NET 10).** All of Heimdall's email goes through one `IEmailService` registration. A
Hermes implementation of that interface replaces `MailgunEmailService` there, so its senders and
message text stay unchanged. Its check that refuses to start in production without Mailgun becomes a
check for the Hermes settings.

## Deployment

Docker, deployed by **yggdrasil** to the local, development, homologation and production
environments, each with its own env file and credentials.

- A `catalog.yaml` entry with no `host`.
- A `stacks/hermes-api.proxy.yml` that joins the edge and telemetry networks, with **no Traefik
  labels and no published port**.
- Callers use `http://hermes-api.<environment>:8080/` over the edge network, the way fortuna-api
  reaches heimdall-api. Other containers share that network, which is why the API key is still
  required.
- `/healthcheck`, and metrics on port 9464.
- Outside production, Hermes logs instead of sending when Mailgun is not configured. In production
  it refuses to start.
