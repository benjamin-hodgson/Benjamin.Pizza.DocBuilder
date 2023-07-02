#pragma warning disable CA1852, SA1516

using Benjamin.Pizza.DocBuilder;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging();
builder.Services.AddHttpClient<IReferenceLoader, MicrosoftXRefReferenceLoader>();
builder.Services.AddTransient<IReferenceLoader, MicrosoftXRefReferenceLoader>();
builder.Services.AddTransient<Func<Documentation, IReferenceLoader>>(s => d =>
    new ChainedReferenceLoader(
        new LocalReferenceLoader(d, s.GetRequiredService<ILogger<LocalReferenceLoader>>()),
        new CachedReferenceLoader(s.GetRequiredService<IReferenceLoader>(), s.GetRequiredService<ILogger<CachedReferenceLoader>>()))
);
builder.Services.AddTransient<ReferenceResolver>();

using var app = builder.Build();

using var assemblies = AssemblyLoader.GetAssemblies(args);

var documentation = Documentation.FromAssemblies(assemblies.Assemblies);

var xrefResolver = app.Services.GetRequiredService<ReferenceResolver>();
documentation = await xrefResolver.ResolveAllReferences(documentation).ConfigureAwait(false);

if (Directory.Exists("_site"))
{
    Directory.Delete("_site", true);
}

Directory.CreateDirectory("_site");

foreach (var page in documentation.Pages)
{
    await File.WriteAllTextAsync(
        Path.Combine("_site", page.Url.ToString()),
        HtmlRenderer.Render(page).ToString()
    ).ConfigureAwait(false);
}
