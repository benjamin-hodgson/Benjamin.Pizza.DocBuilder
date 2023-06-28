using System.Collections.Immutable;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Benjamin.Pizza.DocBuilder;

internal interface IReferenceLoader
{
    Task<Reference.Resolved?> Load(Xref xref);
}

internal sealed class MicrosoftXRefReferenceLoader : IReferenceLoader
{
    private readonly HttpClient _client;

    public MicrosoftXRefReferenceLoader(HttpClient client)
    {
        _client = client;
    }

    public async Task<Reference.Resolved?> Load(Xref xref)
    {
        var uri = new UriBuilder("https://xref.docs.microsoft.com/query")
        {
            Query = "uid=" + xref.Value[2..] // strip off "T:"
        };

        using var stream = await _client.GetStreamAsync(uri.Uri).ConfigureAwait(false);

        var json = await JsonSerializer.DeserializeAsync<JsonDocument>(stream).ConfigureAwait(false);

        if (json!.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        return new Reference.Resolved(
            json!.RootElement[0].GetProperty("name").GetString()!,
            new Uri(json!.RootElement[0].GetProperty("href").GetString()!, UriKind.Absolute));
    }
}

internal sealed class CachedReferenceLoader : IReferenceLoader
{
    private readonly IReferenceLoader _underlying;
    private readonly Dictionary<Xref, Reference.Resolved?> _cache = new();

    public CachedReferenceLoader(IReferenceLoader underlying)
    {
        _underlying = underlying;
    }

    public async Task<Reference.Resolved?> Load(Xref xref)
    {
        if (_cache.TryGetValue(xref, out var result))
        {
            return result;
        }

        var resolved = await _underlying.Load(xref).ConfigureAwait(false);

        _cache[xref] = resolved;

        return resolved;
    }
}

internal sealed class LocalReferenceLoader : IReferenceLoader
{
    private readonly ImmutableDictionary<Xref, Reference.Resolved> _references;
    private readonly ILogger<LocalReferenceLoader> _logger;

    public LocalReferenceLoader(Documentation documentation, ILogger<LocalReferenceLoader> logger)
    {
        _references = documentation.Xrefs;
        _logger = logger;
    }

    public Task<Reference.Resolved?> Load(Xref xref)
    {
        _logger.LogInformation("Trying to get local reference for {Xref}", xref.Value);

        if (_references.TryGetValue(xref, out var result))
        {
            _logger.LogInformation("Found local reference {Xref}", xref.Value);
            return Task.FromResult<Reference.Resolved?>(result);
        }

        _logger.LogInformation("No local reference {Xref}", xref.Value);
        return Task.FromResult<Reference.Resolved?>(null);
    }
}

internal sealed class ChainedReferenceLoader : IReferenceLoader
{
    private readonly ImmutableArray<IReferenceLoader> _loaders;

    public ChainedReferenceLoader(params IReferenceLoader[] loaders)
        : this(loaders.ToImmutableArray())
    {
    }

    public ChainedReferenceLoader(ImmutableArray<IReferenceLoader> loaders)
    {
        _loaders = loaders;
    }

    public async Task<Reference.Resolved?> Load(Xref xref)
    {
        foreach (var loader in _loaders)
        {
            var result = await loader.Load(xref).ConfigureAwait(false);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
