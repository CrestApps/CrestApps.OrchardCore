---
sidebar_label: MCP
title: Model Context Protocol (MCP)
description: Bring the tools of outside MCP servers to your AI assistants, and share your site's tools, prompts and files with other AI apps.
technical_manual:
  - ai/mcp/index
  - ai/mcp/client
  - ai/mcp/server
  - ai/mcp/ftp
  - ai/mcp/sftp
---

The **Model Context Protocol (MCP)** is a common standard for sharing tools and data with AI. It works in two directions:

- **Use outside tools.** An **MCP connection** brings the tools of an outside MCP server, such as a time-zone service or your company's internal tools, into the site. The assistants you choose can then use them.
- **Share your own.** The **MCP Server** lets other AI apps, such as a desktop AI assistant, use the tools you select on this site. It can also offer **prompts** (ready-made instructions) and **resources** (files and data an app can read).

| | |
| --- | --- |
| **Menu** | Artificial Intelligence > Model Context Protocol > MCP Hosts, Prompts, Resources; Settings > Artificial Intelligence (the **MCP Server** section) |
| **Permission** | Manage MCP Connections; Manage MCP Prompts; Manage MCP Resources; Manage the MCP Server settings; Access the MCP Server (for apps that connect) |
| **Feature** | Model Context Protocol (MCP) Client; Model Context Protocol (MCP) Local Client; Model Context Protocol (MCP) Server; Model Context Protocol (MCP) FTP Resource; Model Context Protocol (MCP) SFTP Resource |

<AskYourAdmin />

<YouTube video="ai-mcp" title="Video overview of the Model Context Protocol" />

