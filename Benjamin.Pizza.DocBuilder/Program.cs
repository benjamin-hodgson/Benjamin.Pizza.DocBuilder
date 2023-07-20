#pragma warning disable CA1852, SA1516

using Benjamin.Pizza.DocBuilder;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure();

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

// var defaultNamespaceUrl = new Uri(Path.ChangeExtension(Path.GetFileName(args[0]), "html"), UriKind.Relative);
// await File.WriteAllTextAsync(
//     Path.Combine("_site", "index.html"),
//     RedirectPage.GetHtml(defaultNamespaceUrl).ToString()
// ).ConfigureAwait(false);
