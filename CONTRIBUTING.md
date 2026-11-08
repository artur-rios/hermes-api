# Contributing

This repository lives at <https://git.techcraftsmen.net/artur-rios/hermes-api>. The GitHub repository is a read-only
mirror of it.

The process a use case follows, from branch to merge, is in the
[Development Workflow Document](docs/requirements/Development%20Workflow%20Document.md).

## Prerequisites

- **.NET 10 SDK**
- **Docker**, to build and run the container

## Build and test

```bash
dotnet build ArturRios.Hermes.sln
dotnet test
```

`ci/test.sh` is the `test` check: restore, build, both suites, and the 90% line coverage floor. `ci/docker.sh` is the
`docker` check. Both run on your machine as they do in CI.

## Run locally

```bash
cp docker/local.env.example docker/local.env
docker compose up -d --build
```

The API answers on `127.0.0.1:5090`. Without Mailgun credentials, messages are logged instead of sent.

## Branching model

```
feature/<name> ─┬─▶ develop ──▶ release/x.y.z ──▶ main  (tag vx.y.z)
fix/<name> ─────┘
```

| Branch | Cut from | Merges into | How |
| --- | --- | --- | --- |
| `feature/<name>`, `fix/<name>` | `develop` | `develop` | Pull request. The branch is deleted on merge. |
| `release/x.y.z` | `develop` | `main` | Pull request. Jenkins merges it and tags `vx.y.z` after deploying to production. |
| `develop`, `main` | — | — | Protected: no direct pushes, no force pushes, no deletion. |

Names are lowercase: letters, digits, `.`, `_` and `-`.
