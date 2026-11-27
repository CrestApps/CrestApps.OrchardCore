---
sidebar_label: AI File Sources
sidebar_position: 20
title: AI File Sources
description: Ingest a folder of files on a schedule into a File AI data source, with local file system, FTP, and SFTP connectors.
user_manual:
  - user-manual/ai/knowledge
---

# AI File Sources

| | |
| --- | --- |
| **Feature Name** | AI File Sources |
| **Feature ID** | `CrestApps.OrchardCore.AI.FileSources` |

A **file source** reads a folder on a schedule and ingests every file it finds into a **Files** AI data
source. Each file's text, figures, charts, and tables are stored as separately retrievable knowledge, so an
AI profile attached to that data source can answer from the folder's contents and cite the figure it used.

Where [AI Documents](./documents/) covers files a user uploads through a chat surface, a file source covers
files that already live somewhere — a folder on the server, an FTP drop, an SFTP export — and keeps the
knowledge base in step with them without anyone uploading anything.

<YouTube video="ai-file-sources" title="Video overview of file sources in the admin" />

The video shows an administrator creating the index, a data source and a file source, reading the folder, and asking a question that the files answer.

## Enable the features

1. Enable **AI File Sources** (`CrestApps.OrchardCore.AI.FileSources`). It brings **AI Data Sources** and
   **AI Documents for Chat Interactions** with it, and ships the local file-system connector.
2. Optionally enable a remote connector:
   - **AI File Sources - FTP** (`CrestApps.OrchardCore.AI.FileSources.Ftp`) for FTP and FTPS.
   - **AI File Sources - SFTP** (`CrestApps.OrchardCore.AI.FileSources.Sftp`) for SFTP.

Enabling a connector adds its own section to the file source editor; it does not start an MCP server. The
separate [MCP FTP](./mcp/ftp) and [MCP SFTP](./mcp/sftp) resource features are unrelated to ingestion.

Managing file sources requires the **Manage file sources** (`ManageFileSources`) permission. The screens live
under **Artificial Intelligence** → **File Sources**.

## Create a file source

