# Project Overview — Hermes API

## What This Is

Hermes is an internal HTTP service that sends plain text email on behalf of my other backends. A
project calls it with its own API key, and Hermes applies that project's rules and delivers the
message through Mailgun. It has no public route: it is reachable only on the Docker network.

## The Problem

The contact form on the personal page only opens the visitor's own mail client through `mailto:`.
Visitors cannot send a message from the site itself.

Heimdall can send email, but its Mailgun credentials and sending are tied to Heimdall. Every new
project that needs email would repeat the credentials, the sender code and the limits.

An email route is also a target. Each accepted request becomes an email, so abuse can flood an
inbox, use up the Mailgun quota, damage the domain's sending reputation, or send spam to strangers.
Even if every other defense fails, the damage has to stay small and fixed.

## Who It's For

| Who | How they relate to Hermes |
| --- | --- |
| **Personal page** | First-version client. Its server sends contact form messages to the owner. |
| **Heimdall API** | First-version client. Sends transactional email to its users: verification, password reset, two-factor codes and notices. |
| **Future backends** | Added as new clients with their own key and rules. |
| **Owner** | Configures clients and keys, receives contact messages, and is alerted when a client reaches its daily limit. |
| **Site visitors** | Never call Hermes. They use the personal page, which calls Hermes for them. |

## What It Does

- Sends one plain text email per request, with a recipient, a subject, a body and an optional
  reply-to.
- Authenticates each calling project by its own API key.
- Applies each project's rules on the server: its sender, the recipients it may use, and its limits
  per minute and per day.
- Answers clearly whether a message was accepted, invalid, over the limit, or failed at the
  provider, so the caller can decide what to do.
- Keeps limits separate per client, so abuse of one cannot use up another's quota.
- Alerts the owner when a client reaches its daily limit.
- Exposes a health check and metrics like the other APIs.

The first version also includes the two integrations, in their own repositories:

- **Personal page:** a small public `POST /api/contact` route, protected by a per-source limit at
  Traefik, a size cap, validation, a honeypot field and Cloudflare Turnstile, which then calls
  Hermes. The form gains a field for the visitor's email address.
- **Heimdall:** its email goes through Hermes instead of calling Mailgun directly. Its messages and
  its own failure handling stay as they are.

## What It Doesn't Do

- No public endpoint, and nothing that puts a key in a browser.
- No newsletters, bulk sending or mailing lists.
- No HTML templates, attachments or open tracking.
- No receiving of email.
- No storing of message bodies.
- No user interface or admin API.
- No queue or automatic retry.
- No forms for sites without a server, such as the Hugo blog.

## How Success Is Measured

- A message sent from the personal page reaches the owner's inbox, and the owner can reply to it.
- The personal page's key cannot send email to anyone but the owner.
- Heimdall's emails reach its users through Hermes, with the same content as before, and Heimdall
  no longer holds Mailgun credentials.
- A client over its limit gets a 429, and no email goes out.
- One client reaching its limit does not affect another client.
- Logs never contain a subject or a body.
