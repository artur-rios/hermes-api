# Brainstorm

Notes for a small API that every project of mine can use to send email.

> Drafted with Claude from a conversation about the personal page's contact form,
> and from reading heimdall-api, dotnet-messaging and yggdrasil. Anything marked
> *(proposed)* is a suggestion that has not been decided yet.

## Why

The contact form on my personal page only opens the visitor's own mail client
through `mailto:`. I want people to be able to send me a message directly from
the site.

Heimdall already sends email, through `ArturRios.Messaging` and Mailgun, but
all of that is wired into Heimdall. Every new project would need its own
credentials, its own sender code and its own limits. I want one place for it.

What worries me is an open route that anyone can hit with a large number of
requests. The requests themselves are not the expensive part. Every accepted
request is an email: it can flood my inbox, burn the Mailgun quota, damage the
domain's sending reputation, or be used to send spam to other people. Even if
every other defense fails, the damage has to stay small and fixed.

## What it is

An internal HTTP service. My other backends ask it to send an email, and it
delivers the email through Mailgun.

- No public route. No host name, no Traefik router. It is only reachable on the
  Docker network, at `hermes-api.<environment>:8080`, the same way fortuna-api
  reaches heimdall-api.
- Every project that calls it is a named client with its own API key. The edge
  network is shared with other containers, so being on it is not enough.
- A browser never calls it and never holds a key. Anything public belongs to
  the project that owns the page.

## What I want from it

- One operation: send an email with a recipient, a subject, a plain text body
  and an optional reply-to.
- A policy per client, kept on the server: the key, the sender, which recipients
  it may send to, and its limits per minute and per day. The caller cannot
  choose the sender.
- Fixed recipients where that is all a client needs. The personal page can only
  ever send to me, so even a leaked key cannot be used to email strangers.
- Limits per client, so abuse of the contact form cannot use up the quota that
  Heimdall's password resets depend on.
- Clear answers: accepted, invalid, over the limit (429 with `Retry-After`), or
  the provider failed. The caller decides what to do with a failure. Heimdall
  hides it on purpose, the contact form should tell the visitor.
- An alert when a client reaches its daily limit.
- Logs that never hold a subject or a body, and that identify a recipient only
  by the pseudonymous reference `ArturRios.Messaging` already uses.
- `/healthcheck` and metrics on port 9464, like the other APIs.
- Outside production, log instead of sending when Mailgun is not configured,
  like Heimdall. In production, refuse to start.

## The first client: the personal page

This work lives in personal-page and yggdrasil, not in this repository, but it
is the reason this exists. The public route stays small and in front:

1. `POST /api/contact` as a Next.js route handler. The site already runs its
   own server, so it can hold the key.
2. A per-source limit at Traefik on that path, in
   `stacks/personal-page.proxy.yml`, as heimdall-api does for its credential
   routes. A refused request never reaches the site.
3. A size cap, the same validation as the form, and a honeypot field.
4. Cloudflare Turnstile, verified on the server.
5. A call to Hermes with the site's key. Hermes enforces the recipient and the
   daily limit, whatever happened before it.

The form needs a new field for the visitor's email, which becomes the reply-to.
Today their mail client supplies it. If Hermes fails or the limit is reached,
the form shows my address instead. Never send a confirmation to the address the
visitor typed.

The site's stack passes it no runtime environment today, so it needs an env
file for the Hermes key and the Turnstile secret.

## What I do not want

- A public endpoint, or anything that puts a key in a browser.
- Newsletters, bulk sending or mailing lists.
- HTML templates, attachments or open tracking. Plain text only.
- Receiving email.
- Keeping message bodies anywhere.
- A user interface.

## Things I am unsure about

> *2026-11-08 — settled.* The personal page **and** Heimdall both integrate in the
> first version. The other *(proposed)* items below were accepted as written, and
> mail goes out from the Mailgun domain Heimdall already uses. The
> [Technology Stack](Technology%20Stack.md) and [Business Rules](Business%20Rules.md)
> record the outcome. The rest of this stays as it was written.

- **Should Heimdall move to it?** It works today, in process. Moving adds a
  network hop and a dependency on its most sensitive flows, and its password
  recovery is carefully built around the Mailgun round trip. In exchange there
  would be one Mailgun configuration. *(proposed: personal page first, decide
  about Heimdall later.)*
- **Where the counters live.** In memory is simple, but a deploy resets the
  daily count. It only stays correct with one container. Each environment can
  have its own PostgreSQL if that stops being good enough. *(proposed: memory
  first.)*
- **Send now or queue and retry.** A queue survives a Mailgun outage but needs
  storage, and then message bodies are being stored. *(proposed: send
  synchronously and let the caller hear the failure.)*
- **Where client keys and policies are kept.** *(proposed: the environment's
  `hermes-api.env` on the host, with only key hashes in it.)*
- **Which domain sends.** Is there one Mailgun domain for everything, or does
  `artur-rios.tech` need its own, with its own SPF and DKIM?
- **Static sites.** The blog is Hugo and has no server to hold a key. If it ever
  needs a form, it would need a public endpoint designed for that. Not now.
- **The name.** Hermes, the messenger, to match Heimdall and the others. It is a
  working name.

## How

.NET 10. It is what Heimdall uses, `ArturRios.Messaging` already talks to
Mailgun, and I know how to build, test and deploy it. *(proposed: no more layers
than one endpoint needs, with the same conventions and `ArturRios.*` packages as
my other APIs.)*

`ArturRios.Messaging` needs to change first. `IEmailService` only takes the
recipient, subject and body, and the sender is the process-wide `MAILGUN_FROM`.
Hermes needs a sender per client and a reply-to per message.

On yggdrasil it needs an entry in `catalog.yaml` with no `host`, and a
`stacks/hermes-api.proxy.yml` that joins the edge and telemetry networks with no
Traefik labels and no published port. Each environment has its own env file and
its own credentials.
