---
sidebar_label: Chat with an Assistant
title: Chat with an AI Assistant
description: Start a chat with an AI assistant in the admin, find and delete past chats, attach files, talk instead of type, listen to answers and check the sources.
technical_manual:
  - ai/chat
  - ai/realtime-voice
  - ai/documents/index
---

Every AI assistant on the site is an [AI profile](profiles.md). The chat page lets you ask that assistant questions in the admin and keeps your past conversations so you can pick them up later. Use it for an internal helper, for example one that knows your policies or your product catalog.

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > *the assistant's name* |
| **Permission** | Query any AI profile, or Query AI profile - *profile name* for one assistant |
| **Feature** | AI Chat |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a chat AI profile and chatting with the model">
  <source src="/img/docs/ai-chat.mp4" type="video/mp4" />
</video>

## Open an assistant

An assistant appears under **Artificial Intelligence** in the admin menu, by its name, when its profile has **Show on admin menu** turned on. Click its name to open a new chat.

If you manage profiles, you can also open **Artificial Intelligence > Profiles**, open the **Actions** menu of a chat profile, and choose:

| Action | What it does |
| --- | --- |
| **New chat** | Opens a new, empty chat with the assistant. |
| **View chat history** | Lists your past chats with the assistant. |
| **Delete chat history** | Deletes all past chats with the assistant, after you confirm. |

:::tip
You can also chat from any admin page with the floating admin chat widget, and visitors can chat on your website. See [Chat widgets](chat-widgets.md).
:::

## Start a chat

1. Open the assistant. An empty chat shows the assistant's welcome message, or "What do you want to know?".
2. Type your question in the message box and press **Enter**, or click the send arrow. Press **Shift+Enter** to start a new line without sending.
3. The answer appears as it is written. To stop a long answer, click the stop button that replaces the send arrow while the assistant is writing.
4. Keep asking follow-up questions. The assistant remembers what was said earlier in the same chat.

The address in your browser changes to include the chat, so you can bookmark it or come back to it later.

Each answer has buttons underneath it:

| Button | What it does |
| --- | --- |
| **Copy** | Copies the answer, with its list of sources, to the clipboard. Code in an answer has its own **Copy code** button. |
| **Read aloud** | Reads the answer out loud. Click it again to pause. Shown when the profile has read-aloud turned on. |
| **Thumbs up** / **Thumbs down** | Rates the chat. Click the same thumb again to remove your rating. Shown when your administrator collects chat analytics for this assistant. |

If the assistant creates a picture or a chart, it has a **Download** button.

### Ready-made prompts

