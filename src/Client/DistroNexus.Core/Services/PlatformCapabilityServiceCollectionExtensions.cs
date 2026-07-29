using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DistroNexus.Core.Services;

public static class PlatformCapabilityServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformCapabilities(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        services.TryAddSingleton<IPlatformCapabilityService, PlatformCapabilityService>();
        return services;
    }
}
