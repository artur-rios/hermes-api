# Changelog

All notable changes to the Hermes API are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- The project scaffold: the solution, shared build settings, environment configuration, JSON console logs,
  `/healthcheck`, Prometheus metrics on port 9464, the container image and the `test` and `docker` checks.
- Clients are read from the environment at startup. Hermes refuses to start, naming the client and the setting, when a
  client is invalid, when two clients share an identifier or a key, when a sender is off the Mailgun domain, or when
  production has no Mailgun credentials or alert address.
