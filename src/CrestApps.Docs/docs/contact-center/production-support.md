---
sidebar_label: Production support
sidebar_position: 20
title: Contact Center production support
description: Finite production support matrix, initial capacity tier, and prohibited Contact Center deployment combinations.
---

The Contact Center commercial release remains blocked until remediation phases R0 through R8 and their release evidence pass. The versioned machine-readable contract is `.github/contact-center/support-matrix.v1.json`; unlisted combinations are unsupported.

The measurable availability, latency, dependency, recovery, and ownership gates are defined in [Service objectives](service-objectives.md). Every P0/P1 production-readiness finding is tracked to a DRI, approver, test id, CI job, and retained evidence in the [PR-to-test control matrix](pr-test-control-matrix.md).

## Initial GA-Core profiles

The first release targets two provider-specific tenant profiles. A tenant selects one profile and one voice provider; mixing both provider profiles in one tenant is unsupported.

| Profile | Provider | Included voice scope |
| --- | --- | --- |
| `ga-core-asterisk` | Asterisk | Inbound voice plus Manual and Preview dialing |
| `ga-core-dialpad` | DialPad | Inbound voice, Manual and Preview dialing, and call transfer |

The feature identifiers in the matrix describe the target feature graph that R2 must implement. They are not a claim that the current feature graph is production-ready.

## Database and topology

- PostgreSQL 16.x is the only initial production database target.
- SQLite is for local development, demonstrations, and tests only.
- Production requires one region, two to four application nodes, a shared relational database, and a Redis SignalR backplane.
- Single-node production, multi-node operation without the backplane, and multi-region active-active operation are unsupported.

## Tier-1 capacity target

R8 must prove the entire envelope rather than extrapolating from a smaller test:

| Limit | Per tenant | Per deployment |
| --- | ---: | ---: |
| Concurrent signed-in agents | 100 | 250 |
| Concurrent voice interactions | 50 | 100 |
| New interactions per second | 10 | N/A |
| Tenants | N/A | 5 |

These are acceptance ceilings for the first certified tier, not architectural maximums. Higher tiers require separate load, soak, failure, and dependency-limit evidence.

## Distributed harness dependency ledger

R0 records the distributed evidence that cannot be produced honestly by an in-process unit fixture in `.github/contact-center/r0b-harness-dependency-ledger.v1.json`. The ledger does not certify any scenario. It prevents later phases from silently replacing production topology proof with mocks or single-process approximations.

| Scenario | Implementation phase | Certification phase |
| --- | --- | --- |
| Redis backplane with two Orchard shells | R1 | R8 |
| Duplicate/reordered provider stream across two processes | R3 | R4 |
| Provider-listener lease loss and ownership transfer | R4 | R4 |
| Application-node failure during active work | R3 | R8 |
| Redis network partition | R7 | R8 |
| Database network partition | R7 | R8 |
| N/N-1 rolling-version deployment | R7 | R8 |

Each ledger entry resolves the applicable control-matrix ids, current unit evidence, concrete blockers, required infrastructure, and retained evidence directory. R2 builds the minimum two-process Orchard harness; the owning remediation phase adds the missing production behavior; R8 runs the complete release certification.

## Prohibited capabilities and combinations

- Power, Progressive, and Predictive dialing.
- Recording, monitor, whisper, barge, take-over, and bidirectional media.
- More than one voice provider profile in one tenant.
- Elasticsearch in routing, assignment, provider ingest, or another correctness path.
- Any feature, provider, database, or topology combination not listed in the versioned matrix.

Unsupported controls must be hidden or rejected server-side. Enabling an implementation that has not passed the profile's release gates does not make that capability supported.
