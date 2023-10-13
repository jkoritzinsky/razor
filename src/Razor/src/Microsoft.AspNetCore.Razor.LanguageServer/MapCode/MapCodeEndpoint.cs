// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.MapCode.Mappers;
using Microsoft.AspNetCore.Razor.LanguageServer.MapCode.SourceNode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CommonLanguageServerProtocol.Framework;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

[LanguageServerEndpoint(LSP.MapperMethods.WorkspaceMapCodeName)]
internal sealed class MapCodeEndpoint : IRazorDocumentlessRequestHandler<LSP.MapCodeParams, LSP.WorkspaceEdit?>
{
    private readonly IRazorDocumentMappingService _documentMappingService;
    private readonly DocumentContextFactory _documentContextFactory;
    private readonly ClientNotifierServiceBase _languageServer;

    public MapCodeEndpoint(
        IRazorDocumentMappingService documentMappingService,
        DocumentContextFactory documentContextFactory,
        ClientNotifierServiceBase languageServer)
    {
        _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        _documentContextFactory = documentContextFactory ?? throw new ArgumentNullException(nameof(documentContextFactory));
        _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
    }

    public bool MutatesSolutionState => false;

    public async Task<LSP.WorkspaceEdit?> HandleRequestAsync(
        LSP.MapCodeParams request,
        RazorRequestContext context,
        CancellationToken cancellationToken)
    {
        // TO-DO: Apply Updates to the workspace before doing mapping. This is currently unsupported until we determine the
        // types of updates the client sends us.
        if (request.Updates is not null)
        {
            return null;
        }

        var changes = new Dictionary<string, List<LSP.TextEdit>>();
        foreach (var mapping in request.Mappings)
        {
            if (mapping.TextDocument is null || mapping.FocusLocations is null)
            {
                continue;
            }

            var documentContext = _documentContextFactory.TryCreateForOpenDocument(mapping.TextDocument.Uri);
            if (documentContext is null)
            {
                continue;
            }

            var (projectEngine, importSources) = await InitializeProjectEngineAsync(documentContext.Snapshot).ConfigureAwait(false);
            var tagHelperContext =  await documentContext.GetTagHelperContextAsync(cancellationToken).ConfigureAwait(false);
            var fileKind = FileKinds.GetFileKindFromFilePath(documentContext.FilePath);
            var extension = Path.GetExtension(documentContext.FilePath);

            foreach (var content in mapping.Contents)
            {
                if (content is null)
                {
                    continue;
                }

                var sourceDocument = RazorSourceDocument.Create(content, "Test" + extension);
                var codeDocument = projectEngine.Process(sourceDocument, fileKind, importSources, tagHelperContext.TagHelpers);

                // TO-DO: Handle delegation once we know exactly what we need to delegate.
                // For now, just let Razor handle everything.
                await HandleRazorAsync(codeDocument, mapping.FocusLocations, changes, cancellationToken).ConfigureAwait(false);
            }
        }

        var finalizedChanges = new Dictionary<string, LSP.TextEdit[]>();
        foreach (var change in changes)
        {
            finalizedChanges.Add(change.Key, [.. change.Value]);
        }

        var workspaceEdits = new LSP.WorkspaceEdit { Changes = finalizedChanges };
        return workspaceEdits;
    }

    private async Task HandleRazorAsync(
        RazorCodeDocument codeDocument,
        LSP.Location[][] locations,
        Dictionary<string, List<LSP.TextEdit>> changes,
        CancellationToken cancellationToken)
    {
        var syntaxTree = codeDocument.GetSyntaxTree();
        if (syntaxTree is null)
        {
            return;
        }

        var sourceNodes = syntaxTree.Root.ExtractSourceNodes();
        if (sourceNodes.Count == 0)
        {
            return;
        }

        // Mixed scoped types nodes are unsupported
        if (sourceNodes.Any(sn => sn is RazorSimpleNode) && sourceNodes.Any(sn => sn is RazorScopedNode))
        {
            return;
        }

        await MapCodeAsync(locations, [.. sourceNodes], changes, cancellationToken).ConfigureAwait(false);
    }

    private async Task MapCodeAsync(
        LSP.Location[][] locations,
        RazorSourceNode[] sourceNodes,
        Dictionary<string, List<LSP.TextEdit>> changes,
        CancellationToken cancellationToken)
    {
        foreach (var locationByPriority in locations)
        {
            foreach (var location in locationByPriority)
            {
                var documentContext = _documentContextFactory.TryCreate(location.Uri);
                if (documentContext is null)
                {
                    continue;
                }

                var syntaxTree = await documentContext.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                if (syntaxTree is null)
                {
                    continue;
                }

                var replace = false;
                foreach (var sourceNode in sourceNodes)
                {
                    if (sourceNode.ExistsOnTarget(syntaxTree.Root, out _))
                    {
                        replace = true;
                        break;
                    }
                }

                if (replace)
                {
                    if (!ReplaceMapper.TryGetValidReplacementNodes(syntaxTree.Root, sourceNodes, out var validReplacements, out var invalidReplacements))
                    {
                        continue;
                    }

                    // Replace does not support more than one node.
                    if (sourceNodes.Length > 1)
                    {
                        continue;
                    }
                }
                else
                {
                    if (!InsertMapper.TryGetValidInsertions(syntaxTree.Root, sourceNodes, out var validInsertions, out var invalidMappedNodes))
                    {
                        continue;
                    }
                }

                if (!changes.TryGetValue(documentContext.Uri.AbsolutePath, out var textEdits))
                {
                    textEdits = [];
                    changes[documentContext.Uri.AbsolutePath] = textEdits;
                }

                var sourceText = await documentContext.Snapshot.GetTextAsync().ConfigureAwait(false);

                foreach (var sourceNode in sourceNodes)
                {
                    if (replace)
                    {
                        // by default we assume the insertion or replacement will be the full syntax node.
                        var insertion = sourceNode.ToFullString();
                        var replacementSpan = ReplaceMapper.GetReplacementSpan(syntaxTree.Root, sourceNode, location, out var adjustedInsertion);
                        if (replacementSpan is not null)
                        {
                            // TO-DO: Fill this in
                        }
                    }
                    else
                    {
                        var insertionSpan = InsertMapper.GetInsertionPoint(syntaxTree.Root, sourceNode, location);
                        if (insertionSpan is not null)
                        {
                            var textSpan = new TextSpan(insertionSpan.Value, 0);
                            var edit = new LSP.TextEdit { NewText = sourceNode.ToFullString(), Range = textSpan.ToRange(sourceText) };
                            textEdits.Add(edit);
                        }
                    }
                }
            }
        }
    }

    private static async Task<(RazorProjectEngine projectEngine, List<RazorSourceDocument> importSources)> InitializeProjectEngineAsync(IDocumentSnapshot originalSnapshot)
    {
        var engine = originalSnapshot.Project.GetProjectEngine();
        var importSources = new List<RazorSourceDocument>();

        var imports = originalSnapshot.GetImports();
        foreach (var import in imports)
        {
            var sourceText = await import.GetTextAsync().ConfigureAwait(false);
            var source = sourceText.GetRazorSourceDocument(import.FilePath, import.TargetPath);
            importSources.Add(source);
        }

        return (engine, importSources);
    }
}
