---
sidebar_label: Profile Builder
title: Profile Builder
description: Build an AI profile by playing the person it will talk to and telling the AI what it got wrong; it rewrites its own instructions until it behaves the way you want.
technical_manual:
  - ai/profile-builder
---

The **Profile Builder** helps you write the instructions for an AI profile without writing them yourself. In a chat, you play the person the AI will talk to: a customer, a patient, a lead, whoever it serves. When the AI answers in a way you do not like, you say what was wrong, and the AI rewrites its own instructions. When it behaves the way you want, you save the result as an [AI profile](profiles.md).

It suits profiles that hold a conversation on their own, such as automated text messages or phone calls, where you need to know how the AI will react before a real person talks to it.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Profile Builder |
| **Permission** | Manage AI profiles; Edit own chat interactions |
| **Feature** | AI Profile Builder |

<AskYourAdmin />

## Start a session

1. Open **Artificial Intelligence > Profile Builder**.
2. Click **New session** to start empty, or choose a profile under **Improve an existing AI profile** and click **Start** to begin from its instructions and settings.

The session page has three parts: the **Settings** on the left, the chat in the middle, and the feedback panel on the right.

In **Settings**, choose the **Chat deployment** (the model to test) and, if you like, write a first version of the **System instructions**. Tools, knowledge and other settings work as in a [chat interaction](chat-interactions.md).

## Role-play and give feedback

1. Type what that person would say and send it. The AI answers.
2. Under a reply, click **Feedback**. The reply is highlighted and shown in the feedback panel.
3. In **Your feedback**, write what the AI should have done differently, for example *"Too long. Keep text messages under two sentences"* or *"Never quote a price; offer to connect them with sales"*.
4. Click one of:
   - **Feedback & regenerate**: rewrites the instructions, removes the reply and answers your message again with the new instructions. Use it on the latest reply.
   - **Feedback only**: rewrites the instructions and keeps the reply.

After a moment the instructions in **Settings** change, and the panel says what was changed and why.

When a reply is just right, click **Looks good** under it. The AI keeps that behavior in mind when it changes its instructions later.

:::tip[Keep going until it holds]
If the regenerated reply is still wrong, give feedback again. The AI considers every comment you gave on that message and on earlier ones, so fixing one thing does not undo another.
:::

You can also edit the instructions in **Settings** yourself at any time. Your edit is kept as its own revision.

### Voice

If the chat deployment has a conversation deployment, you can role-play by voice. Feedback works on spoken replies too, but they cannot be regenerated, because they were already heard. After feedback, end the voice conversation and start it again to use the new instructions.

## Revisions

The **Revisions** tab lists every version of the instructions, with what changed and why. Expand **System message** to read a version, and click **Restore** to make it the current one again.

## Check that earlier fixes still work

On the **Replay** tab, click **Run replay**. Every message you gave feedback on is answered again with the current instructions, and each answer is marked:

- **Pass**: the answer respects your feedback.
- **Fail**: the answer repeats a mistake you pointed out.
- **Unclear**: your feedback was too vague to judge.

Tools are not used during a replay, so nothing is sent or booked.

## Save the result

On the **Finish** tab:

- **Opening message**: what the AI is told when it starts a conversation itself, such as an automated text. Click **Try the opening** to see how it opens, and **Save** to keep it. It becomes the profile's opening message.
- **Coach deployment**: the model that reads your feedback and rewrites the instructions. A capable model writes better instructions. By default it is the chat deployment.
- **Copy system message** copies the instructions.
- **Update the AI profile** saves the result to the profile the session started from.
- **Save as a new AI profile**: enter a **Title** and **Technical name**, then click **Create AI profile**. The new profile opens in the editor.

The session stays in the list, so you can come back to it later.
