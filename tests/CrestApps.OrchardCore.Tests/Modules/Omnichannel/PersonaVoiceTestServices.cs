using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Services for testing how automated calls pick an AI persona's voice: the real persona resolver over one persona and
/// a few text-to-speech deployments of two providers.
/// </summary>
internal static class PersonaVoiceTestServices
{
    public const string PersonaId = "receptionist";

    public const string PersonaVoice = "persona-voice";

    /// <summary>
    /// The deployment the persona's voice was chosen on.
    /// </summary>
    public const string PersonaDeployment = "openai-tts";

    /// <summary>
    /// Another deployment of the persona voice's provider.
    /// </summary>
    public const string SameProviderDeployment = "openai-tts-hd";

    /// <summary>
    /// A deployment of a different provider.
    /// </summary>
    public const string OtherProviderDeployment = "azure-speech";

    /// <summary>
    /// Builds the services, with the AI Personas feature's resolver registered unless <paramref name="withPersonas"/>
    /// is <see langword="false"/>.
    /// </summary>
    /// <param name="withPersonas">Whether the AI Personas feature is enabled.</param>
    /// <param name="personaLanguage">The persona's language.</param>
    /// <param name="configure">Further registrations.</param>
    public static IServiceProvider Create(bool withPersonas = true, string personaLanguage = null, Action<IServiceCollection> configure = null)
    {
        var services = new ServiceCollection();

        if (withPersonas)
        {
            var personaManager = new Mock<IAIPersonaManager>();
            personaManager
                .Setup(manager => manager.FindByNameAsync(PersonaId, It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult(new AIPersona
                {
                    Name = PersonaId,
                    Language = personaLanguage,
                    VoiceName = PersonaVoice,
                    VoiceDeploymentName = PersonaDeployment,
                }));

            var deployments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PersonaDeployment] = "OpenAI",
                [SameProviderDeployment] = "OpenAI",
                [OtherProviderDeployment] = "AzureSpeech",
            };
            var deploymentStore = new Mock<IAIDeploymentStore>();
            deploymentStore
                .Setup(store => store.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string name, CancellationToken _) => ValueTask.FromResult(
                    deployments.TryGetValue(name, out var clientName)
                        ? new AIDeployment { Name = name, ClientName = clientName }
                        : null));

            services.AddSingleton<IAIPersonaResolver>(new DefaultAIPersonaResolver(
                personaManager.Object,
                Mock.Of<ITemplateEngine>(),
                deploymentStore.Object,
                NullLogger<DefaultAIPersonaResolver>.Instance));
        }

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a chat profile linked to the test persona.
    /// </summary>
    /// <param name="id">The profile's identifier.</param>
    public static AIProfile CreateProfile(string id = "profile-1")
    {
        var profile = new AIProfile
        {
            ItemId = id,
            Type = AIProfileType.Chat,
        };

        profile.Put(new AIPersonaMetadata { PersonaId = PersonaId });

        return profile;
    }
}
