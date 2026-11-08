# Technology Stack Document — Hermes API

## 1. Purpose

This document is the **single source of truth for the technologies used to build Hermes API** — the
runtime platform, language, libraries, cross-cutting concerns, and testing tools, together with the
version each is pinned to and the role it plays.

Every other document in this folder **references this document** for technical choices instead of
restating them, so that:

- The domain documents ([Vision](Vision%20Document.md),
  [System Requirements](System%20Requirements%20Document.md),
  [Use Case Specification](Use%20Case%20Specification%20Document.md)) stay focused on *what* the
  system does.
- The [Operations & Infrastructure Document](Operations%20%26%20Infrastructure%20Document.md) stays
  focused on the platform's structure and operations.
- The [Testing Specification Document](Testing%20Specification%20Document.md) stays focused on *how*
  to test.
- Technology versions and roles are maintained in exactly **one** place.

> **Rule:** when a technology choice changes, it changes here first. Other documents link to this
> one rather than duplicating the detail.

Versions match what the Heimdall API pins at the time of writing, so that one toolchain serves both
services. Hermes has no data storage and no data access layer, so this document has no sections for
them.

---

## 2. Platform & Language

| Concern | Choice | Notes |
| --- | --- | --- |
| Runtime / framework | **.NET 10** (`net10.0`), ASP.NET Core | The runtime image is `aspnet:10.0.13`, pinned by digest. |
| SDK | **.NET SDK 10.0** | `global.json` asks for `10.0.113` with `latestFeature` roll-forward. The build image is `sdk:10.0.402`, pinned by digest. |
| Language | **C#** | Tracks the framework's default language version. |
| Language features | `Nullable` and `ImplicitUsings` enabled, warnings as errors | Applied to every project through shared build properties. |
| Package versions | Central package management with transitive pinning | One `Directory.Packages.props` decides every version. |

---

## 3. Libraries

### 3.1 First-party

| Package | Version | Used by | Role |
| --- | --- | --- | --- |
| **ArturRios.Messaging** | The release produced by the foundation issue | Web API | Delivery through Mailgun. The release Heimdall pins today, `1.4.0`, has no per-message sender or reply-to, so it cannot be used as it is. |
| **ArturRios.Util** | `2.1.0` | Web API | The `DataOutput` result model. |
| **ArturRios.Util.WebApi** | `5.1.0` | Web API | Mapping results to HTTP responses, and shared ASP.NET Core wiring. |

### 3.2 Third-party

| Package | Version | Used by | Role |
| --- | --- | --- | --- |
| **FluentValidation** | `12.2.0` | Web API | Request validation. |
| **Serilog** | `4.4.0` | Web API | Structured logging. |
| **Serilog.AspNetCore** | `10.0.0` | Web API | Serilog integration with ASP.NET Core. |
| **OpenTelemetry.Extensions.Hosting** | `1.19.1` | Web API | Metrics collection. |
| **OpenTelemetry.Instrumentation.AspNetCore** | `1.19.0` | Web API | Request metrics. |
| **OpenTelemetry.Instrumentation.Http** | `1.19.0` | Web API | Outbound call metrics. |
| **OpenTelemetry.Exporter.Prometheus.AspNetCore** | `1.19.1-beta.1` | Web API | Serves the metrics for a Prometheus scrape. This exporter has no stable release. |

Rate limit counters are written in the project and use no library: the limits count only messages
that pass validation and the recipient policy, which the framework's request-level limiter cannot
see.

---

## 4. Cross-Cutting Technologies

| Concern | Technology | Version | How it is used |
| --- | --- | --- | --- |
| Input validation | FluentValidation | `12.2.0` | One validator for the send request. |
| Logging | Serilog | `4.4.0` | Structured JSON to the console. |
| Authentication | Project code | — | Per-client API keys sent as a bearer token and compared as SHA-256 hashes in constant time. |
| Error / result model | ArturRios.Util | `2.1.0` | Handlers return `DataOutput` rather than throwing. |
| Configuration | Environment variables | — | Read at startup through the ASP.NET Core configuration system. |
| Metrics | OpenTelemetry | `1.19.1` | Exposed for Prometheus on an internal port. |

There is no Swagger or OpenAPI document. The contract of the single endpoint is defined in the
[System Requirements Document](System%20Requirements%20Document.md).

---

## 5. External Services

| Service | Used for | Version |
| --- | --- | --- |
| **Mailgun** | Email delivery | API `v3`, the default of ArturRios.Messaging |
| **Cloudflare Turnstile** | Bot check on the personal page's public route | Hosted service, unversioned |

---

## 6. Integrating Projects

These projects keep their own stacks, which are defined in their own repositories.

| Project | What it adds for Hermes | Version |
| --- | --- | --- |
| **Personal page** | Vitest, for its `/api/contact` route only | Latest stable at implementation time |
| **Heimdall API** | The ArturRios.Messaging release produced by the foundation issue | As in §3.1 |

---

## 7. Testing Technologies

These are the technologies mandated for tests. **How** they are applied (naming, structure, coverage,
the per-use-case workflow) is defined in the
[Testing Specification Document](Testing%20Specification%20Document.md); this section is the
canonical list of the tools and versions.

| Concern | Technology | Version | How it is used |
| --- | --- | --- | --- |
| Test framework | xunit.v3.mtp-off | `4.0.2` | Unit and functional tests. |
| Test runner | xunit.runner.visualstudio | `4.0.1` | Runs the tests through `dotnet test`. |
| Test SDK | Microsoft.NET.Test.Sdk | `18.11.0` | Test host. |
| Coverage | coverlet.collector | `10.1.0` | Collects line coverage for the floor. |
| Test helpers | ArturRios.Util.Test | `5.0.0` | Category attributes and the functional base class. |
| Mocking / test doubles | Moq | `4.21.0` | The only mocking library. |
| Test data generation | Bogus | `35.6.5` | Generated test data instead of large literals. |

---

## 8. Version Summary

| Category | Package / Tool | Version |
| --- | --- | --- |
| Platform | .NET | `10.0` |
| Platform | ASP.NET Core runtime image | `10.0.13` |
| Platform | .NET SDK in `global.json` | `10.0.113` |
| Platform | .NET SDK build image | `10.0.402` |
| Language | C# | Framework default |
| First-party | ArturRios.Messaging | The release produced by the foundation issue |
| First-party | ArturRios.Util | `2.1.0` |
| First-party | ArturRios.Util.WebApi | `5.1.0` |
| Validation | FluentValidation | `12.2.0` |
| Logging | Serilog | `4.4.0` |
| Logging | Serilog.AspNetCore | `10.0.0` |
| Metrics | OpenTelemetry.Extensions.Hosting | `1.19.1` |
| Metrics | OpenTelemetry.Instrumentation.AspNetCore | `1.19.0` |
| Metrics | OpenTelemetry.Instrumentation.Http | `1.19.0` |
| Metrics | OpenTelemetry.Exporter.Prometheus.AspNetCore | `1.19.1-beta.1` |
| External | Mailgun API | `v3` |
| External | Cloudflare Turnstile | Hosted service, unversioned |
| Testing | xunit.v3.mtp-off | `4.0.2` |
| Testing | xunit.runner.visualstudio | `4.0.1` |
| Testing | Microsoft.NET.Test.Sdk | `18.11.0` |
| Testing | coverlet.collector | `10.1.0` |
| Testing | ArturRios.Util.Test | `5.0.0` |
| Testing | Moq | `4.21.0` |
| Testing | Bogus | `35.6.5` |
| Integration testing | Vitest, in the personal page | Latest stable at implementation time |
