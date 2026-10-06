---
sidebar_label: Extensions
sidebar_position: 27
title: Extensions
description: Give people short internal extension numbers so colleagues can call and transfer to them by extension or by name.
technical_manual:
  - telephony/extension-dialing
---

An **extension** is a short internal number, such as `1001`, that belongs to one user. Colleagues can dial it from the soft phone keypad, search for the person by name, or pick them as a transfer target. Use extensions when people need to reach each other inside the company without dialing a full phone number.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Extensions |
| **Permission** | Manage telephony extensions |
| **Feature** | Telephony |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an extension for a user">
  <source src="/img/docs/um-extensions.mp4" type="video/mp4" />
</video>

## Create an extension

1. Open **Interaction Center > Management > Extensions** and click **Add extension**.
2. Fill in the fields below.
3. Click **Save**.

| Field | What it does |
| --- | --- |
| **Extension number** | The number a colleague dials to reach this user, for example `1001`. Required, and it must be unique. |
| **User** | The user this extension rings. Type to search the list of enabled users. Required. |
| **Display name** | The name a colleague sees when they call this extension, for example *Front desk*. Leave it empty to show the user's own name. |

Every saved extension can be dialed straight away; there is no enabled or disabled switch. To stop an extension ringing, delete it from the list.

## Find an extension in the list

The list shows each extension's number as a grey badge, then its display name, then the user it rings as a separate badge. The search box matches the extension number and the display name.

## How people see an extension

Wherever the soft phone shows an extension, it also shows who it rings:

- The keypad names the person while an extension is typed, and lists matching people when you type part of a name.
- A call to an extension is shown as, for example, *Jane Doe · ext 2*, and so is the entry on the **Recent** tab.
- The transfer panel offers **Transfer to extension 2 · Jane Doe**.
- The colleague you call sees your name on their ringing call.

The name shown is the extension's **Display name** when it is set. Otherwise it is the user's display name, as the site shows users, and then their user name. A new extension, or a changed name, can take up to five minutes to appear in a phone that is already open; reopening the phone picks it up straight away.

People never see their own extension in the list, and calling or transferring to it is refused with *That's your own extension.*, because it would only ring the phone they are using.

The agent sees their own extension as a badge in the [agent workspace](agent-workspace.md).

## Call or transfer to an extension

- **Call:** open the soft phone keypad, switch to **Dial extension**, and type the number or a name. See [Call an extension](calls.md#call-an-extension).
- **Transfer:** in the transfer panel, switch to **Transfer to an extension**, or pick the person from the directory. See [Transfer a call](calls.md#transfer-a-call).
- **Conference:** call the extension with **Add call**, then merge the calls. See [Merge calls into a conference](calls.md#merge-calls-into-a-conference).

When the person does not answer, the caller is sent to that person's voicemail, and the message appears on their soft phone's **Voicemail** tab. A caller who hangs up before the call is answered leaves no message.

:::note[Which phone systems support extensions]
Extension calls work with Telnyx. Asterisk does not offer extension calls yet, so the **Dial extension** control is hidden there.
:::
