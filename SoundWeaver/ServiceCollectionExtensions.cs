using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoundWeaver.Audio; // Required for AudioLayer if MultiLayerAudioPlayer were fully handled
using SoundWeaver.Bot;
using SoundWeaver.Playlists;
using SoundWeaver.UI;

namespace SoundWeaver
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSoundWeaverServices(this IServiceCollection services)
        {
            // Registering services that have ILogger dependencies
            // ViewModels are typically transient
            services.AddTransient<SoundWeaverControlViewModel>();

            // Services
            services.AddSingleton<DiscordBotService>();
            services.AddTransient<PlaylistManager>();

            // SubApplication - its lifecycle might be managed by the main app's plugin system.
            // Registering it here allows it to be resolved with its dependencies if the plugin system uses DI.
            // If SoundWeaverSubApplication is instantiated directly by Assistant's plugin loader,
            // then Assistant's plugin loader would need to be able to provide ILogger and ILoggerFactory.
            // For now, assume it can be resolved.
            services.AddTransient<SoundWeaverSubApplication>();

            // AudioPlayer and MultiLayerAudioPlayer are not registered here because they depend on
            // VoiceNextConnection, which is obtained at runtime.
            // SoundWeaverControlViewModel will be responsible for creating them and passing the necessary loggers
            // using the ILoggerFactory.

            // If AudioLayer needed its own ILogger<AudioLayer> and was created by MultiLayerAudioPlayer,
            // then MultiLayerAudioPlayer would need ILogger<AudioLayer> injected (or use its own factory).
            // Since MultiLayerAudioPlayer is skipped for modification, this is also deferred.

            return services;
        }
    }
}