Your site may have ready-made prompts, such as "Summarize this conversation". They are AI profiles of type **Template generated prompt** (see [AI profiles](profiles.md#profile-types)). When there are any, a wrench button sits beside the message box. After the chat has started, click the wrench and pick a prompt: the assistant runs it against the current chat and adds the answer to it.

## Find a past chat

- The page shows your recent chats in a list on the right (on a wide screen). Click a title to reopen that chat and continue it.
- Click **Chat History** at the top of the page to see all your chats with the assistant. Type part of a title in **History search** and click **Search** to find one, then click **View** to open it.
- Click **New Chat** to start over with an empty chat.

Chat titles are created automatically from the conversation; you cannot rename them. A chat with no title shows as *Untitled*.

You only see your own chats. A chat that was held automatically, such as an AI text conversation with a customer, opens read-only with the note "This conversation was handled automatically and is shown read-only."

## Delete chats

- To delete one chat, open **Chat History** and click **Delete** on its row, then confirm. You need the **Delete chat session** permission.
- To delete all your chats with an assistant, click **Delete Chat History** on the **Chat History** page and confirm. You need the **Delete all chat sessions** permission. After deleting, a new empty chat opens.

Deleting a chat also deletes the files you attached to it.

## Attach files to a chat

When the assistant allows it, you can attach documents or pictures and ask about them, for example "What is the cancellation fee in this contract?".

<video controls preload="metadata" width="100%" aria-label="Screencast of uploading a PDF to a chat and receiving an answer that cites the file">
  <source src="/img/docs/ai-chat-attachments-pdf.mp4" type="video/mp4" />
</video>

1. Click **Attach files** (the paperclip) above the message box, or drag files onto the message box.
2. Each file shows as a small label while it uploads. Click the **x** on a label to remove that file.
3. Ask your question and send it. The assistant reads the file to answer.

The note **Supported formats** under the button lists the file types you can attach, such as PDF, Word, PowerPoint, text and Markdown. Pictures are listed only when the assistant can read images.

Files you attach belong to that chat only. They are not shared with other chats or other users. If the paperclip is missing, the profile does not allow uploads; ask the person who manages the assistant.

The screencast above uses a [chat interaction](chat-interactions.md); attaching works the same way in a chat.

## Talk instead of type

Depending on the profile's **Chat mode**, the message box can have voice buttons.

### Dictate a message

With the **Audio input** chat mode, a microphone button (**Voice input**) sits beside the message box.

1. Click the microphone and allow the browser to use it if asked.
2. Speak. Your words appear in the message box as you talk.
3. Click the stop button when you are done.
4. Check or correct the text, then send it as usual.

If transcription fails, recording stops and the microphone button resets so you can try again.

### Have a spoken conversation

With the **Conversation** chat mode, a sound-wave button (**Start Conversation**) sits beside the message box.

1. Click the sound-wave button to start talking with the assistant. While the conversation runs, the message box gives way to an **End Conversation** button.
2. Speak naturally. When you finish a sentence, it appears in the chat as your message and is sent for you.
3. The assistant answers in text and speaks the answer at the same time.
4. To interrupt, just start talking. The assistant stops and listens to you.
5. Click **End Conversation** to stop. The message box comes back, and anything you type continues the same chat.

Spoken and typed turns all end up in the same chat. Sending a typed message ends a live conversation first.

:::tip
Use headphones during a spoken conversation, so the assistant does not hear its own voice through your speakers.
:::

On some sites the conversation runs on a live voice model. Then a **Voice settings** gear lets you pick your **Microphone**, **Speaker**, **Assistant volume** and **Language**, and turn **Allow interruptions** and **Push-to-talk** on or off. With push-to-talk, hold the button or the Space bar while you speak. These preferences are saved in your browser.

### Listen to an answer

When the profile has read-aloud turned on, each answer has a **Read aloud** button. Click it to hear the answer; click it again to pause. Starting another answer stops the one that is playing. During a spoken conversation the button is hidden, because the assistant already speaks.

## Check the sources

When the assistant answers from your documents or knowledge base, the answer shows small numbers, such as ¹ and ², after the statements that came from a source. Point at a number to see the source's name.

A numbered list of sources appears under the answer. Click a source to open it in a new tab. Sources without a web address are listed as plain text.

## Try out a utility or agent profile

Profiles of type **Utility** or **Agent** do not have a chat page. To check how one answers:

1. Open **Artificial Intelligence > Profiles**.
2. Open the profile's **Actions** menu and choose **Invoke Profile**.
3. Type a message and send it.

Only the latest answer is shown, and nothing is saved as a chat. Use it to test a profile's instructions or tools after a change.

## When something goes wrong

| Message or problem | What to do |
| --- | --- |
| The assistant is not in the menu | Its profile does not have **Show on admin menu** turned on, or you lack permission to use it. Ask your administrator. |
| "You are not authorized to interact with the given profile." | You need the permission for this assistant. Ask your administrator. |
| "You've reached the limit for starting new chats. Please wait a few minutes and try again." | You started too many chats in a short time. Wait a few minutes. |
| The answer is vague, or the assistant says it cannot do something it should | The assistant may use tools you are not allowed to use. Ask your administrator to check your role's AI tool permissions (see [Tools and agents](tools-and-agents.md#who-can-use-tools)). |
