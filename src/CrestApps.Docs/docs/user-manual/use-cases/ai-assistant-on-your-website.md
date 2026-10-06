---
sidebar_label: AI Assistant on Your Website
title: Put an AI Assistant on Your Website
description: Create an AI profile for your visitors, place the chat widget on your public website, and keep an eye on how it is used.
technical_manual:
  - ai/chat
  - ai/chat-analytics
---

## Who it's for and what you get

For **AI content managers** and **administrators** who want visitors to get answers on the company website, any time.

When it is done:

- a floating chat window on your website's pages, answered by an AI profile you control;
- the assistant follows your instructions and, if you attach them, answers from your own documents and data;
- the same kind of assistant can sit on every admin page for your staff, if you want;
- reports show how visitors use the chat.

<video controls preload="metadata" width="100%" aria-label="Screencast of adding the AI chat widget to the website with an always-on layer and chatting with it from the home page">
  <source src="/img/docs/ai-frontend-widget.mp4" type="video/mp4" />
</video>

## Before you start

| You need | Who sets it up |
| --- | --- |
| An AI provider connection and a chat deployment | Your administrator or AI content manager; see [AI connections](../ai/connections.md) |
| The feature **AI Chat**, and Orchard Core's **Widgets** and **Layers** features to place widgets on the site. **AI Chat Session Analytics** for the reports. | Your administrator, in **Tools > Features** |
| Permission to manage AI profiles, and to manage the site's widgets | Your administrator; see [Roles and permissions](../getting-started/roles-and-permissions.md) |

<AskYourAdmin />

## Steps

1. **Create a profile for visitors.** Under **Artificial Intelligence > Profiles**, add a **Chat** profile just for the website, for example *Website Assistant*. Write its instructions for the public: what it may help with, its tone, and what it must not do. See [AI profiles](../ai/profiles.md).
2. **Give it your knowledge (optional).** Attach documents or data sources so it answers from your own content, and limit it to that content if it should not answer anything else. See [Build an AI knowledge assistant](ai-knowledge-assistant.md).
3. **Try it in the admin first.** Chat with the profile from the admin pages and adjust its instructions until you like the answers. See [AI chat](../ai/chat.md).
4. **Place the chat widget.** Add an **Artificial Intelligence Chat** widget to your site's pages, on a layer that is shown wherever the chat should appear, and pick your profile. See [Chat widgets](../ai/chat-widgets.md).
5. **Check the protection settings.** Under **Settings > Artificial Intelligence**, review the prompt security and the limits for anonymous visitors, so the public chat cannot be abused. See [Chat widgets](../ai/chat-widgets.md).
6. **Watch how it is used.** First tick **Enable session metrics** on the profile's **Data Processing & Metrics** tab; it is off by default. Then read the AI chat reports to see how many visitors chat, how long the chats last, and how many were abandoned. See [AI analytics](../ai/analytics.md).
7. **Offer it to staff too (optional).** The **AI Chat Admin Widget** feature puts a floating assistant on every admin page, using the profile you pick. See [Chat widgets](../ai/chat-widgets.md).

## Check that it works

1. Open your public website in a private browser window, so you are a visitor rather than a signed-in user.
2. Open the floating chat window and ask a question your instructions cover. Then ask one they should refuse.
3. Ask a question only your documents can answer, if you attached any, and check the answer and its references.
4. Later, check that your test conversation shows in the AI chat reports.

## Tips

- Use a **separate profile** for the website, so changes for your staff never affect what the public sees.
- Write instructions in plain language and test them with real questions from your customers.
- The widget does not start a conversation just because a page loaded; a session starts when the visitor chats.
- Change one thing at a time, and test again after each change.
