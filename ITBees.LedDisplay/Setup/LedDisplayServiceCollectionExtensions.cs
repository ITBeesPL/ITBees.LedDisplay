using ITBees.LedDisplay.Models;
using ITBees.LedDisplay.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITBees.LedDisplay.Setup;

public static class LedDisplayServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ILedDisplayManager"/> (singleton) and the background maintenance of the boards.
    /// </summary>
    /// <param name="configurationSection">Optional section bound to <see cref="LedDisplayOptions"/>, usually <c>LedDisplays</c>.</param>
    /// <param name="configure">Optional code configuration, applied after the section.</param>
    public static IServiceCollection AddLedDisplays(
        this IServiceCollection services,
        IConfiguration? configurationSection = null,
        Action<LedDisplayOptions>? configure = null)
    {
        var options = services.AddOptions<LedDisplayOptions>();
        if (configurationSection != null)
        {
            options.Bind(configurationSection);
        }

        if (configure != null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILedDisplayClientFactory, LedDisplayClientFactory>();
        services.TryAddSingleton<ILedDisplayManager, LedDisplayManager>();
        services.AddHostedService<LedDisplayMaintenanceService>();
        return services;
    }
}
