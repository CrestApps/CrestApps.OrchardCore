---
sidebar_label: Call Recordings
sidebar_position: 32.5
title: Call Recordings - Search, Listen and Read the Transcript
description: Find a recorded call, play it back, jump to any line of an AI call's transcript, and erase a recording when you have to.
technical_manual:
  - contact-center/index
  - contact-center/agent-desktop
---

Every recorded call is listed on one page, whatever kind of call it was. You can search the calls, play one back and, when an AI voice agent talked on the call, read its transcript line by line.

| | |
| --- | --- |
| **Menu** | Interaction Center > Call recordings |
| **Permissions** | *Listen to own call recordings* (your own calls only) or *Listen to anyone's call recordings* (every user's calls). The *Agent* role has the first, the *Supervisor* role the second. |

<AskYourAdmin />

## Who sees which calls

- With **Listen to own call recordings**, the page lists only the calls you were the agent on. You cannot search for, list or open anyone else's.
- With **Listen to anyone's call recordings**, the page lists every recorded call and you can search by agent too.

## Which calls are recorded

Recording must be allowed and switched on in [Contact Center settings](contact-center-settings.md#recording-governance): **Recording enabled** allows it, and **Record every call automatically** makes it happen without anyone pressing a button. With both on:

| Call | Recorded from |
| --- | --- |
| A queue call or a dialer call | When it connects to an agent. |
| An AI voice agent's call | When the call is answered. |
| A number dialed on the soft phone keypad | When the other side answers. |
| An extension call between colleagues | Not recorded. |

A call shows up on the page once its recording has been saved, which can take a minute after the call ends.

## Search the calls

Use the bar at the top of the page:

- **Started**: the date range to search, such as *Today*, *Last 7 days* or a custom range, in your own time zone.
- **Phone number**: all or part of the customer's number.
- **Direction**: inbound or outbound.
- **Call type**: *Contact Center call*, *AI voice agent* or *Soft phone keypad*.
- **Agent**: whose calls to list, picked from the Contact Center agents. Only shown to users who may hear everyone's calls.

Click **Search**, or **Clear** to start again. Each row shows the customer's number, the direction, the call type, when the call started, the agent and the length.

- **Play** plays the recording right in the list. Click it again to close the player. One recording plays at a time.
- **View** opens the call page.

## View a call

The call page shows who was on the call and when, a player for the recording and, below it, the transcript.

You can link through to the contact's activities with **Contact activities** when the call belongs to a contact.

## Read the transcript

Calls an AI voice agent talked on have a transcript. Calls between people are not transcribed.

Each line of the transcript shows:

- the **time** into the recording the line was said, such as `00:00:21`;
- a **copy** button, which copies the time, the speaker and the line;
- a round **play** button, which plays the recording from that line;
- a grey badge with the **silence** before the line, in seconds;
- who said it: **AI** for the voice agent, **CUSTOMER** for the person called;
- what was said.

Clicking the time also plays from that line. While the recording plays, the line being heard is highlighted. The badge next to **Transcript** adds up the silence on the call.

:::note[The times are close, not exact]
A transcript keeps one time for each line. The silence between lines is worked out from how long each line takes to say, so treat it as a close estimate. For an AI call that was handed to an agent, the AI's part happened before the agent's recording started: those lines show `--:--:--` and have no play button.
:::

## Erase a recording

Only users with the *Delete call recordings* permission see **Erase recording** at the bottom of the call page. No role has it by default except *Administrator*; managing the contact center does not include it.

1. Type why the recording is being erased, for example because the customer asked for their data to be deleted.
2. Click **Erase**.

The recording is deleted for good and no longer listed. A recording under legal hold cannot be erased.
