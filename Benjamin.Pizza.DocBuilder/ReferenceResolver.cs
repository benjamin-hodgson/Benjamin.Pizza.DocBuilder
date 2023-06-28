namespace Benjamin.Pizza.DocBuilder;

internal sealed class ReferenceResolver
{
    private readonly Func<Documentation, IReferenceLoader> _referenceLoaderFactory;

    public ReferenceResolver(Func<Documentation, IReferenceLoader> referenceLoaderFactory)
    {
        _referenceLoaderFactory = referenceLoaderFactory;
    }

    public async Task<Documentation> ResolveAllReferences(Documentation documentation)
    {
        var resolver = new DocumentationPageReferenceResolver(_referenceLoaderFactory(documentation));

        return await documentation.UpdateMarkupAsync(resolver.ResolveReferences).ConfigureAwait(false);
    }

    private sealed class DocumentationPageReferenceResolver
    {
        private readonly IReferenceLoader _loader;

        public DocumentationPageReferenceResolver(IReferenceLoader loader)
        {
            _loader = loader;
        }

        public async ValueTask<Markup> ResolveReferences(Markup markup)
            => markup is Markup.Link l
                ? l with { Reference = await ResolveReference(l.Reference).ConfigureAwait(false) }
                : markup;

        private async Task<Reference> ResolveReference(Reference reference)
            => reference switch
            {
                Reference.Resolved r => r,
                Reference.Unresolved(var xref) => (await _loader.Load(xref).ConfigureAwait(false)) ?? reference,
                _ => throw new ArgumentOutOfRangeException(nameof(reference), reference, "Unknown reference")
            };
    }
}
