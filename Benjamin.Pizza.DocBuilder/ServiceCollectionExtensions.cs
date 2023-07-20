using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Benjamin.Pizza.DocBuilder;

internal static class ServiceCollectionExtensions
{
    public static void Configure(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddHttpClient<IReferenceLoader, MicrosoftXRefReferenceLoader>();
        services.AddTransient<IReferenceLoader, MicrosoftXRefReferenceLoader>();
        services.AddTransient<Func<Documentation, IReferenceLoader>>(s => d =>
            new ChainedReferenceLoader(
                new LocalReferenceLoader(d, s.GetRequiredService<ILogger<LocalReferenceLoader>>()),
                new CachedReferenceLoader(
                    new FileSystemCachedReferenceLoader(
                        s.GetRequiredService<IReferenceLoader>(),
                        s.GetRequiredService<ILogger<FileSystemCachedReferenceLoader>>()
                    ),
                    s.GetRequiredService<ILogger<CachedReferenceLoader>>()
                )
            )
        );
        services.AddTransient<ReferenceResolver>();
    }
}
