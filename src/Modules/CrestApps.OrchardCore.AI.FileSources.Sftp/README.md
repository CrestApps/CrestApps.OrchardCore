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

Every SFTP connection names the server's SHA-256 host key fingerprint, as `ssh-keygen -lf` prints it (`SHA256:...`), and the client CrestApps.Core builds trusts the server only when it shows that key. Since CrestApps.Core preview.375 it refuses a connection that names no fingerprint, so the editor requires one and checks its form (`SshHostKeyFingerprints.IsValid`), and the recipe and deployment steps store it as `HostKeyFingerprint` in `SftpConnectionMetadata`. **Upgrading:** an SFTP connection saved before has no fingerprint and fails to connect until one is added on its editor. Run `ssh-keyscan -t ed25519 sftp.example.com | ssh-keygen -lf -` against a server you trust to read it. `SshHostKeyFingerprints` (in `CrestApps.OrchardCore.Core`) is the same check for the SFTP clients this solution creates itself, such as the agentic commerce feed push.
## Documentation

See [AI File Sources](../../CrestApps.Docs/docs/ai/file-sources.md) in the technical manual.
