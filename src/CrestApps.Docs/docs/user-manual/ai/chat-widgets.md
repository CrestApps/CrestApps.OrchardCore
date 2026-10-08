---
sidebar_label: Chat Widgets
title: Chat Widgets for the Admin and Your Website
description: Turn on the floating AI chat widget on admin pages, add an AI chat box to your public website, and learn what staff and visitors see.
technical_manual:
  - ai/chat
---

A chat widget is a small chat window that floats in the corner of the page. The **admin chat widget** gives your staff an assistant on every admin page, so they can ask a question without leaving what they are doing. The **website chat widget** puts an assistant on your public site, for example to answer visitors' questions about your products or opening hours.

| | |
| --- | --- |
| **Menu** | Settings > Artificial Intelligence, **Admin Widget** section (admin widget); Design > Widgets (website widget) |
| **Permission** | Manage AI profiles (admin widget settings); Manage layers (website widget); Query any AI profile, or Query AI profile - *profile name*, to chat |
| **Feature** | AI Chat Admin Widget (admin widget); AI Chat with Widgets and Layers (website widget) |

<AskYourAdmin />

Both widgets chat with an [AI profile](profiles.md) of type **Chat**. Create the profile first; everything you set on it (instructions, chat mode, file uploads, read-aloud, ratings) applies inside the widget too.

## Turn on the admin chat widget

<video controls preload="metadata" width="100%" aria-label="Screencast of enabling the admin widget, creating an all-tools profile, and using the floating admin assistant">
  <source src="/img/docs/ai-admin-widget.mp4" type="video/mp4" />
</video>

1. Ask your administrator to turn on the **AI Chat Admin Widget** feature.
2. Open **Settings > Artificial Intelligence** and scroll to the **Admin Widget** section.
3. Fill in the fields below and click **Save**.

| Field | What it does |
| --- | --- |
| **Chat profile** | The chat profile the widget talks to. Choose **No profile (disabled)** to hide the widget completely. |
| **Max history sessions** | How many past chats the widget's history list shows, from 1 to 50. Default 10. |
| **Primary color** | The color of the widget's header and button. Default `#41b670`. |

**Max history sessions** and **Primary color** appear once you pick a profile.

The widget shows on every admin page for each signed-in user who is allowed to use that profile.

:::tip
An admin assistant becomes much more useful when it can act on the site, for example list content or check a setting. Give its profile tools on the **Capabilities** tab. See [Tools and agents](tools-and-agents.md).
:::

## Use the admin chat widget

- Click the round robot button (**Open AI Assistant**) in the bottom-right corner to open the chat. Click the **x** to close it.
- Type your question and press **Enter**. The widget has the same message box as the chat page, including attachments, voice, read-aloud, copy and ratings when the profile allows them. See [Chat with an AI assistant](chat.md).
- In the header, click **New chat** to start over, or **Chat history** to list your recent chats and reopen one.
- Drag the header to move the window, and drag its edge to resize it. **Restore size** puts it back to its normal size. You can drag the round button too.

The widget remembers its position, its size, whether it was open and your current chat in your browser, so it looks the same on the next page.

## Add a chat box to your website

<video controls preload="metadata" width="100%" aria-label="Screencast of adding the website chat widget with an always-on layer">
  <source src="/img/docs/ai-frontend-widget.mp4" type="video/mp4" />
</video>

The website widget is a normal Orchard Core widget called **Artificial Intelligence Chat**, so you place it like any other widget.

1. Create a chat profile for the public, for example *Website assistant*. The **Website assistant** starting point in the **New AI Profile** picker is made for this. See [Create a profile](profiles.md#create-a-profile).
2. Open **Design > Widgets**.
3. Make sure there is a layer that is active on the pages where the chat should appear. To show it on every page, use a layer whose rule is a **Boolean** condition set to true. Many sites already have one, often named *Always*.
4. In the zone where the widget should live, for example **Footer**, click **Add Widget** and choose **Artificial Intelligence Chat**.
5. Fill in the fields below, pick the layer, and publish the widget.
6. Open a page of your website and click the chat button to try it.

| Field | What it does |
| --- | --- |
| **Profile** | The chat profile the widget talks to. Required. |
| **Total history chat to show** | How many past chats a signed-in visitor can reopen from the widget. Set it to 0, or leave it blank, to hide the history. |

The widget picks up your theme's main color.

:::note[Visitors need permission]
Visitors who are not signed in use the **Anonymous** role. The widget only shows to people whose role may use the profile, so give the **Anonymous** role the **Query AI profile - *profile name*** permission for the public profile under **Access Control > Roles**. Grant only that one profile, not **Query any AI profile**.
:::

## What visitors see

- A round chat button in the bottom-right corner (**Open AI Chat**). Clicking it opens a chat window titled **AI Chat**.
- The welcome message of the profile, then a message box. Visitors type a question and get an answer as it is written.
- The same extras as the admin chat, when the profile allows them: attaching files, voice, read-aloud, copy, ratings and numbered sources.
- **New chat** to start over. Signed-in visitors also get **Chat history** to reopen past chats, when **Total history chat to show** is above 0.

A chat is saved only once the visitor sends a first message, never just because someone opened a page.

## Protect a public assistant

A public assistant is open to anyone, so the site protects it:

- Each visitor can only send a limited number of messages and start a limited number of chats in a short time. A visitor who goes over the limit sees "You've reached the limit for starting new chats. Please wait a few minutes and try again."
- Messages that try to trick the assistant into ignoring its instructions are blocked.
- A visitor is recognized across visits by a cookie, for the analytics and the limits above.

Your administrator can tune these protections under **Settings > Artificial Intelligence** in the **Prompt Security** and **Visitor Identity** sections, and per profile on the **Prompt Security** tab of the profile editor. The settings are explained in the [Technical Manual](../../ai/chat.md#security-and-visitor-identity-settings).

:::tip
Keep a public assistant focused. Give it clear instructions about what it may talk about, and turn on **Restrict answers to retrieved data only** when it should answer only from your own content (see [Knowledge](knowledge.md)).
:::
