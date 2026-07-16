---
sidebar_label: Service objectives
sidebar_position: 21
title: Contact Center service objectives
description: Availability, latency, dependency, recovery, and ownership objectives for the Contact Center Tier-1 production profile.
---

The versioned machine-readable contract is `.github/contact-center/service-objectives.v1.json`. These objectives apply to the Tier-1 profile in the [production support matrix](production-support.md) and remain release gates rather than current production claims.

## Availability and error budgets

Objectives use a rolling 30-day window.

| Service indicator | Availability | Maximum monthly error budget |
| --- | ---: | ---: |
| Agent routing API | 99.9% | 43.8 minutes |
| Provider webhook ingress | 99.95% | 21.9 minutes |
| Agent real-time session | 99.9% | 43.8 minutes |

An exhausted error budget blocks feature releases affecting the failed indicator until the owning SRE and engineering roles approve recovery evidence or an explicit exception.

## Latency and freshness

| Indicator | p95 | p99 |
| --- | ---: | ---: |
| Routing decision | 250 ms | 750 ms |
| Committed offer to agent notification | 500 ms | 1,500 ms |
| Authenticated webhook to durable inbox acknowledgement | 500 ms | 1,500 ms |
| Supervisor dashboard freshness | 2,000 ms | 5,000 ms |

Provider-side ringing, carrier setup, and customer answer time are measured separately from application command acceptance.

## Dependency limits

- Relational database commands: 100 ms p95 and 3-second timeout.
- Redis operations: 25 ms p95 and 1-second timeout.
- Provider commands: 2-second p95 application observation and 10-second bounded timeout before durable `OutcomeUnknown` handling.
- Provider webhook request bodies: maximum 1 MiB before authentication or durable acceptance.

Retries must be bounded, jittered, idempotent, and included in the caller's end-to-end deadline. A dependency timeout cannot be converted into success-shaped state.

Configure the provider-command boundary with `CrestApps_Telephony:Commands:Timeout`. The default is 10 seconds and startup validation accepts one second through two minutes. This is the end-to-end application observation deadline, including provider retry behavior, not a per-attempt HTTP timeout. SignalR disconnects and request cancellation do not cancel an admitted mutation. If the deadline or host shutdown occurs before confirmation, durable commands enter `OutcomeUnknown` and synchronous commands report an unknown outcome; provider-confirmed local persistence then completes independently of the expired command token.

## Recovery objectives

| Scope | RPO | RTO |
| --- | ---: | ---: |
| Relational Contact Center state | 5 minutes | 60 minutes |
| Redis and real-time projections | 0 minutes | 15 minutes |
| Durable provider-event ingress | 0 minutes | 30 minutes |

Redis is rebuildable coordination/projection state and is not an authoritative system of record. Zero provider-ingress RPO requires durable acknowledgement only after inbox commit and provider replay/reconciliation procedures.

## Ownership

Architecture, security/privacy, SRE/database, quality/release, product, and documentation roles own the corresponding evidence. Named individuals must be assigned to these roles for a release candidate; role names in the contract do not constitute final approval.
