---
sidebar_label: Voice Media
sidebar_position: 25
title: Voice Media (Hold Music and Prompts)
description: Upload hold music and recorded prompts once and reuse them on queues and IVR menus.
---

The **voice media** library holds the audio your phone system plays: hold music for queues and recorded prompts for IVR menus. Each clip is uploaded to your phone provider so it can be played on live calls.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Voice Media |
| **Permission** | Manage the voice media library |
| **Feature** | Contact Center (`CrestApps.OrchardCore.ContactCenter`) with a phone provider that can host media, such as Telnyx |

<video controls preload="metadata" width="100%" aria-label="Screencast of the voice media library and the upload form">
  <source src="/img/docs/um-voice-media.mp4" type="video/mp4" />
</video>

## Add a clip

1. Open **Interaction Center > Management > Voice Media** and click **Add Voice Media**.
2. Enter a **Name** that people will recognize, such as *Main hold music*, and an optional **Description**.
3. Under **Audio**, choose an MP3, WAV, OGG or WebM file of up to 20 MB.
4. Click **Save**. The file is uploaded to the phone provider.

Uploading a new file to an existing clip replaces the old audio and removes the old file from the provider. The **Audio** field is disabled when no enabled phone provider can host media.

:::note About the screencast
The demo site is not connected to a phone provider, so the screencast shows the form without uploading a file.
:::

## Use a clip

- On a queue, pick it as **Hold music** on the *While callers wait* card. See [Queues](queues.md#while-callers-wait).
- In an IVR menu, pick it as a menu's **Recorded prompt**. See [IVR menus](entry-points-and-ivr.md#build-an-ivr-menu).

:::note Moving to another site
Voice media is not included in deployment plans. After importing queues or IVR menus into another site, upload the clips there and pick them again.
:::
