# Business Rules — Hermes API

## Domain Entities

| Entity | What it represents | Kept where |
| --- | --- | --- |
| **Client** | A project allowed to call Hermes, such as the personal page or Heimdall. | Environment file |
| **API key** | The secret a client authenticates with. Only its hash is kept. | Environment file |
| **Sender** | The address and optional display name a client's email goes out from, on the shared Mailgun domain. | Environment file |
| **Recipient policy** | Who a client may send to: a fixed list of addresses, or anyone. | Environment file |
| **Limit** | How many messages a client may send per minute and per day. | Environment file |
| **Usage counter** | How much of its limits a client has used in the current windows. | Memory |
| **Email request** | One message to send: recipient, subject, body and optional reply-to. | Never stored |

## Relationships

| From | To | Cardinality |
| --- | --- | --- |
| Client | API key | One client has one or two active keys |
| Client | Sender | One client has exactly one sender |
| Client | Recipient policy | One client has exactly one policy |
| Client | Limit | One client has exactly one per-minute and one per-day limit |
| Client | Usage counter | One client has its own counters, never shared |
| Client | Email request | One client makes many requests, none of which is kept |

## Rules

| ID | Rule | Rationale |
| --- | --- | --- |
| BR-01 | Every request must carry a valid API key belonging to a configured client. | The edge network is shared, so being on it is not enough. |
| BR-02 | The key alone decides which client is calling. A request cannot claim to be another client. | One client must not be able to use another's rules or quota. |
| BR-03 | The sender comes from the client's configuration. A request cannot set it. | A caller must not be able to send as someone else. |
| BR-04 | The recipient must satisfy the client's recipient policy, or nothing is sent. | A leaked key must do as little harm as possible. |
| BR-05 | The personal page's recipient policy is a fixed list holding only the owner's address. | Its key must never be able to email strangers. |
| BR-06 | Heimdall's recipient policy allows any recipient. | It sends transactional email to its own users. |
| BR-07 | A request has exactly one recipient. | No bulk sending. |
| BR-08 | Message bodies are plain text only. | No HTML reaches an inbox through Hermes. |
| BR-09 | A reply-to is optional, is a single address, and is used only as the message's reply-to. | The owner must be able to answer a visitor, and the address must not receive anything from Hermes. |
| BR-10 | Each client has its own per-minute and per-day limits, and one client's usage never affects another's. | Abuse of the contact form must not use up the quota Heimdall's password resets depend on. |
| BR-11 | The personal page's limits are 5 messages per minute and 50 per day. | Bounds the worst case of a public form. |
| BR-12 | Limits count only messages that pass validation. | A malformed request sends nothing and should not use up quota. |
| BR-13 | A client over a limit receives a 429 with `Retry-After`, and no email is sent. | The caller learns when to try again, and the worst case stays fixed. |
| BR-14 | Messages are sent immediately. Hermes does not queue or retry. | A queue would mean storing message bodies. |
| BR-15 | A delivery failure is reported to the caller, and the caller decides what to do with it. | The contact form tells the visitor, while Heimdall hides failures on purpose. |
| BR-16 | Message bodies are never stored or logged, and subjects are never logged. | Hermes must hold no message content. |
| BR-17 | A recipient appears in logs only as the pseudonymous reference `ArturRios.Messaging` already uses. | Logs must not collect email addresses. |
| BR-18 | API keys are kept only as hashes. | Reading the environment file must not reveal a usable key. |
| BR-19 | The owner is alerted when a client reaches its daily limit. | Reaching it means likely abuse or a limit set too low. |
| BR-20 | Hermes has no public route. | The service itself must not be an open target. |
| BR-21 | In production, Hermes refuses to start without Mailgun configured. Elsewhere it logs instead of sending. | Production must not silently lose email, and other environments must work without credentials. |
| BR-22 | Heimdall's limits are 60 messages per minute and 1,000 per day. | High enough for normal use. Heimdall hides send failures, so reaching a limit would mean users silently get no email. |
| BR-23 | The alert for BR-19 is an email from Hermes to the owner, sent at most once per client per day, and it does not count against any client's limit. | Yggdrasil has no alerting of its own, and the alert must still go out when the limit is used up. |
| BR-24 | The per-minute limit uses fixed one-minute windows, and the per-day limit uses a fixed day that resets at midnight UTC. | Simple to reason about, and it gives `Retry-After` an exact value. |

## Validation Constraints

| Field | Required | Constraints |
| --- | --- | --- |
| Recipient | Yes | One valid email address that satisfies the client's recipient policy. |
| Subject | Yes | Up to 200 characters. |
| Body | Yes | Plain text, up to 10,000 characters. |
| Reply-to | No | One valid email address. |
| API key | Yes | Must match an active key of a configured client. |

## Permissions

| Role | May | May not |
| --- | --- | --- |
| **Client** | Send email within its own sender, recipient policy and limits. | Change its rules, see another client, or set its own sender. |
| **Owner** | Add and remove clients, set their rules and replace keys, by editing the environment file and redeploying. | Read past message content, because none is kept. |
| **Anyone else** | Nothing. | Reach Hermes at all. |

## Lifecycle

**Client.** Created by adding it to the environment file and redeploying. Removed the same way, and
its keys stop working at that deploy. There is no soft delete and no history.

**API key.** To replace one, add the new key as the client's second key, deploy, switch the client
to it, then remove the old key and deploy again.

**Usage counter.** Starts at zero and is reset when its window ends, or when Hermes restarts.

**Email request.** Exists only while it is being handled. It ends as one of four results, and
nothing about it is kept beyond the logs that BR-16 and BR-17 allow:

| Result | Meaning |
| --- | --- |
| Accepted | Mailgun accepted the message for delivery. |
| Invalid | It failed validation or the recipient policy, and nothing was sent. |
| Over the limit | A limit was reached, and nothing was sent. |
| Provider failed | Mailgun refused it or could not be reached. |

## Prohibitions

- Never expose a public route, or place an API key in a browser.
- Never let a caller choose the sender.
- Never send to more than one recipient per request.
- Never store a message body, or log a subject or body.
- Never keep an API key in plain text.
- Never share a limit between clients.
- Never send anything to a reply-to address. The personal page must not send a confirmation to the
  address a visitor typed.
