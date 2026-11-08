# Testing Specification Document — Hermes API

## 1. Purpose

This document defines **how a use case is tested once it has been implemented**. It is a standard to
be followed by any human or agent that builds tests for this project, so that every use case in the
[Use Case Specification Document](Use%20Case%20Specification%20Document.md) receives the same shape of
testing, with the same tools, naming, and structure.

The rule is simple:

> **After a use case is developed, tests are built for it in the same change — before it is considered
> done.** A use case without its tests is incomplete.

The tools and versions used are defined in the
[Technology Stack Document](Technology%20Stack%20Document.md); when the tests run in the delivery flow
is defined in the [Development Workflow Document](Development%20Workflow%20Document.md).

## 2. Testing philosophy

1. **Behavior-driven.** Tests describe *behavior*, not implementation.
2. **Test at the right layer.** Validators, the recipient policy, the usage counters and configuration
   checks are unit-tested. The endpoint and its status codes are tested end to end over HTTP.
3. **Isolation in unit tests.** Every collaborator is replaced by a double, and time is supplied by a
   fake clock so that window boundaries can be tested exactly.
4. **Realism in functional tests.** The real application runs in process with its real pipeline and
   configuration binding. Only Mailgun is replaced.
5. **Same pattern every time.** The workflow in §8 is applied identically to every use case.

### 2.1 The coverage floor

Merged line coverage must stay at or above **90%**, and the `test` check fails below it. Branch
coverage is reported but not gated.

## 3. What to test for each use case

| Artifact produced | Test kind | Test location |
| --- | --- | --- |
| Request validator | Unit | `tests/ArturRios.Hermes.WebApi.Tests/Unit` |
| Recipient policy | Unit | `tests/ArturRios.Hermes.WebApi.Tests/Unit` |
| Usage counters and windows | Unit | `tests/ArturRios.Hermes.WebApi.Tests/Unit` |
| Key hashing and comparison | Unit | `tests/ArturRios.Hermes.WebApi.Tests/Unit` |
| Configuration checks | Unit | `tests/ArturRios.Hermes.WebApi.Tests/Unit` |
| Owner alert | Unit | `tests/ArturRios.Hermes.WebApi.Tests/Unit` |
| `POST /api/emails` | Functional | `tests/ArturRios.Hermes.WebApi.Tests/Functional` |
| `/healthcheck` | Functional | `tests/ArturRios.Hermes.WebApi.Tests/Functional` |
| Personal page `/api/contact` route | Unit, with Vitest | The personal page's repository |
| Heimdall's Hermes `IEmailService` | Unit and functional | The Heimdall API's repository, under its own Testing Specification |

Plain data holders with no behavior get no tests of their own. The Traefik limit in UC-04 is
configuration in yggdrasil and is checked by that repository's own tests, not here.

## 4. Test project layout

The test tree mirrors the source tree, and a test class is named after the production class it covers,
with a `Tests` suffix.

```
src/
└── ArturRios.Hermes.WebApi/
    ├── Clients/
    ├── Emails/
    └── Limits/
tests/
└── ArturRios.Hermes.WebApi.Tests/
    ├── Unit/
    │   ├── Clients/
    │   ├── Emails/
    │   └── Limits/
    └── Functional/
```

## 5. Naming & structure

Every test is named with the **Given / When / Then** pattern:

```
GivenAClientOverItsDailyLimit_WhenSendingAnEmail_ThenAnswers429AndSendsNothing
```

Every test body follows the Given/When/Then shape:

```csharp
[UnitFact]
public void GivenAFixedPolicy_WhenTheRecipientIsNotListed_ThenItIsNotPermitted()
{
    // Given
    var policy = RecipientPolicy.Fixed(["owner@example.com"]);

    // When
    var permitted = policy.Permits("stranger@example.com");

    // Then
    Assert.False(permitted);
}
```

## 6. Unit testing standard

### 6.1 Scope of a unit test

One test exercises one production class. It must not start the web host, reach the network, or
depend on the real clock.

### 6.2 Test doubles

Moq is used for every double, and no second mocking library is introduced. `IEmailService` is always a
double. Test data comes from Bogus rather than large inline literals.

### 6.3 Coverage per unit

For each production unit, a test author walks this list:

- The happy path.
- Each validation failure, including one character over each length limit.
- Each recipient policy outcome, for both `fixed` and `any`.
- Each limit boundary: the last allowed message and the first refused one.
- Each window boundary: the end of a minute and midnight UTC.
- Each configuration error that refuses startup.

## 7. Functional testing standard

### 7.1 Scope

One test exercises one request through the real HTTP pipeline, using the `WebApiTest<TEntryPoint>`
base class, with test clients supplied through configuration.

### 7.2 External dependencies

Mailgun is **always faked**, and no test reaches the network. There is no database, so no container is
needed. The fake records what it was asked to send, so that a test can assert that nothing was sent.

### 7.3 Coverage per entry point

The main flow plus every `AF-xx`, including the authentication flows. Each test asserts both the
response and what the Mailgun fake received. Tests for FR-EM-19, FR-EM-20 and FR-EM-21 also capture the
logs and assert that no subject, body or plain address appears in them.

## 8. Per-use-case workflow

Apply this every time:

1. List the main flow and every `AF-xx` of the use case.
2. Write the unit tests for each new validator, policy and counter behavior.
3. Write one functional test per flow that reaches the endpoint.
4. Run the full suite and fix failures until it is green.
5. Check that merged line coverage is still at or above the floor.

## 9. Running the suites

```bash
dotnet test
```

| Suite | Command |
| --- | --- |
| Unit | `dotnet test --filter "Category=Unit"` |
| Functional | `dotnet test --filter "Category=Functional"` |

The categories are separated by the `[UnitFact]` and `[FunctionalFact]` attributes from
ArturRios.Util.Test.
