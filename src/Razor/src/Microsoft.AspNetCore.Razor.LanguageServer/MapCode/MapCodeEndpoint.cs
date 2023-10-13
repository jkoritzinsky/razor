// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
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

    private readonly InsertMapperHelper _insertHelper;
    //private readonly ReplaceMapperHelper _replaceHelper;

    public MapCodeEndpoint(
        IRazorDocumentMappingService documentMappingService,
        DocumentContextFactory documentContextFactory,
        ClientNotifierServiceBase languageServer)
    {
        _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        _documentContextFactory = documentContextFactory ?? throw new ArgumentNullException(nameof(documentContextFactory));
        _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));

        _insertHelper = new InsertMapperHelper();
        //_replaceHelper = new ReplaceMapperHelper();
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
                var languageKind = _documentMappingService.GetLanguageKind(codeDocument, hostDocumentIndex: 0, rightAssociative: true);

                // If content is C# or HTML, delegate to their respective language servers.
                if (languageKind is RazorLanguageKind.CSharp || languageKind is RazorLanguageKind.Html)
                {
                    // Have C# and HTML handle the mapping instead.
                    var delegatedRequest = new DelegatedMapCodeParams(
                        documentContext.Identifier,
                        languageKind,
                        [content],
                        // TO-DO: Account for focus locations.
                        FocusLocations: []);

                    var edits = await _languageServer.SendRequestAsync<DelegatedMapCodeParams, LSP.WorkspaceEdit?>(
                        CustomMessageNames.RazorMapCodeEndpoint,
                        delegatedRequest,
                        cancellationToken).ConfigureAwait(false);

                    await HandleDelegatedResponseAsync(edits, changes, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Otherwise we're in a Razor context and need to map the code ourselves.
                    await HandleRazorAsync(codeDocument, mapping.FocusLocations, changes, cancellationToken).ConfigureAwait(false);
                }
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

        var sourceNodes = NodeHelper.ExtractSourceNodes(syntaxTree.Root);
        if (sourceNodes.Count == 0)
        {
            return;
        }

        // Mixed scoped types nodes are unsupported
        if (sourceNodes.Any(sn => sn is RazorSimpleNode) && sourceNodes.Any(sn => sn is RazorScopedNode))
        {
            return;
        }

        var edits = await this.MapInternalAsync(locations, sourceNodes.ToArray(), cancellationToken).ConfigureAwait(false);
        foreach (var edit in edits)
        {
            if (!changes.TryGetValue(edit.Uri.AbsolutePath, out var textEdits))
            {
                textEdits = new List<LSP.TextEdit>();
                changes[edit.Uri.AbsolutePath] = textEdits;
            }

            textEdits.Add(edit.TextEdit);
        }
    }

    /// <summary>
    /// Maps a given syntax node from code generated by the AI,
    /// to a given or existing snapshot.
    /// </summary>
    private async Task<MappedEdit[]> MapInternalAsync(
        LSP.Location[][] locations,
        RazorSourceNode[] sourceNodes,
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

                //var mapperHelper = this.GetMapperHelper(sourceNodes, syntaxTree.Root);

                // If no insertion found is valid, skip this focus location.
                //if (!mapperHelper.TryGetValidInsertions(syntaxTree.Root, sourceNodes, out var validInsertionNodes, out var invalidInsertions))
                //{
                //    continue;
                //}

                // Replace does not support more than one node.
                /*if (mapperHelper is ReplaceMapperHelper && sourceNodes.Length > 1)
                {
                    continue;
                }*/

                var mappedEdits = new List<MappedEdit>();
                foreach (var sourceNode in sourceNodes)
                {
                    // TO-DO
                }

                //// Merge edits when mapping has determined an insert.
                //// This is a hotfix for now, because we don't have a way to handle multiple insertion nodes yet.
                //// So this should help mitigate the issue we were seeing when we tried to insert more than one source node.
                //if (mapperHelper is InsertMapperHelper && mappedEdits.Count > 1)
                //{
                //    mappedEdits = new List<MappedEdit> { MappedEdit.MergeEdits(mappedEdits.ToArray()) };
                //}

                return mappedEdits.ToArray();
            }
        }

        return [];
    }

    /*private ICodeMapperHelper GetMapperHelper(RazorSourceNode[] insertions, Language.Syntax.SyntaxNode target)
    {
        foreach (var insertion in insertions)
        {
            if (insertion.ExistsOnTarget(target, out _))
            {
                return _replaceHelper;
            }
        }

        return _insertHelper;
    }*/

    private async Task HandleDelegatedResponseAsync(
        LSP.WorkspaceEdit? edits,
        Dictionary<string, List<LSP.TextEdit>> changes,
        CancellationToken cancellationToken)
    {
        if (edits is null || edits.Changes is null)
        {
            return;
        }

        // Map code back to Razor
        foreach (var edit in edits.Changes)
        {
            var generatedUri = new Uri(edit.Key);
            var docChanges = changes[edit.Key];
            if (docChanges is null)
            {
                docChanges = [];
                changes[edit.Key] = docChanges;
            }

            foreach (var documentEdit in edit.Value)
            {
                var (hostDocumentUri, hostDocumentRange) = await _documentMappingService.MapToHostDocumentUriAndRangeAsync(
                    generatedUri, documentEdit.Range, cancellationToken).ConfigureAwait(false);

                if (hostDocumentUri != generatedUri)
                {
                    var textEdit = new LSP.TextEdit
                    {
                        Range = hostDocumentRange,
                        NewText = documentEdit.NewText
                    };

                    docChanges.Add(textEdit);
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
