---
sidebar_label: Voice Media
sidebar_position: 25
title: Voice Media (Hold Music and Prompts)
description: Upload hold music and recorded prompts once and reuse them on queues and IVR menus.
technical_manual:
  - contact-center/voice-routing
  - contact-center/configuration-deployment
---

The **voice media** library holds the audio your phone system plays: hold music for queues and recorded prompts for IVR menus. The platform keeps a master copy of each clip and sends a copy to your phone provider, so a clip keeps playing when you change provider or the provider loses its copy.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Voice Media |
| **Permission** | Manage the Contact Center voice media library |
| **Feature** | Contact Center, with the Telephony feature for the master copy and a phone provider that can host media, such as Telnyx |

<AskYourAdmin />

<YouTube video="contact-center-voice-media" title="Video overview of Voice Media: hold music and prompts" />

<YouTube video="contact-center-setup-walkthroughs" title="Video of the voice media library and the upload form" start={194} end={218} />

<<<<<<< HEAD
<YouTube video="contact-center-ai-voice-agent-inbound" title="Video of the two things called Voice Media: the Contact Center Voice Media feature and this library" start={205} end={245} />

The **Voice Media** library on this page is not the **Contact Center Voice Media** feature. That feature (see **Tools > Features**) gives automated calls a live two-way audio path, which an AI voice agent needs for a live conversation. The library is part of the base Contact Center and has no feature of its own.
=======
<YouTube video="telephony-recording-storage" title="Video of the voice media master copy and where it is stored" start={112} end={139} />
>>>>>>> d6b323fd5 (Call recordings: say where recordings are stored and how Azure Blob Storage fits, and add the recording storage video)

## Add a clip

<YouTube video="contact-center-voice-media" title="Video of adding a clip" start={61} end={114} />

1. Open **Interaction Center > Management > Voice Media** and click **Add Voice Media**.
2. Enter a **Name** that people will recognize, such as *Main hold music*, and an optional **Description**.
3. Under **Audio**, choose an MP3, WAV, OGG or WebM file of up to 20 MB.
4. Click **Save**. The platform keeps the file as the clip's master copy and sends a copy to the phone provider.

Uploading a new file to an existing clip replaces the old audio everywhere: the old master copy and every provider's copy are removed once the clip is saved. The **Audio** field is disabled when neither media storage nor a phone provider that can host media is available.

If the phone provider does not accept the upload, the clip is still saved with its master copy and you see a warning. The provider receives the clip the first time a call needs it, or when you click **Re-sync media**.

:::note[About the screencast]
The demo site is not connected to a phone provider, so the screencast shows the form without uploading a file.
:::

## Voice media after an export and import

A clip travels in a [deployment plan](administration/import-and-export.md) as its library entry (name, description and format) but the audio does not: the **Contact Center Voice Media Library** step carries no audio. After an import on a site that does not have the file, the clip is listed with its **Master copy** badge, but the audio is not there: open the clip, choose the audio file again and save. The clip keeps its identifier, so the queues, entry points and menus that use it start playing it again. Deleting a clip deletes its master copy and every provider copy, so when two sites share one media store, deleting a clip in one removes the audio the other plays.

<YouTube video="contact-center-configuration-deployment" title="Video of re-uploading the audio of an imported voice media clip" start={207} end={256} />

:::caution[Two things with similar names]
The **Voice Media** screen under **Interaction Center > Management** is the library of hold music and prompts described on this page. The **Contact Center Voice Media** feature on the **Features** page is a different thing: it adds live two-way audio for calls, such as AI voice conversations. You do not need that feature for hold music and recorded prompts.
:::

## What the badges mean

<YouTube video="contact-center-voice-media" title="Video of what the badges mean" start={114} end={152} />

Each clip in the list shows what is stored for it:

| Badge | Meaning |
| --- | --- |
| **Master copy** | The platform holds the audio. The clip plays on any phone provider, and a provider that loses its copy is sent a new one automatically, within about ten minutes. |
| **Provider copy only** | The clip was uploaded before the platform kept master copies, so it exists only at the provider it was uploaded to. It keeps playing there, but it cannot be checked or sent to another provider. |
| **No audio** | The clip has no audio yet. |

A badge with the provider's name shows which providers hold a copy.

## Protect an older clip

A clip marked **Provider copy only** is lost if you change phone provider, or if the provider deletes it. To protect it:

1. Open the clip. The edit form says *This clip exists only at the phone provider. Re-upload this clip to protect it.*
2. Under **Audio**, choose the original file again and click **Save**.

The clip now has a master copy, and queues and IVR menus that use it are unchanged.

## Re-sync a clip with the phone provider

<YouTube video="contact-center-voice-media" title="Video of replacing, protecting and re-syncing a clip" start={152} end={205} />

Click **Re-sync media** on a clip to check that every enabled phone provider still holds it, and to send it to any provider that does not. Use it after you change provider, or when the provider has deleted a clip and you do not want to wait for the clip to be sent again on its own. You see a message for each provider: that it already held the clip, that it received a copy, or that it did not accept one.

The button appears only on clips with a master copy. A clip with no master copy has nothing to send; upload its audio again instead.

## Use a clip

<YouTube video="contact-center-voice-media" title="Video of using a clip" start={205} end={240} />

- On a queue, pick it as **Hold music** on the *While callers wait* card. See [Queues](queues.md#while-callers-wait).
- In an IVR menu, pick it as a menu's **Recorded prompt**. See [IVR menus](entry-points-and-ivr.md#build-an-ivr-menu).

The first time a provider plays a clip it has no copy of, it is sent one from the master copy. A caller may notice a short delay on that first call only.

## Delete a clip

<YouTube video="contact-center-voice-media" title="Video of deleting a clip" start={240} end={277} />

Deleting a clip removes its master copy and every provider's copy. Queues and IVR menus that pointed at it fall back to their spoken text or to silence, so update them first.

## Your voicemail greeting

The greeting you record or upload on **My Voicemail Greeting** is kept the same way: the platform holds the master copy and sends it to whichever phone provider a caller reaches you on. A greeting recorded before master copies were kept shows a notice asking you to record or upload it again to protect it.

:::note[Moving to another site]
Voice media entries travel in deployment plans, but the audio does not. After importing into another site that does not share this site's media storage and phone provider account, open each imported clip and upload its audio again. Because each clip keeps its identifier, the queues and IVR menus that use it start playing the new audio as soon as it is uploaded.
:::
