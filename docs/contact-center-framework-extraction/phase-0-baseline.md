# Phase 0 baseline

Recorded on branch `ma/contact-center-framework-extraction`, taken from `ma/add-contact-center`
at commit `a71550af`. These numbers are the floor that every Phase 0 and Phase 1 workstream gate
compares against (see [00-phase-0-preparation.md](00-phase-0-preparation.md) section 3).

## Commands

The test projects use Microsoft.Testing.Platform, so `dotnet test` reports "Zero tests ran".
Run the built test assembly directly instead.

```bash
dotnet build CrestApps.OrchardCore.slnx -c Release -warnaserror -p:TreatWarningsAsErrors=true -p:RunAnalyzers=true -p:NuGetAudit=false
dotnet tests/CrestApps.OrchardCore.Tests/bin/Release/net10.0/CrestApps.OrchardCore.Tests.dll
dotnet tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests/bin/Release/net10.0/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.dll
```

Note: pass MSBuild properties as `-p:Name=Value`. The `/p:` form is rewritten into a path by the
POSIX shell used here and MSBuild then rejects it with `MSB1008: Only one project can be specified`.

## Baseline

| Suite | Total | Failed | Skipped | Time |
| --- | ---: | ---: | ---: | ---: |
| `CrestApps.OrchardCore.Tests` | 5294 | 0 | 1 | ~134 s |
| `CrestApps.OrchardCore.ContactCenter.FeatureActivationTests` | 76 | 0 | 2 | ~45 s |
| `CrestApps.OrchardCore.ContactCenter.DistributedTests` | not run | - | - | needs Redis + PostgreSQL (`CONTACT_CENTER_REDIS_CONFIGURATION`, `CONTACT_CENTER_POSTGRES_CONNECTION`) |
| `CrestApps.OrchardCore.Telephony.PlaywrightTests` | not run | - | - | needs a browser host |

Release build with warnings as errors: succeeds.

The single skipped test in the main suite is
`Telephony.AsteriskBrowserAudioE2ETests.BrowserToAsteriskWebRtcAudio_WithDirectIceAndForcedTurn_VerifiesReceivedToneFrequencies`,
which needs real Asterisk, coturn and browser WebRTC.

## Scope

The in-scope ("moving") projects for the extraction are the 30 projects listed in
[02-inventory-and-target-layout.md](02-inventory-and-target-layout.md): 2202 of the 4469 C# files
under `src`.

## Coverage

Per the sequencing decision recorded for this branch, the global coverage bar in P0.1 is not
enforced as an up-front gate. Instead each workstream pins the classes it touches before
refactoring them (plan principle 3), and the suites above must stay green after every workstream.
