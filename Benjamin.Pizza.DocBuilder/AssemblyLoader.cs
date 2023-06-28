using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Benjamin.Pizza.DocBuilder;

internal static class AssemblyLoader
{
    public static LoadedAssemblies GetAssemblies(IEnumerable<string> assemblyPaths)
    {
        var bclFiles = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");

        var resolver = new PathAssemblyResolver(bclFiles.Concat(assemblyPaths));

        var context = new MetadataLoadContext(resolver);

        var assemblies = assemblyPaths
            .Select(p => (context.LoadFromAssemblyPath(p), XmlDocFile.Load(Path.ChangeExtension(p, "xml"))))
            .ToImmutableArray();

        return new LoadedAssemblies(context, assemblies);
    }
}

internal sealed class LoadedAssemblies : IDisposable
{
    private MetadataLoadContext? _ctx;
    public ImmutableArray<(Assembly, XmlDocFile)> Assemblies { get; }

    public LoadedAssemblies(MetadataLoadContext ctx, ImmutableArray<(Assembly, XmlDocFile)> assemblies)
    {
        _ctx = ctx;
        Assemblies = assemblies;
    }

    public void Dispose()
    {
        _ctx?.Dispose();
        _ctx = null;
    }
}