The video covers why a common standard for AI tools helps, how the MCP client and server fit together, and each task below except sharing resources, which has [its own video](#share-resources).

Each part is its own feature, so you only see the menus of the features that are turned on: **MCP Hosts** comes with the client, and **Prompts** and **Resources** come with the server.

## Connect to an MCP server

<YouTube video="ai-mcp" title="Video of adding an MCP connection" start={96} end={151} />

1. Open **Artificial Intelligence > Model Context Protocol > MCP Hosts** and click **Add Connection**.
2. In **Available Sources**, click **Add** on the kind of server:
   - **Server-Sent Events**: a server on the web.
   - **Standard Input/Output**: a program that runs on the site's own server (see [below](#connect-to-a-local-program)).
3. Enter a **Title** and the **Endpoint**, which is the address of the server.
4. Choose the **Authentication** (see [Choose how to sign in](#choose-how-to-sign-in)).
5. Click **Save**.

The site does not contact the server when you save. A wrong address or key shows up the first time an assistant uses the connection, so try each new connection with an assistant before you rely on it.

The list shows each connection with its **Connection type** and when it was **Last modified**. Use **Edit** to change one, and **Delete** to remove it.

## Connect to a local program

<YouTube video="ai-mcp" title="Video of adding a local program connection" start={151} end={192} />

A **Standard Input/Output** connection starts a program on the site's own server instead of calling an address. It is available only when the **Model Context Protocol (MCP) Local Client** feature is on. Because it runs a program on your server, turn it on only when your technical team asks for it.

| Field | What it does |
| --- | --- |
| **Title** | The name shown in the list and on profiles. |
| **Command** | The command that starts the program. |
| **Command arguments** | Optional arguments for the command. |
| **Working directory** | Optional folder the program runs in. |
| **Environment variables** | Optional values the program needs. |

Your technical team provides the values for these fields.

## Choose how to sign in

<YouTube video="ai-mcp" title="Video of the authentication options of a connection" start={192} end={234} />

The **Authentication** list of a **Server-Sent Events** connection offers:

| Option | Use it when |
| --- | --- |
| **Anonymous (No Authentication)** | The server is open to everyone or sits on a private network. |
| **API Key** | The server wants a key in a header. Set the **Header Name** (it defaults to `Authorization`), an optional **Key Prefix** such as `Bearer`, and the **API Key**. |
| **Basic Authentication** | The server wants a **Username** and **Password**. |
| **OAuth 2.0 Client Credentials** | The site gets a token from a **Token Endpoint** with a **Client ID** and **Client Secret**, and optional **Scopes**. |
| **OAuth 2.0 + Private Key JWT** | The same, signing in with a **Private Key (PEM)** and an optional **Key ID (kid)**. |
| **OAuth 2.0 + Mutual TLS (mTLS)** | The same, signing in with a **Client Certificate (Base64 PFX)** and an optional **Certificate Password**. |
| **Custom Headers (Advanced)** | The server asks for other headers, which you list under **Additional headers**. |

Secrets are stored securely and never shown again. When you edit a connection, leave a secret field blank to keep the stored value.

## Give an assistant the tools

<YouTube video="ai-mcp" title="Video of giving an assistant the tools of a connection" start={234} end={267} />

A connection does nothing until an assistant uses it:

1. Open **Artificial Intelligence > Profiles** and edit the profile.
2. On the **Capabilities** tab, tick the connection under **MCP Connections**.
3. Click **Save**.

The assistant now sees the tools of that server and decides by itself when to call them. Profile templates and chat interactions have the same list (in a chat interaction it is called **Connections**).

:::caution[Tools really act]
A tool can change things on the other system. Give a connection only to the assistants that should be able to use it, and check what the server's tools do first.
:::

## Share your tools with other AI apps

<YouTube video="ai-mcp" title="Video of the MCP Server settings" start={267} end={324} />

With the **Model Context Protocol (MCP) Server** feature on, apps outside the site can use the tools you choose:

1. Open **Settings > Artificial Intelligence** and find the **MCP Server** section.
2. Choose the **Authentication type**: **OpenID Connect**, **API key** or **None (Anonymous access)**. Use **None** only for testing.
3. With **API key**, enter the key that apps must send. With **OpenID Connect**, tick **Require access permission** to also ask for the **Access the MCP Server** permission.
4. Under **Tools** (and **Tool Instances**, when that feature is on), tick what to share, or use **Select All Tools**. Nothing is shared until you pick it.
5. Click **Save**. The site reloads its settings.

**Expose all tools** shares every tool and tool instance and ignores your selection. Use it only when you trust every app that connects. You need the **Manage the MCP Server settings** permission. How outside apps connect is described in the [Technical Manual](../../ai/mcp/server.md).

## Share prompts

<YouTube video="ai-mcp" title="Video of adding an MCP prompt" start={324} end={379} />

A **prompt** is a ready-made instruction that other AI apps can offer to their users, with blanks to fill in.

1. Open **Artificial Intelligence > Model Context Protocol > Prompts** and click **Add Prompt**.
2. Enter a **Title**, a unique **Name** (other apps ask for the prompt by it), and a **Description** that says what it does.
3. Under **Arguments**, click **Add Argument** for each blank. Give it a **Name**, and optionally a **Title** and **Description**, and tick **Required** when the app must supply it.
4. Under **Messages**, click **Add Message**. Choose a **Role**, **User** or **Assistant**, and write the **Content**. An argument's name in double curly braces is replaced by the value the app supplies.
5. Click **Save**.

## Share resources

A **resource** is a file or other data that an app can read. With a resource, the app asks your site for the file when it needs it, so it always reads the current version instead of a pasted copy.

<YouTube video="ai-mcp-resources" title="Video overview of MCP resources" />

The video shows what resources are, the types below, and how to add a File, a Media, an SFTP and an FTP resource.

### The resource types

<YouTube video="ai-mcp-resources" title="Video of the resource types" start={57} end={99} />

Open **Artificial Intelligence > Model Context Protocol > Resources** and click **Add Resource**. **Available Resource Types** lists:

- **File** reads from the site's file providers.
- **Media** shares files of the media library.
- **FTP/FTPS** and **SFTP** read from a file server. They are available when the FTP or SFTP resource feature is on.
- **Recipe**, **Recipe Schema** and **Recipe Step Schema** share the site's recipes with developer tools. You do not need them for everyday documents.

### Share a file

<YouTube video="ai-mcp-resources" title="Video of adding a File resource" start={99} end={142} />

1. Click **Add** on **File**.
2. Enter a **Display Text** (the name in the list) and a **Name** (how apps identify the resource).
3. Add a **Description** that says what the file holds. AI apps read it to decide whether the resource is useful.
4. Leave the **MIME Type**, which tells the app what kind of content it is, on `text/plain` or change it to match the file.
5. Enter the **Path**. The **Full URI** line under the field shows the address an app uses. A placeholder in curly braces, such as `{fileName}`, turns one resource into many: the app fills in the file it wants.
6. Click **Save**. The resource is listed with its type.

### Share media

<YouTube video="ai-mcp-resources" title="Video of adding a Media resource" start={142} end={172} />

Click **Add** on **Media** and fill in the same fields. In the **Path**, the placeholder `{path}` stands for the location of a file in the media library, so an app can ask for any file. To share a single file instead, replace the placeholder with its location. Click **Save**.

### Share files from a file server

<YouTube video="ai-mcp-resources" title="Video of adding SFTP and FTP resources" start={172} end={234} />

1. Click **Add** on **SFTP**, then enter a **Display Text**, a **Name** and a **Description**.
2. Enter the **Host**, the **Port** (22 by default) and the **Username** of the file server.
3. Sign in with a **Password**, or with a **Private Key** and its **Passphrase**. Both are stored securely and never shown again.
4. **Proxy Settings**, a **Connection Timeout (seconds)** and a **Keep-Alive Interval (seconds)** are optional, and help with firewalls and slow networks.
5. Click **Save**.

An **FTP/FTPS** resource has the same **Host**, **Port**, **Username** and **Password**, plus an **Encryption Mode** (**None (Plain FTP)**, **Explicit (STARTTLS)**, **Implicit (Port 990)** or **Auto Detect**), a **Data Connection Type**, **Accept Any Certificate** and timeouts. Saving only stores the settings, so try each resource from an AI app before you rely on it.

## Use cases

- **Live facts.** Connect a time-zone, weather or ticket service so a support assistant answers from current information.
- **Company tools.** Give your assistants the tools your technical team already built, through one connection.
- **Other AI apps.** Let staff use their favorite AI app with the tools of your site.
- **Shared wording.** Publish the prompts every team should use, so everyone asks in the same way.

## Keep it safe

- Share only the tools you are comfortable having called. Prefer **API key** or **OpenID Connect** for the server, and keep **None** for testing.
- Start small: add one connection, give it to one assistant, and check the answers before you offer it more widely.
- Give the **Manage MCP ...** permissions only to the roles that need them.

## Related

- [Tools, agents and connections](tools-and-agents.md): built-in tools, tool instances, agents and A2A connections.
- [Profiles](profiles.md): the **Capabilities** tab.
- [Technical Manual: MCP](../../ai/mcp/index.md)
