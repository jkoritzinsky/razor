// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

[LanguageServerEndpoint(MapperMethods.TextDocumentMapCodeName)]
internal sealed class MapCodeEndpoint : IRazorRequestHandler<MapCodeParams, WorkspaceEdit?>
{
    private readonly IRazorDocumentMappingService _documentMappingService;
    private readonly ClientNotifierServiceBase _languageServer;

    public MapCodeEndpoint(
        IRazorDocumentMappingService documentMappingService,
        ClientNotifierServiceBase languageServer)
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

        // TO-DO: Apply Updates to the workspace before doing mapping. This is currently unsupported until we determine the
        // types of updates the client sends us.
        if (request.Updates is not null)
        {
            return null;
        }

        // We need focus locations to be able to determine which language server to delegate to. If we don't have them, return.
        if (request.FocusLocations is null || request.FocusLocations.Length == 0)
        {
            return null;
        }

        // We'll go through the focus locations (which are sorted in priority order) and see if we can map any of them successfully.
        foreach (var focusLocation in request.FocusLocations)
        {
            var location = focusLocation.Location;
            if (location is null || location.Uri != context.Uri)
            {
                continue;
            }

            var documentPositionInfo = await DefaultDocumentPositionInfoStrategy.Instance.TryGetPositionInfoAsync(
                _documentMappingService,
                context.DocumentContext,
                location.Range.Start,
                context.Logger,
                cancellationToken).ConfigureAwait(false);

            if (documentPositionInfo is null)
            {
                continue;
            }

            var codeDocument = await context.DocumentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            var changes = new Dictionary<string, List<TextEdit>>();

            foreach (var content in request.Contents)
            {
                if (documentPositionInfo.LanguageKind is RazorLanguageKind.Razor)
                {
                    await HandleRazorAsync(content, context.DocumentContext, changes, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Have C# and HTML handle the mapping instead.
                    var delegatedRequest = new DelegatedMapCodeParams(context.DocumentContext.Identifier, documentPositionInfo.LanguageKind, [content]);

                    var edits = await _languageServer.SendRequestAsync<DelegatedMapCodeParams, WorkspaceEdit?>(
                        CustomMessageNames.RazorMapCodeEndpoint,
                        delegatedRequest,
                        cancellationToken).ConfigureAwait(false);

                    await HandleDelegatedResponseAsync(edits, changes, cancellationToken).ConfigureAwait(false);
                }
            }

            // At least one change failed to map. Let's try using a different focus location.
            if (changes.Count != request.Contents.Length)
            {
                continue;
            }

            var finalizedChanges = new Dictionary<string, TextEdit[]>();
            foreach (var change in changes)
            {
                finalizedChanges.Add(change.Key, [.. change.Value]);
            }

            var workspaceEdits = new WorkspaceEdit { Changes = finalizedChanges };
            return workspaceEdits;
        }

        // We couldn't map the contents to locations.
        return null;
    }

    // Handles Razor code mapping. These various heuristics will evolve over time as we encounter more user scenarios.
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private static async Task HandleRazorAsync(
        string content,
        VersionedDocumentContext context,
        Dictionary<string, List<TextEdit>> changes,
        CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore IDE0060 // Remove unused parameter
    {
        // TO-DO: Fill this in. Currently, it seems like the only Razor edits we're receiving are full document changes, so
        // we can't do much here. Once we're sent Razor snippets, we can do more here.
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
                var (hostDocumentUri, hostDocumentRange) = await _documentMappingService.MapToHostDocumentUriAndRangeAsync(
                    generatedUri, documentEdit.Range, cancellationToken).ConfigureAwait(false);

                if (hostDocumentUri != generatedUri)
                {
                    var textEdit = new TextEdit
                    {
                        Range = hostDocumentRange,
                        NewText = documentEdit.NewText
                    };

                    docChanges.Add(textEdit);
                }
            }
        }
    }
}
