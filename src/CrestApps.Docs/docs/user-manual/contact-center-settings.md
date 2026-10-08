---
sidebar_label: Contact Center Settings
sidebar_position: 28
title: Contact Center Settings
description: Site-wide rules for call recording and consent, the outside numbers agents and IVR menus may transfer to, and secure data capture.
technical_manual:
  - contact-center/index
  - contact-center/agent-desktop
  - contact-center/production-support
---

The **Contact Center** settings page holds the rules that apply to every queue and every agent.

| | |
| --- | --- |
| **Menu** | Settings > Contact Center |
| **Permission** | Manage the Contact Center |
| **Feature** | Contact Center; each tab below names the feature it needs |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of the Contact Center settings tabs: recording governance, external transfer destinations and secure data capture">
  <source src="/img/docs/um-cc-settings.mp4" type="video/mp4" />
</video>

The page has up to four tabs. A tab only appears when its feature is enabled.

## Recording governance

Needs the **Contact Center Call Recording** feature. Enabling the feature is what allows calls to be recorded; disable it to stop all recording.

| Field | What it does |
| --- | --- |
| **Record every call automatically** | Starts recording every queue and dialer call when it connects to an agent, every AI voice agent call when it is answered, and every number dialed on the soft phone keypad when it is answered. Extension calls are not recorded. On by default; when off, a call is recorded only when a workflow or a supervisor starts it. Recorded calls are listed on the [Call recordings](call-recordings.md) page. |
| **Consent model** | **All parties must consent** (the default) or **Single party consent is sufficient**. |
| **Tell callers the call is recorded** | Turns on the recording disclosure below. Off by default. |
| **Disclosure** | The words callers hear, for example *This call may be recorded for quality assurance and training purposes.* Up to 500 characters. Use the wording your legal team approves. |
| **Say it to inbound callers** | The disclosure is spoken to a caller on an entry point before its welcome message, menu or queue, even when the entry point has no welcome message. Callers sent straight to voicemail or turned away do not hear it. |
| **Have automated voice agents say it first** | An AI voice agent gives the disclosure before anything else it says. See [How the disclosure is given](#how-the-disclosure-is-given). |
| **Ask agents to say it when the caller has not heard it** | On an outbound call, or an inbound call that reached the agent without the entry point's announcement, the [agent workspace](agent-workspace.md) shows the disclosure and an **I told the customer** button. |
| **Do not record until the caller has heard the disclosure** | With all-party consent, a call starts recording only once the disclosure has been spoken to the caller or the agent has confirmed giving it. AI voice agent calls and soft phone keypad calls are not recorded while this is on. |
| **Retention (days)** | How long recordings are kept before they may be erased. 0 keeps them forever. |
| **Apply legal hold by default** | New recordings start under legal hold, so they cannot be deleted. |
| **Allow agents to pause recording** | Shows the **Pause recording** button in the [agent workspace](agent-workspace.md#protect-card-details-on-a-recorded-call). |
| **Maximum secure-pause window (seconds)** | A paused recording resumes on its own after this long. 0 means no limit. |
| **Require a reason to pause** | The agent must type a reason before pausing. |

### How the disclosure is given

Set the disclosure here once, rather than in each entry point's welcome message or each AI profile's instructions. That way every call gets the same approved words, and calls that never hear a welcome message are covered too.

| Call | Who says it | When |
| --- | --- | --- |
| Inbound call on an entry point | The platform, word for word | First, before the welcome message, menu or queue. |
| Turn-based AI voice agent call | The platform, word for word | At the start of the assistant's opening line. |
| Realtime AI voice agent call | The AI model | The model writes its own opening line, so it is told to say the disclosure first, word for word. Read a few call transcripts to check that it does. |
| Outbound call, or any call the caller reached without the disclosure | The agent | The agent reads it out, then presses **I told the customer**. |

Each disclosure is logged on the call with the time and the words used, as the **Caller told the call is recorded** event. That record is also the caller's consent, so with **Do not record until the caller has heard the disclosure** on, the recording starts once it is given.

:::note
This feature gives the notice and records that it was given. Which calls need a notice, and what it must say, depends on where you and your callers are. Check with your legal team.
:::

## Default numbers

Needs the **Omnichannel Channel Endpoints** feature, which adds the **Omnichannel Addresses** list the numbers are picked from. These are the numbers used for an agent who has none of their own.

| Field | What it does |
| --- | --- |
| **Default phone number** | Returned for an agent who is on no number's **Agents who dial from this number** list, for example by the **Find Agent Numbers** workflow task. Picked from the Omnichannel Addresses used for voice calls. Calls themselves still fall back to the provider's default caller ID. |
| **Default SMS number** | The number an agent who is on no number's **Agents who text from this number** list texts from: the messaging composer starts new conversations from it, and the **Find Agent Numbers** workflow task returns it. Picked from the Omnichannel Addresses used for text messages. |

## External transfer destinations

The outside numbers agents and IVR menus may transfer calls to.

1. Add a row for each destination with a **Display name** (for example *After-hours answering service*) and its **E.164 address** (for example `+17025550199`).
2. Untick **Enabled** to keep a row but stop offering it.
3. Tick **Let agents transfer to numbers that are not on this list** only if agents may type any number. Agents also need the *Transfer Contact Center calls externally* permission.

Emergency and premium-rate numbers are always refused.

## Secure Data Capture

Needs the **Contact Center Secure Data Capture** feature. It lets an agent send the customer a one-time link to type card or bank details themselves, so the agent never hears or sees them.

| Field | What it does |
| --- | --- |
| **Enable agent-assisted secure data capture** | Master switch. Shows **Collect data securely** in the agent workspace. |
| **Capture link lifetime (seconds)** | How long the link works. 30 to 3600, default 300. |
| **Pause recording during capture** | Pauses the call recording while the customer enters the data. On by default. |

Card numbers are checked, only the last four digits are kept, and the security code is never stored.
