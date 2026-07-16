---
sidebar_label: Single-Node Completion Roadmap
sidebar_position: 1
title: Single-Node Completion Roadmap (Plan 2)
description: The phased roadmap that makes the bundled Asterisk provider carry real browser audio and run a credible contact center on a single node first, built entirely on Orchard Core's multi-tenant infrastructure.
---

This page is the public, forward-looking roadmap for completing the **Contact Center** so that a single Orchard Core node runs a *credible*, end-to-end contact center — real browser audio, inbound routing, supervisor monitoring, and recording — with distributed (multi-node) hardening following as a second phase. It complements the architecture overview on the [Contact Center](index.md) page and the candidate GA profiles on the [Production support](production-support.md) page.

:::note
This is a roadmap. Capabilities described as *planned* or *in progress* are not yet supported for production and may change. The authoritative, contract-tested capability matrix ships with the release that delivers each capability.
:::

## Goals

1. **Single node, fully functional first — but distributed by construction.** The immediate objective is a certified single-node profile in which every advertised capability actually works and is proven by an automated test that plays and verifies real audio. The services are built on Orchard Core's distributed primitives (distributed cache, distributed locks, and the Redis-backed SignalR backplane), so the platform is *distributable* by design; for this phase it is delivered and **fully tested on a single node** — reliable, secure, and distributable — using Docker containers orchestrated by the project's **.NET Aspire host** with the Redis feature enabled, so those distributed paths are actually exercised on one node. **Multi-node and high-availability testing follows as a secondary phase.**
2. **Provider-agnostic browser media.** Browser audio is delivered through a pluggable media-adapter abstraction. Asterisk is the first concrete adapter (WebRTC via `chan_pjsip` over secure WebSockets with DTLS-SRTP and ICE/TURN); other providers can add their own adapters later.
3. **Built on the Orchard Core foundation.** Every new service extends Orchard Core's own infrastructure — `IDistributedCache`, `IDistributedLock`, the `OrchardCore.Redis` feature, and the tenant-qualified SignalR backplane — so the platform stays multi-tenant safe with no cross-tenant data sharing. These distributed services are switched **on** in the single-node reference host so they are exercised and verified, not bypassed. All work is additive: it extends existing Orchard Core and module patterns and must not regress the current multi-tenant architecture or existing telephony/omnichannel behavior.
4. **Honest verification.** "Done" means a runnable end-to-end proof of real bidirectional audio through a bundled Asterisk, plus supervisor monitor/whisper/barge and recording verified against that same live call.

## Multi-tenancy and the Orchard Core foundation

The Contact Center is a multi-tenant platform, and Plan 2 treats tenant isolation as a first-class, non-negotiable requirement:

- **No parallel infrastructure.** Distributed cache, locking, and real-time messaging use Orchard Core's own primitives and the per-tenant Redis-backed features, rather than a separate stack.
- **No shared mutable state across tenants.** Registries, credential caches, provider-settings caches, and media-session tables are tenant-scoped (shell-scoped services or tenant-keyed distributed cache invalidated through Orchard Core's signaling), never process-global.
- **Tenant-namespaced telephony.** When multiple tenants share one Asterisk or TURN server, every telephony identity — application name, dialplan context, endpoints, bridges, recordings, and TURN credentials — is namespaced per tenant, and inbound provider events are bound to and resolved through the owning tenant so one tenant can never route into, monitor, or record another tenant's call.
- **Per-tenant provider configuration.** Each tenant can point at its own provider. An unconfigured provider appears *unavailable* rather than causing errors.

These commitments are validated by adversarial two-tenant tests (identical logical identifiers across two tenants sharing one PBX, including a negative test proving one tenant can never receive, route, monitor, or record another tenant's telephony events) and by automated architecture guards in CI.

## Capability status (Asterisk reference provider)

| Capability | Today | Plan 2 target |
| --- | --- | --- |
| Browser audio (WebRTC) | Not available | Delivered (single-node certified) |
| Inbound routing to a queue and agent | Not available | Delivered |
| Server-side connect / bridge on accept | Not available | Delivered |
| Call recording | Not available | Delivered, with consent/retention governance |
| Supervisor monitor / whisper / barge | Not available | Delivered |
| Blind transfer | Not available | Delivered |
| Attended/consult transfer and conference | Not available | Delivered |
| Multi-node / high-availability media | Not claimed | Gated by the platform's multi-node release program |

The DialPad provider remains an external-device call-control integration for this phase; a DialPad browser-media adapter is a later addition.

## Phased roadmap

The work is sequenced so that correctness and safety land before power:

1. **Foundations.** A runnable reference stack (Asterisk + TURN + Redis + CMS), orchestrated by the project's **.NET Aspire host** with a Docker Compose variant for headless CI, the automated audio proof (initially failing), and the two-tenant isolation harness. Two single-node profiles are exercised — one with the Redis-backed distributed cache, locking, and real-time backplane **enabled**, and one with those features **disabled** — so the platform is proven correct both with and without the backplane, and the digest-pinned Asterisk image is checked at startup for the WebRTC modules it must carry.
2. **State authority and consistency.** A canonical live-call ownership model with optimistic-concurrency protection and single-writer guarantees, so call, interaction, queue, and agent state cannot diverge under load — even on one node.
3. **Security and authorization.** A single call-control authorization boundary that verifies ownership and entitlement for every call action and every supervisor operation, with server-resolved, policy-checked transfer and dial destinations.
4. **Browser media.** The WebRTC media adapter and the Asterisk WebRTC endpoint lifecycle (secure credential provisioning, rotation, and revocation).
5. **Connect and inbound routing.** Real server-side bridging on agent accept and idempotent inbound routing from Asterisk into the queue-and-offer pipeline.
6. **Supervisor and recording.** Recording, listen/whisper/barge with a real supervisor audio path, transfer, and conference — with recording continuity across transfers.
7. **Reliability and observability.** Health, graceful degradation, reconnection and in-flight call recovery, and media-quality telemetry with tenant-correlated tracing.
8. **Test honesty and proof.** Real audio-content assertions (tone injection and frequency analysis), a real relational-database test matrix, and CI architecture guards; the end-to-end audio proof goes green.
9. **Documentation and capability matrix.** Honest, code-matching capability documentation, a secure deployment guide, and a single-node quickstart.
10. **Multi-node testing and distributed hardening (secondary).** The distributed code already runs and is exercised on the single node; this phase adds multi-node *testing* and hardening — readiness gating, single-consumer election for provider event streams, distributed rate limiting, and scale-oriented queries — feeding the platform's multi-node release program.
11. **Future capability (post-GA).** A unified work-state consistency root, real IVR, first-class non-voice channels, and additional provider adapters.

## Verification and support

- **Exit bar.** The single-node profile is considered complete only when the automated end-to-end test — run on the .NET Aspire host (or the Docker Compose CI variant) with Redis enabled — proves real, audible, bidirectional audio (over both direct and relayed media paths), supervisor audibility, and a retrievable recording of that same call.
- **Support gating.** The certified single-node profile, with a published concurrent-call capacity ceiling, is the configuration to trust for live audio at general availability. Multi-node and media high-availability claims are gated by the platform's separate multi-node release program and are not published ahead of their proof.

## Tracking

Detailed, evidence-based execution is tracked internally alongside the master Contact Center plan. This public page summarizes intent and progress; the shipped capability matrix in each release is the authoritative statement of what is supported.
