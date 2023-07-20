using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Benjamin.Pizza.DocBuilder;

internal static class ServiceCollectionExtensions
{
    public static void Configure(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddHttpClient<MicrosoftXRefReferenceLoader>();
        services.AddTransient<IReferenceLoader, MicrosoftXRefReferenceLoader>();
        services.Decorate<IReferenceLoader, FileSystemCachedReferenceLoader>();
        services.Decorate<IReferenceLoader, CachedReferenceLoader>();
        services.AddTransient<Func<Documentation, IReferenceLoader>>(s => d =>
            new LocalReferenceLoader(d, s.GetRequiredService<ILogger<LocalReferenceLoader>>())
        );
        services.Decorate<Func<Documentation, IReferenceLoader>>((f, s) => d =>
            new ChainedReferenceLoader(
                f(d),
                s.GetRequiredService<IReferenceLoader>()
            )
        );
        services.AddTransient<ReferenceResolver>();
    }
}
