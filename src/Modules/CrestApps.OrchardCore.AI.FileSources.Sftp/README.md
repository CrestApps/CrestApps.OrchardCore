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

Every SFTP connection names the server's host key fingerprint, as `ssh-keygen -lf` prints it (`SHA256:...`, or the older `MD5:...` form; several can be separated by commas, semicolons or spaces), and the client CrestApps.Core builds trusts the server only when it shows one of those keys (`SftpHostKeys`). Since CrestApps.Core preview.375 it refuses a connection that names no fingerprint, so the editor requires one and accepts exactly what the client matches (`SftpHostKeyFingerprints.IsValid` in `CrestApps.OrchardCore.AI.FileTransfer.Core`, which asks `SftpHostKeys.Matches` itself), and the recipe and deployment steps store it as `HostKeyFingerprint` in `SftpConnectionMetadata`. **Upgrading:** an SFTP connection saved before has no fingerprint and fails to connect until one is added on its editor. Run `ssh-keyscan -t ed25519 sftp.example.com | ssh-keygen -lf -` against a server you trust to read it. The SFTP clients this solution creates itself, such as the agentic commerce feed push, check the server with the same `SftpHostKeys.Require`.

## Stored secrets

The editor never shows the stored password, private key or passphrase and keeps them when the fields are left blank, but only while the connection signs in to the same account on the server the host key identifies. Changing the host, the port, the user name or the host key fingerprint drops all three and asks for a password or a key again (`FileTransferFields.SameAccount` and `SecretDestination`), so a user who may edit the connection but never saw them can't send the password to a server of their own or sign in with the key to another server or account. The host key counts because trusting another key lets a server that stands in for the host receive the password. The proxy password is kept while the proxy's type, host, port and user name are the same. Recipes apply the same rule through `RecipeSecretDestinations`.
## Documentation

See [AI File Sources](../../CrestApps.Docs/docs/ai/file-sources.md) in the technical manual.
