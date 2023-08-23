// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

// TO-DO: Change to constant once LSP client implementation is merged
[LanguageServerEndpoint("textDocument/mapCode")]
internal sealed class MapCodeEndpoint : IRazorRequestHandler<MapCodeParams, WorkspaceEdit?>
{
    private readonly IRazorDocumentMappingService _documentMappingService;
    private readonly ClientNotifierServiceBase _languageServer;

    public MapCodeEndpoint(IRazorDocumentMappingService documentMappingService, ClientNotifierServiceBase languageServer)
    {
        _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
    }

    public bool MutatesSolutionState => false;

    public TextDocumentIdentifier GetTextDocumentIdentifier(MapCodeParams request) => request.TextDocument;

    public async Task<WorkspaceEdit?> HandleRequestAsync(MapCodeParams request, RazorRequestContext context, CancellationToken cancellationToken)
    {
        if (context.DocumentContext is null)
        {
            return null;
        }

        // TO-DO: Apply Updates to workspace before doing anything below

        var changes = new Dictionary<string, List<TextEdit>>();

        // Determine the language kind for each content to figure out which language server to delegate to
        foreach (var content in request.Contents)
        {
            var sourceDocument = context.DocumentContext.FilePath.EndsWith(".razor", StringComparison.Ordinal)
                ? RazorSourceDocument.Create(content, "File.razor")
                : RazorSourceDocument.Create(content, "File.cshtml");

            var codeDocument = RazorCodeDocument.Create(sourceDocument);
            var languageKind = _documentMappingService.GetLanguageKind(codeDocument, 0, rightAssociative: false);

            if (languageKind is RazorLanguageKind.Razor)
            {
                await HandleRazorAsync(codeDocument, context.DocumentContext, changes, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Have C# and HTML handle the mapping instead.
                var delegatedRequest = new DelegatedMapCodeParams(context.DocumentContext.Identifier, languageKind, [content]);

                var edits = await _languageServer.SendRequestAsync<DelegatedMapCodeParams, WorkspaceEdit?>(
                    CustomMessageNames.RazorMapCodeEndpoint,
                    delegatedRequest,
                    cancellationToken).ConfigureAwait(false);

                await HandleDelegatedResponseAsync(edits, changes, cancellationToken).ConfigureAwait(false);
            }
        }

        var finalizedChanges = new Dictionary<string, TextEdit[]>();
        foreach (var change in changes)
        {
            finalizedChanges.Add(change.Key, change.Value.ToArray());
        }

        var workspaceEdits = new WorkspaceEdit { Changes = finalizedChanges };
        return workspaceEdits;
    }

    private static async Task HandleRazorAsync(
        RazorCodeDocument codeDocument,
        VersionedDocumentContext context,
        Dictionary<string, List<TextEdit>> changes,
        CancellationToken cancellationToken)
    {
        var syntaxTree = codeDocument.GetSyntaxTree();

        // TO-DO: This is a work in progress. Eventually we want to do things like insert and replace components and TagHelpers.
        // We'll implement this bit once the LSP client side is in so we can test while implementing.
        var tagHelperContext = await context.GetTagHelperContextAsync(cancellationToken).ConfigureAwait(false);
        var tagHelperNames = tagHelperContext.TagHelpers.Select(tagHelperContext => tagHelperContext.Name);
    }

    private async Task HandleDelegatedResponseAsync(WorkspaceEdit? edits, Dictionary<string, List<TextEdit>> changes, CancellationToken cancellationToken)
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
                docChanges = new List<TextEdit>();
                changes[edit.Key] = docChanges;
            }

            foreach (var documentEdit in edit.Value)
            {
                var (mappedDocumentUri, mappedRange) = await _documentMappingService.MapToHostDocumentUriAndRangeAsync(
                    generatedUri, documentEdit.Range, cancellationToken).ConfigureAwait(false);

                if (mappedDocumentUri != generatedUri)
                {
                    var textEdit = new TextEdit
                    {
                        Range = mappedRange,
                        NewText = documentEdit.NewText
                    };

                    docChanges.Add(textEdit);
                }
            }
        }
    }
}