A file source targets a **File** data source, created under **Artificial Intelligence** → **Data Sources**; the
editor says so when none exists. **Add File Source** under **Artificial Intelligence** → **File Sources** opens the
list of connectors registered by the enabled features (**File system**, **FTP / FTPS**, **SFTP**). The admin
steps are in the User Manual under [File sources](../user-manual/ai/knowledge.md#file-sources).

Every connector shares these fields:

| Field | Purpose |
| --- | --- |
| **Name** | The display name of the source. |
| **Target File data source** | The File AI data source that receives the ingested knowledge. |
| **Enabled** | Whether the scheduled run picks this source up. |
| **Re-read interval (minutes)** | How often the source is re-read. Empty uses the host default (`DefaultRunIntervalMinutes`). |
| **Figures** | `Auto` describes a figure when it looks worth describing, `All` describes every figure that is kept, and `Off` ignores figures entirely. |
| **Figures described per document** | Ceiling on how many figures one document may have described (default `25`). |
| **Vision deployment** | The deployment that describes figures. Empty uses whatever fills the site's `vision` slot. |
| **Utility deployment** | The deployment that answers the utility prompts ingestion runs. Empty uses the site's `utility` slot. |
| **Items per run** | The most files one run may read. Empty uses the host default. |
| **Language** | The BCP-47 language tag the corpus is written in, when it is known and uniform. |

:::note
Figures are only described when a deployment that declares the `imageInput` capability fills the **vision**
slot (or is named on the source). Without one, a file's figures are stored but never described. See
[Model Capabilities](model-capabilities.md).
:::

## The local file system connector

The file-system connector reads a folder on the server. To keep one tenant from reaching another tenant's
files — or any file the host process can open — the folder it may read is **fixed per tenant**:

```
App_Data/Sites/{TenantName}/file-sources
```

That path is computed from the tenant's own identity. It is never read from configuration and never taken
from a request, so a tenant administrator cannot widen their own reach by editing either.

| Field | Purpose |
| --- | --- |
| **Folder** | The folder to read, **relative to the tenant's `file-sources` folder**. Empty means that folder itself. |
| **Include sub-folders** | Whether sub-folders are read too (default on). |
| **Files per listing** | The most files one listing will take on. Empty uses the host default. |

Containment is decided on canonical, fully-resolved paths, so a relative path that climbs out of the tenant
folder is rejected rather than followed.

## FTP and SFTP connectors

Enabling **AI File Sources - FTP** or **AI File Sources - SFTP** adds a connector section for a folder on a
remote server. Neither connector is confined to the tenant folder, because the remote folder is already
scoped by the credentials you supply.

Both connectors share the remote folder fields:

| Field | Default | Purpose |
| --- | --- | --- |
| **Folder** | `/` | The folder on the server to read. |
| **Include sub-folders** | on | Whether sub-folders are read too. |
| **Files per listing** | host default | The most files one listing will take on. |

**FTP** adds the host, port (21 when empty), username, password, encryption mode, data connection type, an
*accept any certificate* switch, connect and read timeouts in milliseconds, and a retry count. Accepting any
certificate, and a user name sent without TLS (an encryption mode other than Explicit or Implicit), are refused unless
the host allows them with `CrestApps_AI:FileTransfer:AllowAnyFtpCertificate` and
`CrestApps_AI:FileTransfer:AllowUnencryptedFtp` in the tenant's configuration; the FTP client checks the same settings
when it connects.

**SFTP** adds the host, port, username, the server's **host key fingerprint**, and either a password or a private
key with an optional passphrase, plus optional proxy settings, a connection timeout, and a keep-alive interval. The
fingerprint is the server's host key fingerprint as `ssh-keygen -lf` prints it (`SHA256:...`, or the older `MD5:...`
form), and several can be given separated by commas, such as while a key is being replaced; the connection
trusts the server only when it shows that key, and a connection without one is refused. An SFTP source saved before
the fingerprint existed fails to connect until one is added.

An FTP or SFTP server, and an SFTP proxy, can't be on the site's own network: a private, loopback, link-local or
shared address, `localhost`, or a name that resolves to one is refused when the source is saved (in the editor, through
the API or a recipe) and again before every connection. A name that doesn't resolve is refused too. An FTP data
connection takes only *EPSV* or *PASVEX*, the passive modes that keep the data channel on the server the connection
names: every other mode, the default *AutoPassive* included, can connect wherever the server's passive answer says. The
value is read the way the FTP client reads it, so `07` or `EPSV,PORT` is refused like *PassiveAllowUnroutable*. An empty
value is saved as *PASVEX*, and a connection saved before this rule is held to *PASVEX* when it connects. A host whose file servers are internal on purpose sets
`CrestApps_AI:FileTransfer:AllowPrivateNetwork` to `true` in the tenant's configuration; link-local addresses and the
cloud metadata services are refused even then. The same rules apply to the FTP and SFTP MCP resources.

The connection then goes to the address that was checked, not to whatever the name resolves to a moment later, so a
name that answers with a public address first and an internal one second can't reach an internal server. An SFTP server
and proxy are connected to by address: the server is verified by its host key, not by its name. So is an FTP server
over plain FTP (anonymous or not), with the *Auto* encryption mode, or with **Accept any certificate**, where the name
proves nothing.

:::warning[Known limit: an FTP server held to its TLS certificate keeps its name]

With the *Explicit* or *Implicit* encryption mode and the certificate checked, the FTP client checks the certificate
against the name it connects with and takes no separate name for the check, so connecting by address would fail every
valid certificate. That connection keeps the name, which the client resolves again. A name that answers differently the
second time reaches no service: the server must present a valid certificate for the name before it receives the
credentials or a command. The TCP connection and the TLS handshake are still made, so keep egress rules on the network
too, refusing private, loopback, link-local and cloud metadata addresses.

:::

