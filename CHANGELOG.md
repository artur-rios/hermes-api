# Changelog

All notable changes to the Hermes API are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `GET /internal/health` on the metrics port (9464) reports whether Mailgun is reachable, as `Healthy`, `Degraded`
  (unreachable, with a reason word) or `NotConfigured`, in a bare JSON report that holds only names, statuses and
  reasons. It needs no credential, is never served on the application port, and sends no email.

## [1.0.0] - 2026-11-09

### Added

- The `heimdall-signals` client is specified and documented: Heimdall's security alert job sends under its own client,
  from `heimdall-signals@` the mail domain, to one fixed address, at 5 messages a minute and 200 a day.
- The project scaffold: the solution, shared build settings, environment configuration, JSON console logs,
  `/healthcheck`, Prometheus metrics on port 9464, the container image and the `test` and `docker` checks.
- Clients are read from the environment at startup. Hermes refuses to start, naming the client and the setting, when a
  client is invalid, when two clients share an identifier or a key, when a sender is off the Mailgun domain, or when
  production has no Mailgun credentials or alert address.
- `POST /api/emails` sends one plain text email for the client the API key belongs to, under that client's sender,
  recipient policy and per-minute and per-day limits. Outside production without Mailgun, it logs instead of sending.
- The owner is emailed once per client per UTC day when a client's message uses up the last of its daily limit. A failed
  alert is logged and does not change the client's answer.
