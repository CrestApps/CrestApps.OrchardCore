# CrestApps OrchardCore Tests

The unit and integration tests for every CrestApps module in this repository.

## Running them

The project uses xUnit v3 on the Microsoft Testing Platform, and the assembly runs one test at a time
(`TestParallelizationBehavior.cs`). Run it with `tests/run-unit-tests.ps1`, which runs the test classes in one process
per core, each still one test at a time, and checks that every test method ran. CI runs the same script.

```bash
pwsh ./tests/run-unit-tests.ps1 -Build
```

Filter to one area with a namespace (the `CrestApps.OrchardCore.Tests.` prefix is optional) or part of a class name:

```bash
pwsh ./tests/run-unit-tests.ps1 -Namespace Checkout, Subscriptions
pwsh ./tests/run-unit-tests.ps1 -Class CheckoutEngineTests
```

The results, one log per process and the class timings that balance the next run are written to `TestResults/unit`.
The script prints the slowest classes at the end of every run.

Always verify with a **Release** build and warnings as errors before calling a change done, because the
analyzer set is stricter there than in Debug and CI runs it that way:

```bash
dotnet build CrestApps.OrchardCore.slnx -c Release -warnaserror
```

## Tests that need infrastructure

Some suites talk to real services and **throw rather than skip** when the environment is not configured.
Silently skipping is worse: a suite that quietly runs nothing looks green while covering nothing.

| Suite | Needs |
| --- | --- |
| Contact Center distributed tests | `CONTACT_CENTER_REDIS_CONFIGURATION` and `CONTACT_CENTER_POSTGRES_CONNECTION` |

Everything else, including the commerce suites, runs with no external dependency.

## How the commerce tests are arranged

Money code fails in a small number of expensive ways, so the tests are organized around those failures
rather than around classes:

| Folder | What it pins |
| --- | --- |
| `Checkout` | The engine's ordering guarantees: an attempt is durable before a provider is called, completion is idempotent under concurrency, a partial failure compensates, and a guest cannot resume a stranger's checkout. Also the discount arithmetic. |
| `Subscriptions` | That an agreement is created only from confirmed payments, that every lifecycle transition is idempotent, and that access outlives cancellation by exactly the period the customer paid for. |
| `Transactions` | The ledger's state machine, the reminder cadence, Pay Later renewal cycles, and that a refund can never exceed what is still refundable. |
| `Stripe` | The gateway boundary: currency conversion, inline recurring pricing, deterministic idempotency keys, and that an agreement settles only when Stripe says its invoice was paid. |
| `Taxation` | That tax is determined rather than stored, and that a refund allocates from the original snapshot rather than today's rules. |

## Writing a test here

Two conventions matter more than style:

1. **Say what would go wrong.** A test named after a method tells the next reader nothing. A summary that
   names the failure ("a webhook and the sweep both reporting the same cycle must advance it once") is what
   stops somebody deleting the test when it becomes inconvenient.
2. **Pass `TestContext.Current.CancellationToken`** to anything that takes a token. The xUnit analyzer
   enforces it, and a Release build fails without it.

Prefer the in-memory fakes under each area's `Fakes` folder to a mock of a store. A fake that behaves like
the real store catches ordering mistakes a mock will happily allow.
