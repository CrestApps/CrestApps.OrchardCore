---
sidebar_label: Soft phone audio quality
sidebar_position: 13
title: Soft phone audio quality — findings, fixes and what is left
description: What was measured on live Telnyx soft-phone calls, the defects it exposed and how each was fixed, the settings an agent can change, and the limits that no code change removes.
---

# Soft phone audio quality

A record of a live investigation into agents sounding quiet, muffled and delayed to PSTN callers. It is kept
because most of what it cost was learning what to measure: six confident explanations were each overturned by a
measurement, usually within minutes, and the instrumentation is now the durable part.

## How to diagnose a complaint about a call

Open the soft phone with the Diagnostics tab enabled (the `EnableDiagnostics` site setting, or `?diag=1` for one
session). The live readout carries, per direction:

```
MOS 4.33 · loss 0.0% · jitter 3ms · rtt 121ms · buffer 61ms · conceal 0.0%
  · in 0.247 · out 0.163 · mic 0.184
  · codec audio/opus · send audio/opus · sent soft-phone stream
  · echo erl -30.0dB erle 0.2dB · remote loss 0.0% · ice srflx/host
  · mic device Headset (WH-1000XM5) (Bluetooth) · capture 48kHz mono ec+ns+agc boost+6
```

Every field is also written to the server log on each transmitted quality sample, so a complaint can be
reconstructed after the fact. Read them in this order:

| Symptom | Field to read first | What it means |
| --- | --- | --- |
| The caller cannot hear the agent | `sent` | `OTHER: …` means the far end is hearing a different microphone from the one the agent picked |
| The caller says the agent is quiet | `mic`, `out` | The level feeding the encoder, and the measured peak of the stream being sent |
| The agent cannot hear the caller | `in` | Measured loudness of the far end; `loss`, `jitter` for the network |
| A pause before the reply | `rtt`, `buffer` | Round trip to the provider, and how long this browser holds audio before playing it |
| Rough or broken audio | `conceal` | The share of received audio the browser had to invent |
| Dull or narrow audio | `codec`, `send` | The negotiated codec each way |

Two rules learned the hard way. **An absent measurement is reported as `-1` or `n/a`, never as zero** — a browser
that reports no audio levels (Firefox reports none) must stay distinguishable from a microphone that is silent.
And **read paired fields together**: `buffer` against `conceal`, `mic` against `sent`.

## Defects found and fixed

- **Calls dropped about a minute in.** The reconciliation sweep treated a browser-placed call's history record —
  which has no provider identity, because the provider SDK dialled it directly and the platform never saw it — as
  an orphan with no grace period, and announced a terminal state that the soft phone honoured by hanging up its
  own live call. Client-recorded interactions are now left alone, and the client ignores server terminal states
  for calls it placed itself.
- **The far end heard a different microphone.** On inbound calls the provider SDK discards the stream it is handed
  and captures its own, on the browser's default device with default constraints, so the device picker, the mono
  constraint, the level meter and the telemetry all described a track nobody was listening to. The stream is now
  set where the SDK honours it.
- **A call sent nothing at all.** A capture left `live` but `muted` (a headset asleep overnight) produces no
  frames; thirty seconds of a call went out with zero bytes sent. A muted track now counts as dead, and one that
  dies mid-call is replaced under the call without renegotiating.
- **Device changes were ignored on a call.** Selecting another microphone deferred to "the next call", a promise
  nothing kept. Both devices now change in place.
- **The telemetry contradicted itself.** A Web Audio source node binds to the track it was built from, so after a
  mid-call track swap the level probes measured a stopped track and reported silence.
- **Firefox reported silence on a working call.** Firefox only processes an audio graph that reaches the
  destination, so probes and the level meter route through a silent gain node.
- **Twenty identical warnings a call.** The SDK's `LOW_INBOUND_AUDIO` fires after any three-second pause by the
  far end. It is kept, as information.
- **Opus was never negotiated inbound.** Not a code defect: `OPUS` was unchecked on the credential connection, so
  Telnyx's offer to the browser carried only G722/PCMU/PCMA. Checking it and moving it to the top of the
  connection's inbound codec list was the whole fix.

## Settings an agent can change, and when

All live, on a call in progress, under the gear icon.

- **Microphone / Speaker** — change either mid-call.
- **Echo cancellation, noise suppression, automatic gain control** — on by default. Turn one off and ask the
  caller, when they report sounding hollow or processed. Chrome cannot change these on an open capture, so each
  flip re-captures and swaps the track.
- **Microphone boost** — off by default; a gain stage and hard limiter in front of the encoder. Measured speech
  left one headset at about −21 dBFS, well under what a PSTN caller expects, with automatic gain control already
  at its limit. Judge it by `mic` in the readout: around 0.3–0.6 is healthy, a steady **1.000 means the boost is
  too high** and transients are pinning.
- **Audio delay** — how long incoming audio is held before playing. Automatic by default. Shortening it shortens
  the pause before the agent hears a reply; too short and the caller's voice breaks up, which `conceal` will show.
- **Region** — which of the provider's locations the phone connects to. Automatic by default, which follows the
  tenant's **Soft phone region** setting and then the provider's own geo-routing. Unlike everything else here this
  is fixed when the provider client is built, so changing it re-registers the phone — immediately when idle, and
  after the current call ends otherwise. It moves the **signaling** edge; whether the media gateway follows is not
  documented by the provider, so judge it by `rtt` on a call before and after rather than by assumption.

## Limits that no setting removes

- **Bandwidth to a PSTN caller.** The leg to the carrier is G.711 — 3.4 kHz — so a cell user accustomed to HD
  calls hears a landline. Telnyx's HD Voice feature (AMR-WB toward AT&T, T-Mobile, Verizon; free) is the only
  lever, it is per number, and it requires an eligible +1 US long code. The number in use is not eligible.
- **Delay.** A browser bridged to the PSTN will always lag a phone-to-phone call. Of a round trip near half a
  second, the parts that can be influenced are the browser's playout buffer (60–105 ms, and 310–628 ms in the
  first half-minute of a call — hence the **Audio delay** setting) and the round trip to the provider's media edge
  (measured 86–263 ms, which is high for the distance — the **Region** setting is the lever that moves it, and the
  one that decides whether it helps is `rtt` measured before and after). Everything after the provider is the
  carrier's.

## A caution about interpretation

Recorded prompts and text-to-speech reach the same caller, over the same G.711 leg, sounding clear. That single
observation refuted an earlier conclusion that narrow bandwidth explained "muffled": what differs is the source
level, not the path. When a structural limit and a fixable one both explain a symptom, prefer the one that can be
measured — and measure it before deciding.
