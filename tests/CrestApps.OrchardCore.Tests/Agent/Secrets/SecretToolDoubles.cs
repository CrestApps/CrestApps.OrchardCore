using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.Tests.Agent.Secrets;

/// <summary>
/// Stands in for the Secrets services in a tool test: secrets kept in memory in three stores (two writable, one
/// read-only) behind the secret manager and the store operations service, and a clock. Every value of every secret
/// starts with <see cref="Canary"/>, so a test can tell that no tool result carries one.
/// </summary>
internal sealed class SecretToolDoubles
{
    /// <summary>
    /// The text every secret value of the fixture starts with.
    /// </summary>
    public const string Canary = "CANARY";

    /// <summary>
    /// The current time of the fixture.
    /// </summary>
    public static readonly DateTime UtcNow = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    public SecretToolDoubles()
    {
        Database = new InMemorySecretStore("Database", isReadOnly: false);
        KeyVault = new InMemorySecretStore("AzureKeyVault", isReadOnly: false);
        Configuration = new InMemorySecretStore("Configuration", isReadOnly: true);

        Database.Add(
            "Smtp.Password",
            new TextSecret { Text = $"{Canary}-SMTP-PASSWORD" },
            "The password of the SMTP account.",
            expiresUtc: UtcNow.AddDays(10));

        Database.Add(
            "OpenId.SigningKey",
            new RsaKeySecret
            {
                PublicKey = "PUBLIC-KEY",
                PrivateKey = $"{Canary}-RSA-PRIVATE-KEY",
                IncludesPrivateKey = true,
            },
            "Signs the tokens.",
            expiresUtc: UtcNow.AddDays(-5));

        Database.Add(
            "Site.Certificate",
            new X509Secret
            {
                Source = X509SecretSource.Pem,
                CertificatePem = "-----BEGIN CERTIFICATE-----\nPUBLIC\n-----END CERTIFICATE-----\n",
                PrivateKeyPem = $"-----BEGIN PRIVATE KEY-----\n{Canary}-PEM-PRIVATE-KEY\n-----END PRIVATE KEY-----\n",
            },
            "The certificate of the site.",
            expiresUtc: null);

        Configuration.Add(
            "Twilio.AuthToken",
            new TextSecret { Text = $"{Canary}-TWILIO-TOKEN" },
            "Set in the configuration of the server.",
            expiresUtc: null);
    }

    public InMemorySecretStore Database { get; }

    public InMemorySecretStore KeyVault { get; }

    public InMemorySecretStore Configuration { get; }

    /// <summary>
    /// Gets or sets a value indicating whether moving secrets between stores fails, with a message that holds a
    /// canary, as a store could answer with what it keeps.
    /// </summary>
    public bool MoveFails { get; set; }

    /// <summary>
    /// Adds the doubles to the services of a tool call.
    /// </summary>
    /// <param name="services">The services of the tool call.</param>
    public void AddTo(IServiceCollection services)
    {
        var stores = new[] { Database, KeyVault, Configuration };
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(UtcNow);

        services.AddSingleton(clock.Object);
        services.AddSingleton<ISecretManager>(new InMemorySecretManager(stores));
        services.AddSingleton<ISecretStoreOperations>(new InMemoryStoreOperations(this, stores));
    }

    private sealed class InMemorySecretManager : ISecretManager
    {
        private readonly InMemorySecretStore[] _stores;

        public InMemorySecretManager(InMemorySecretStore[] stores)
        {
            _stores = stores;
        }

        public async Task<Secret> GetSecretAsync(string name)
        {
            foreach (var store in _stores)
            {
                var secret = await store.GetSecretAsync(name);

                if (secret.Value is not null)
                {
                    return secret;
                }
            }

            return default;
        }

        public Task<Secret> GetSecretAsync(string name, string storeName)
            => Find(storeName).GetSecretAsync(name);

        public Task SaveSecretAsync(string name, Secret secret, SecretSaveOptions options = null)
            => _stores.First(store => !store.IsReadOnly).SaveSecretAsync(name, secret, options);

        public Task SaveSecretAsync(string name, Secret secret, string storeName, SecretSaveOptions options = null)
        {
            var store = Find(storeName);

            if (store.IsReadOnly)
            {
                throw new InvalidOperationException($"Secret store '{storeName}' is read-only.");
            }

            return store.SaveSecretAsync(name, secret, options);
        }

        public async Task RemoveSecretAsync(string name)
        {
            foreach (var store in _stores.Where(store => !store.IsReadOnly))
            {
                await store.RemoveSecretAsync(name);
            }
        }

        public Task RemoveSecretAsync(string name, string storeName)
        {
            var store = Find(storeName);

            if (store.IsReadOnly)
            {
                throw new InvalidOperationException($"Secret store '{storeName}' is read-only.");
            }

            return store.RemoveSecretAsync(name);
        }

        public async Task<IEnumerable<SecretInfo>> GetSecretInfosAsync()
        {
            var infos = new List<SecretInfo>();

            foreach (var store in _stores)
            {
                infos.AddRange(await store.GetSecretInfosAsync());
            }

            return infos;
        }

        public IEnumerable<ISecretStore> GetStores()
            => _stores;

        private InMemorySecretStore Find(string storeName)
            => _stores.FirstOrDefault(store => string.Equals(store.Name, storeName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Secret store '{storeName}' not found.");
    }

    private sealed class InMemoryStoreOperations : ISecretStoreOperations
    {
        private readonly SecretToolDoubles _doubles;
        private readonly InMemorySecretStore[] _stores;

        public InMemoryStoreOperations(SecretToolDoubles doubles, InMemorySecretStore[] stores)
        {
            _doubles = doubles;
            _stores = stores;
        }

        public async Task<SecretStoreOperationResult> MoveSecretAsync(string name, string sourceStore, string destinationStore)
        {
            if (_doubles.MoveFails)
            {
                throw new InvalidOperationException($"The store answered with {Canary}-STORE-RESPONSE.");
            }

            var source = _stores.First(store => store.Name == sourceStore);
            var destination = _stores.First(store => store.Name == destinationStore);

            if (!source.Contains(name))
            {
                return new SecretStoreOperationResult
                {
                    Name = name,
                    Failure = SecretStoreOperationFailure.SourceMissing,
                };
            }

            if (destination.Contains(name))
            {
                return new SecretStoreOperationResult
                {
                    Name = name,
                    Failure = SecretStoreOperationFailure.DestinationCollision,
                };
            }

            source.MoveTo(destination, name);

            await Task.CompletedTask;

            return new SecretStoreOperationResult
            {
                Name = name,
                Succeeded = true,
                DestinationSaved = true,
            };
        }

        public async Task<IReadOnlyList<SecretStoreOperationResult>> MoveAllSecretsAsync(string sourceStore, string destinationStore)
        {
            var results = new List<SecretStoreOperationResult>();
            var source = _stores.First(store => store.Name == sourceStore);

            foreach (var info in (await source.GetSecretInfosAsync()).ToList())
            {
                results.Add(await MoveSecretAsync(info.Name, sourceStore, destinationStore));
            }

            return results;
        }
    }
}
