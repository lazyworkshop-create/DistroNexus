using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DistroNexus.Core.Services;

public static class WslgApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddWslgApplications(this IServiceCollection services)
    {
        services.TryAddSingleton<IWslgApplicationService, WslgApplicationService>();
        return services;
    }
}
