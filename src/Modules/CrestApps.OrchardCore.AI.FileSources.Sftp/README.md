# AI File Sources - SFTP

Adds an SFTP connector to File Sources, so a folder on an SFTP server can be ingested into a File AI data source.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| AI File Sources - SFTP | `CrestApps.OrchardCore.AI.FileSources.Sftp` | Adds an SFTP connector to File Sources, so a folder on an SFTP server can be ingested into a File AI data source. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.AI.FileSources`](../CrestApps.OrchardCore.AI.FileSources/README.md)

## Host keys

The SFTP client CrestApps.Core builds does not check the server's host key yet. `SftpHostKeys` (in `CrestApps.OrchardCore.AI.FileTransfer.Core`) is the shared check for every SFTP client this solution creates: it trusts a server only when its SHA-256 host-key fingerprint is the configured one, and refuses every server when none is configured.

## Documentation

See [AI File Sources](../../CrestApps.Docs/docs/ai/file-sources.md) in the technical manual.
