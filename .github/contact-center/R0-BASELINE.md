# Contact Center R0 baseline

This file pins the production-readiness remediation baseline. Later phase evidence must identify its tested commit and commands explicitly rather than assuming these counts remain current.

## Source identity

| Field | Value |
| --- | --- |
| Baseline commit | `ccb1076ddb8ee249387c019ac28636e08cd43493` |
| Branch | `ma/add-contact-center` |
| Commit subject | `Enforce agent queue entitlements` |
| Commit author | `Mike Alhayek <mike@crestapps.com>` |
| Commit time | `2026-07-13T17:27:27-07:00` |
| Worktree | Clean before baseline execution |

## Environment

| Component | Version |
| --- | --- |
| Operating system | macOS 26.5.2, arm64 |
| .NET SDK | 10.0.105 |
| Node.js | 26.3.0 |
| npm | 11.16.0 |

## Baseline commands and results

| Gate | Command | Result |
| --- | --- | --- |
| Strict solution build | `dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true /p:RunAnalyzers=true /p:NuGetAudit=false` | Passed with 0 warnings and 0 errors |
| Unit tests | `dotnet test -c Release --no-build -p:NuGetAudit=false ./tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj` | 1,472 passed, 0 failed, 0 skipped |
| Telephony browser tests | `dotnet test -c Release --no-build -p:NuGetAudit=false ./tests/CrestApps.OrchardCore.Telephony.PlaywrightTests/CrestApps.OrchardCore.Telephony.PlaywrightTests.csproj` | 24 passed, 0 failed, 0 skipped |
| Frontend assets | `npm run rebuild` | Passed |
| Documentation | `cd src/CrestApps.Docs && npm run build` | Passed |

## Retained test evidence

The baseline run emitted TRX files outside the repository so generated test output is not committed. Their immutable hashes are recorded here:

| Artifact | SHA-256 |
| --- | --- |
| `r0-unit-baseline.trx` | `b1a955113d4b5b71adc6e2f35366999c61f27adc74b30bf3a0c9df5d7cd152c5` |
| `r0-telephony-browser-baseline.trx` | `c2f23575a7cb6ede6aefd749c048c62054876ab1eaceaf2d09c780ecf64e8098` |

Future CI release evidence must retain the raw test results and associate them with the tested commit. These local hashes establish the R0 starting point but do not replace CI evidence for a release candidate.
