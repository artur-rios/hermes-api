# Hermes API

Hermes is an internal HTTP service that sends plain text email on behalf of my other backends. A
project calls it with its own API key, and Hermes applies that project's rules and delivers the
message through Mailgun. It has no public route: it is reachable only on the Docker network.

> **Status:** foundation in place, email sending not implemented yet.

## What it does

- Sends one plain text email per request, with a recipient, a subject, a body and an optional
  reply-to.
- Authenticates each calling project by its own API key.
- Applies each project's sender, permitted recipients and limits on the server.
- Keeps limits separate per project, so abuse of one cannot use up another's quota.
- Answers clearly whether a message was accepted, invalid, over the limit, or failed at the provider.
- Emails the owner when a project reaches its daily limit.

The first version also connects the personal page's contact form and all of Heimdall's email.

## What it doesn't do

- No public endpoint, and nothing that puts a key in a browser.
- No newsletters, bulk sending or mailing lists.
- No HTML templates, attachments or open tracking.
- No receiving of email.
- No storing of message bodies.
- No user interface or admin API.
- No queue or automatic retry.

## Specifications

The project is specified before it is built. Start with the `initial/` documents for context, then
the `requirements/` documents for the normative detail.

| Document | What's in it |
|---|---|
| [Brainstorm](docs/initial/Brainstorm.md) | The original free-form notes this project grew from. |
| [Project Overview](docs/initial/Project%20Overview.md) | What the project is, who it's for, and how success is measured. |
| [Technology Stack](docs/initial/Technology%20Stack.md) | The informal stack decisions. |
| [Workflow](docs/initial/Workflow.md) | How one use case is delivered, step by step. |
| [Business Rules](docs/initial/Business%20Rules.md) | Domain entities, relationships, and the `BR-xx` rules. |
| [Vision Document](docs/requirements/Vision%20Document.md) | Stakeholders, positioning, and the `F-xx` features. |
| [System Requirements Document](docs/requirements/System%20Requirements%20Document.md) | The `FR-<AREA>-xx` and `NFR-xx` requirements, data model, and traceability. |
| [Use Case Specification Document](docs/requirements/Use%20Case%20Specification%20Document.md) | The `UC-xx` use cases, their flows, and their `AF-xx` alternatives. |
| [Development Workflow Document](docs/requirements/Development%20Workflow%20Document.md) | The normative branch pattern, issue lifecycle, and Definition of Done. |
| [Testing Specification Document](docs/requirements/Testing%20Specification%20Document.md) | How tests are written, named, and run. |
| [Technology Stack Document](docs/requirements/Technology%20Stack%20Document.md) | The single source of truth for every technology and version. |
| [Operations & Infrastructure Document](docs/requirements/Operations%20%26%20Infrastructure%20Document.md) | Layout, configuration, logging, health, and the `IR-xx` platform requirements. |

## Installation

Prerequisites: the .NET SDK listed in the
[Technology Stack Document](docs/requirements/Technology%20Stack%20Document.md).

```bash
git clone https://git.techcraftsmen.net/artur-rios/hermes-api.git
cd hermes-api
dotnet restore
```

## Configuring a client

Clients are indexed variables in the environment's `hermes-api.env`, one setting per line. The
[`docker/*.env.example`](docker/local.env.example) files list them all. Hermes reads them once at startup, and refuses to
start with a message naming the client and the setting when one is wrong.

To add a client, or to give one a key, generate a key and keep only its hash:

```bash
KEY=$(openssl rand -hex 32)
printf '%s' "$KEY" | sha256sum
```

Give the key to the client, set the hash as `HERMES_CLIENTS__<n>__KEYHASHES__0`, and deploy.

To replace a key without downtime, add the new hash as the client's second key and deploy, switch the client to the new
key, then remove the old hash and deploy again.

## Testing

`dotnet test` runs the suite described in the
[Testing Specification Document](docs/requirements/Testing%20Specification%20Document.md):

```bash
dotnet test
```

The suite covers unit and functional tests. Every use case is expected to ship with tests before its
pull request is opened.

## Roadmap

| Milestone | Delivers | Depends on | Issues | Status |
|---|---|---|---|---|
| [M-01 — Foundation](https://git.techcraftsmen.net/artur-rios/hermes-api/milestone/88) | The repository, build, configuration, container, CI and yggdrasil entries, and the `ArturRios.Messaging` release that gives a sender and reply-to per message. | — | 1 | 1 / 1 closed |
| [M-02 — Email sending](https://git.techcraftsmen.net/artur-rios/hermes-api/milestone/89) | A configured project can send email under its own key, sender, recipients and limits. | M-01 | 2 | 1 / 2 closed |
| [M-03 — Operability](https://git.techcraftsmen.net/artur-rios/hermes-api/milestone/90) | The owner is told when a project reaches its daily limit, and the platform can see that Hermes is healthy. | M-01, M-02 | 2 | 0 / 2 closed |
| [M-04 — Integrations](https://git.techcraftsmen.net/artur-rios/hermes-api/milestone/91) | Visitors can send a message from the personal page, and all of Heimdall's email goes through Hermes. | M-01, M-02, M-03 | 2 | 0 / 2 closed |

The counts are as of when the milestones were created. The milestone pages on Forgejo are the live view.

## Backlog

### M-01 — Foundation

| Issue | Work | Spec |
|---|---|---|
| [#1](https://git.techcraftsmen.net/artur-rios/hermes-api/issues/1) | Project scaffold and initial infrastructure | [Operations & Infrastructure](docs/requirements/Operations%20%26%20Infrastructure%20Document.md) |

### M-02 — Email sending

| Issue | Work | Spec |
|---|---|---|
| [#2](https://git.techcraftsmen.net/artur-rios/hermes-api/issues/2) | UC-01 — Send an email | [Use Case Specification](docs/requirements/Use%20Case%20Specification%20Document.md) |
| [#3](https://git.techcraftsmen.net/artur-rios/hermes-api/issues/3) | UC-02 — Configure a client and its keys | [Use Case Specification](docs/requirements/Use%20Case%20Specification%20Document.md) |

### M-03 — Operability

| Issue | Work | Spec |
|---|---|---|
| [#4](https://git.techcraftsmen.net/artur-rios/hermes-api/issues/4) | UC-03 — Alert the owner at the daily limit | [Use Case Specification](docs/requirements/Use%20Case%20Specification%20Document.md) |
| [#7](https://git.techcraftsmen.net/artur-rios/hermes-api/issues/7) | UC-06 — Check health | [Operations & Infrastructure](docs/requirements/Operations%20%26%20Infrastructure%20Document.md) |

### M-04 — Integrations

| Issue | Work | Spec |
|---|---|---|
| [#5](https://git.techcraftsmen.net/artur-rios/hermes-api/issues/5) | UC-04 — Send a contact message | [Use Case Specification](docs/requirements/Use%20Case%20Specification%20Document.md) |
| [#6](https://git.techcraftsmen.net/artur-rios/hermes-api/issues/6) | UC-05 — Deliver Heimdall's email through Hermes | [Use Case Specification](docs/requirements/Use%20Case%20Specification%20Document.md) |

## Contributing

One use case = one branch = one issue = one pull request. The full process — branch naming, issue
status lifecycle, the testing gate, and the Definition of Done — is in the
[Development Workflow Document](docs/requirements/Development%20Workflow%20Document.md).