A stored password, private key, or passphrase is never sent back to the editor. Leaving the field blank on
save keeps the stored value while the connection signs in to the same account on the same server; type a new one to
replace it. Changing the host, the port or the user name, or for SFTP the host key fingerprint, drops the stored
password, private key and passphrase, so they must be entered again: a user who may edit the source but never saw them
can't send them to a server of their own, or sign in with the key somewhere else. The SFTP proxy password is kept while
the proxy's type, host, port and user name are the same. The `FileSources` recipe step and the `file-sources` API apply
the same rule.

Both feed the same ingestion pipeline as the local connector, so the shared fields above behave identically.

## Scheduling

An hourly Orchard background task, **File Source Ingestion**, evaluates every enabled source and runs the
ones whose interval has elapsed. It runs per tenant, so a source only ever reads and writes within the
tenant that owns it.

Because the task fires hourly, a re-index interval shorter than 60 minutes does not make a source run more
often than once an hour.

You can also run a source immediately with **Read now** in its **Actions** menu on the **File Sources** list,
which ignores the interval. The list shows each source's last run status and counts.

## Configuration

The effort knobs bind from the `CrestApps:AI:FileSources` shell configuration section. In the application's
root `appsettings.json` that section sits under the `OrchardCore` key:

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "FileSources": {
          "MaxItemsPerRun": 200,
          "MaxConcurrentFetches": 2,
          "RunCheckIntervalMinutes": 15,
          "DefaultRunIntervalMinutes": 1440
        }
      }
    }
  }
}
```

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `MaxItemsPerRun` | `int` | `200` | The most items a single run may take on when the source does not set its own limit. |
| `MaxConcurrentFetches` | `int` | `2` | How many files are fetched at a time. |
| `RunCheckIntervalMinutes` | `int` | `15` | Poll interval of the framework's own hosted scheduler. **It has no effect under Orchard Core**, where the hourly background task drives the schedule instead. |
| `DefaultRunIntervalMinutes` | `int` | `1440` | The re-index interval used by a source that names none. |

:::warning
`AllowedLocalRoots` is deliberately **not** configurable per tenant. Whatever configuration supplies is
discarded and replaced with the single tenant folder described above, because a host-wide allow-list is the
wrong shape for a multi-tenant site: a tenant administrator who can edit their own tenant's configuration
would otherwise be able to widen their own reach.
:::

:::note
A tenant that overrides these values in its own `App_Data/Sites/{tenant}/appsettings.json` writes the same
keys **without** the `OrchardCore` wrapper, starting at `CrestApps`, because that file is already scoped to
the tenant.
:::

## Recipes and deployment

The `FileSources` recipe step creates or updates file sources. A file source is matched by its `ItemId`, then by its `DisplayText` within the same connector, so running a recipe twice adds nothing. Its connector (`Source`) must be enabled on the site, and its `AIDataSourceId` names an AI data source, so import the data sources first.

```json
{
  "steps": [
    {
      "name": "FileSources",
      "Sources": [
        {
          "ItemId": "4fs00000000000000000000001",
          "Source": "Sftp",
          "DisplayText": "Policies",
          "AIDataSourceId": "4ds00000000000000000000001",
          "Enabled": true,
          "ReindexIntervalMinutes": 60,
          "Properties": {
            "SftpConnectionMetadata": { "Host": "sftp.example.com", "Username": "reader" },
            "RemoteFileSourceMetadata": { "RootPath": "/policies", "Recursive": true }
          }
        }
      ]
    }
  ]
}
```

The **File sources** deployment step (Artificial Intelligence category) exports every file source. Passwords, private keys, passphrases, tokens and other secrets are never exported: they are protected with the keys of the site that stored them. When a recipe updates a file source and leaves a secret out, the stored secret is kept, unless the same update changes the connection's host, port, user name or host key fingerprint; after importing a new FTP or SFTP file source, open it and enter its password or key.

## Related

- [AI Data Sources](./data-sources/) — the data source the ingested knowledge lands in.
- [AI Documents](./documents/) — user-uploaded files on a chat surface.
- [Model Capabilities](model-capabilities.md) — the `vision` and `utility` slots ingestion draws on.
