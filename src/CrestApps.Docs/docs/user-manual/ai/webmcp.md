---
sidebar_label: WebMCP
title: "WebMCP: AI Agents in the Browser"
description: Let AI assistants in the web browser use this site's AI tools and the page you have open, and let the site's own chat work with your page.
technical_manual:
  - ai/mcp/webmcp
---

Some web browsers now come with an AI assistant, and some people add one as a browser extension. **WebMCP** lets such an assistant work with this site the way you do: it can use the AI tools your administrator picks, read the page you have open, open another page of the site and fill in a form for you to review. It works as you, with your permissions, and only while you are signed in. The site's own AI chat can use the same page tools, so you can ask it to fill in the form in front of you.

| | |
| --- | --- |
| **Menu** | Settings > Artificial Intelligence, **WebMCP** card; Artificial Intelligence > Profiles, **Capabilities** tab |
| **Permission** | Use WebMCP tools in the browser; Manage the WebMCP settings (to change the settings); Access any AI tool, or Access AI tool - *tool name*, for each tool |
| **Feature** | WebMCP Tools; WebMCP Page Tools (page tools); WebMCP Chat Bridge (page tools in the site's chat) |

<AskYourAdmin />

:::note[Which browsers]
WebMCP is new. Only some browsers support it, and some only as a trial. In a browser without it, nothing changes on the page. The site's own chat can still use the page tools in every browser.
:::

## Choose what browser assistants can use

1. Open **Settings > Artificial Intelligence** and find the **WebMCP** card.
2. Choose where tools are offered:
   - **Offer tools on admin pages** is on at first.
   - **Offer tools on front-end pages** is off at first. Turn it on to offer tools on the site's public pages too. Visitors who are not signed in never get tools.
3. Pick the tools. Tick the tools and tool instances an assistant may use, or turn on **Expose all tools** to offer every one of them. At first, none is offered.
4. Click **Save**.

| Field | What it does |
| --- | --- |
| **Offer tools on admin pages** | Admin pages offer tools to users who have the **Use WebMCP tools in the browser** permission. |
| **Offer tools on front-end pages** | The site's public pages also offer tools to signed-in users who have the permission. |
| **Expose all tools** | Every tool and tool instance in the lists is offered, and the selection is ignored. |
| Tool and tool instance lists | The tools an assistant may use. The lists show only the tools you are allowed to use yourself. |

Even when a tool is offered, each person can only use the tools their role allows (**Access any AI tool**, or **Access AI tool -** *tool name*). An assistant can never do more than the person using it.

## Page tools

With the **WebMCP Page Tools** feature on, every page that offers tools also offers these:

| Tool | What it does |
| --- | --- |
| Get page info | Tells the assistant the page's title, address, description and main headings. |
| List page links | Lists the links in the page's menus that stay on this site. |
| Open page | Opens another page of this site. It cannot leave the site. |
| List form fields | Lists the fields of the page's forms and what they hold. Passwords, hidden fields and file uploads are never listed. |
| Set form field | Puts a value into one field. It never saves or submits the form: you check the change and click **Save** yourself. |

## Let the site's chat use the page you have open

With the **WebMCP Chat Bridge** feature on, you can let an AI profile's chat use the page tools of the page you have open, for example to ask the admin chat widget to fill in the form you are editing.

1. Open **Artificial Intelligence > Profiles** and edit a chat profile.
2. On the **Capabilities** tab, tick **Allow page tools**.
3. Click **Save**.

The chat uses the tab you looked at last. It only works for signed-in users with the **Use WebMCP tools in the browser** permission.

## Tips

- Offer only the tools you would let the person run themselves, and prefer tools that only read.
- If an assistant says a tool is not available, check that the tool is ticked on the **WebMCP** card and that the person's role allows that tool.
- If the chat cannot reach the page, keep the page open in the tab you looked at last. A page tool that does not answer within a minute fails.
