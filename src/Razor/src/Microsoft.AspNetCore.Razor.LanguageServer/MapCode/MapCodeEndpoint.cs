﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.MapCode.Mappers;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

/// <summary>
/// Maps requested code to a given Razor document.
/// </summary>
/// <remarks>
/// This class and its mapping heuristics will likely be constantly evolving as we receive
/// more advanced inputs from the client.
/// </remarks>
[LanguageServerEndpoint(LSP.MapperMethods.WorkspaceMapCodeName)]
internal sealed class MapCodeEndpoint : IRazorDocumentlessRequestHandler<LSP.MapCodeParams, LSP.WorkspaceEdit?>
{
    private readonly IRazorDocumentMappingService _documentMappingService;
    private readonly DocumentContextFactory _documentContextFactory;
    private readonly ClientNotifierServiceBase _languageServer;
    private readonly FilePathService _filePathService;

    public MapCodeEndpoint(
        IRazorDocumentMappingService documentMappingService,
        DocumentContextFactory documentContextFactory,
        ClientNotifierServiceBase languageServer,
        FilePathService filePathService)
    {
        _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        _documentContextFactory = documentContextFactory ?? throw new ArgumentNullException(nameof(documentContextFactory));
        _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
        _filePathService = filePathService ?? throw new ArgumentNullException(nameof(filePathService));
    }

    public bool MutatesSolutionState => false;

    public async Task<LSP.WorkspaceEdit?> HandleRequestAsync(
        LSP.MapCodeParams request,
        RazorRequestContext context,
        CancellationToken cancellationToken)
    {
        // TO-DO: Apply updates to the workspace before doing mapping. This is currently
        // unimplemented by the client, so we won't bother doing anything for now until
        // we determine what kinds of updates the client will actually send us.
        if (request.Updates is not null)
        {
            return null;
        }

        using var _ = ArrayBuilderPool<TextDocumentEdit>.GetPooledObject(out var changes);
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
            var tagHelperContext = await documentContext.GetTagHelperContextAsync(cancellationToken).ConfigureAwait(false);
            var fileKind = FileKinds.GetFileKindFromFilePath(documentContext.FilePath);
            var extension = Path.GetExtension(documentContext.FilePath);

            foreach (var content in mapping.Contents)
            {
                if (content is null)
                {
                    continue;
                }

                // We create a new Razor file based on each mapping's content in order to get the syntax tree that we'll later use to map.
                var sourceDocument = RazorSourceDocument.Create(content, "Test" + extension);
                var codeToMap = projectEngine.Process(sourceDocument, fileKind, importSources, tagHelperContext.TagHelpers);

                await MapCodeAsync(codeToMap, mapping.FocusLocations, changes, cancellationToken).ConfigureAwait(false);
            }
        }

        var workspaceEdits = new LSP.WorkspaceEdit
        {
            DocumentChanges = changes.ToArray()
        };

        return workspaceEdits;
    }

    private async Task MapCodeAsync(
        RazorCodeDocument codeToMap,
        LSP.Location[][] locations,
        ImmutableArray<TextDocumentEdit>.Builder changes,
        CancellationToken cancellationToken)
    {
        var syntaxTree = codeToMap.GetSyntaxTree();
        if (syntaxTree is null)
        {
            return;
        }

        var nodesToMap = ExtractValidNodesToMap(syntaxTree.Root);
        if (nodesToMap.Count == 0)
        {
            return;
        }

        await MapCodeAsync(locations, [.. nodesToMap], changes, cancellationToken).ConfigureAwait(false);
        MergeEdits(changes);
    }

    private async Task MapCodeAsync(
        LSP.Location[][] focusLocations,
        SyntaxNode[] nodesToMap,
        ImmutableArray<TextDocumentEdit>.Builder changes,
        CancellationToken cancellationToken)
    {
        var didCalculateCSharpFocusLocations = false;
        var csharpFocusLocations = new LSP.Location[focusLocations.Length][];

        // We attempt to map the code using each focus location in order of priority.
        // The outer array is an ordered priority list (from highest to lowest priority),
        // and the inner array is a list of locations that have the same priority.
        // If we can successfully map using the first location, we'll stop and return.
        var mappingSuccess = false;
        foreach (var locationByPriority in focusLocations)
        {
            foreach (var location in locationByPriority)
            {
                // The current assumption is that all focus locations will always be in the same document
                // as the code to map. The client is currently implemented using this behavior, but if it
                // ever changes, we'll need to update this code to account for it.
                var documentContext = _documentContextFactory.TryCreateForOpenDocument(location.Uri);
                if (documentContext is null)
                {
                    continue;
                }

                var syntaxTree = await documentContext.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                if (syntaxTree is null)
                {
                    continue;
                }

                var razorNodesToMap = new List<SyntaxNode>();
                foreach (var nodeToMap in nodesToMap)
                {
                    // If node is C#, we send it to their language server to handle and ignore it from our end.
                    if (nodeToMap.IsCSharpNode(out var csharpBody))
                    {
                        if (!didCalculateCSharpFocusLocations)
                        {
                            csharpFocusLocations = await GetCSharpFocusLocationsAsync(focusLocations, cancellationToken).ConfigureAwait(false);
                            didCalculateCSharpFocusLocations = true;
                        }

                        var csharpMappingSuccessful = await SendCSharpDelegatedMappingRequestAsync(
                            documentContext.Identifier, csharpBody, csharpFocusLocations, changes, cancellationToken).ConfigureAwait(false);
                        if (!csharpMappingSuccessful)
                        {
                            // An error occurred during C# mapping. Let the client handle the mapping.
                            changes.Clear();
                            return;
                        }

                        mappingSuccess = true;
                        continue;
                    }

                    // If node already exists in the document, we'll ignore it.
                    if (nodeToMap.ExistsOnTarget(syntaxTree.Root))
                    {
                        continue;
                    }

                    razorNodesToMap.Add(nodeToMap);
                }

                var sourceText = await documentContext.Snapshot.GetTextAsync().ConfigureAwait(false);

                foreach (var nodeToMap in razorNodesToMap)
                {
                    var insertionSpan = InsertMapper.GetInsertionPoint(syntaxTree.Root, sourceText, location);
                    if (insertionSpan is not null)
                    {
                        var textSpan = new TextSpan(insertionSpan.Value, 0);
                        var edit = new LSP.TextEdit { NewText = nodeToMap.ToFullString(), Range = textSpan.ToRange(sourceText) };

                        var textDocumentEdit = new TextDocumentEdit
                        {
                            TextDocument = new OptionalVersionedTextDocumentIdentifier
                            {
                                Uri = documentContext.Identifier.TextDocumentIdentifier.Uri
                            },
                            Edits = [edit],
                        };

                        changes.Add(textDocumentEdit);
                        mappingSuccess = true;
                    }
                }

                // We were able to successfully map using this focusLocation.
                if (mappingSuccess)
                {
                    return;
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

    private static List<SyntaxNode> ExtractValidNodesToMap(SyntaxNode rootNode)
    {
        var validNodesToMap = new List<SyntaxNode>();
        var stack = new Stack<SyntaxNode>();
        stack.Push(rootNode);

        while (stack.Count > 0)
        {
            var currentNode = stack.Pop();

            if (s_validNodesToMap.Contains(currentNode.GetType()))
            {
                validNodesToMap.Add(currentNode);
                continue;
            }

            // Add child nodes to the stack in reverse order for depth-first search
            foreach (var childNode in currentNode.ChildNodes().Reverse())
            {
                stack.Push(childNode);
            }
        }

        return validNodesToMap;
    }

    // These are the nodes that we currently support for mapping. We should update
    // this list as the client evolves to send more types of nodes.
    private readonly static List<Type> s_validNodesToMap =
    [
        typeof(CSharpCodeBlockSyntax),
        typeof(CSharpExplicitExpressionSyntax),
        typeof(CSharpImplicitExpressionSyntax),
        typeof(MarkupElementSyntax),
        typeof(MarkupTextLiteralSyntax),
        typeof(RazorDirectiveSyntax),
    ];

    private async Task<bool> SendCSharpDelegatedMappingRequestAsync(
        TextDocumentIdentifierAndVersion textDocumentIdentifier,
        SyntaxNode nodeToMap,
        LSP.Location[][] focusLocations,
        ImmutableArray<TextDocumentEdit>.Builder changes,
        CancellationToken cancellationToken)
    {
        var delegatedRequest = new DelegatedMapCodeParams(
            textDocumentIdentifier,
            RazorLanguageKind.CSharp,
            [nodeToMap.ToFullString()],
            FocusLocations: focusLocations);

        LSP.WorkspaceEdit? edits = null;
        try
        {
            edits = await _languageServer.SendRequestAsync<DelegatedMapCodeParams, LSP.WorkspaceEdit?>(
                CustomMessageNames.RazorMapCodeEndpoint,
                delegatedRequest,
                cancellationToken).ConfigureAwait(false);
        } catch
        {
            // C# hasn't implemented + merged their C# code mapper yet.
            return true;
        }

        if (edits is null)
        {
            // It's likely an error occurred during C# mapping.
            return false;
        }

        await HandleDelegatedResponseAsync(edits, changes, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<LSP.Location[][]> GetCSharpFocusLocationsAsync(LSP.Location[][] focusLocations, CancellationToken cancellationToken)
    {
        // If the focus locations are in a C# context, map them to the C# document.
        var csharpFocusLocations = new LSP.Location[focusLocations.Length][];
        for (var i = 0; i < focusLocations.Length; i++)
        {
            var locations = focusLocations[i];
            var csharpLocations = new List<LSP.Location>();
            foreach (var potentialLocation in locations)
            {
                if (potentialLocation is null)
                {
                    continue;
                }

                var documentContext = _documentContextFactory.TryCreateForOpenDocument(potentialLocation.Uri);
                if (documentContext is null)
                {
                    continue;
                }

                var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
                var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
                var hostDocumentRange = potentialLocation.Range.ToLinePositionSpan();
                var csharpDocument = codeDocument.GetCSharpDocument();

                if (_documentMappingService.TryMapToGeneratedDocumentRange(csharpDocument, hostDocumentRange, out var generatedDocumentRange))
                {
                    var csharpLocation = new LSP.Location
                    {
                        // We convert the URI to the C# generated document URI later on in
                        // LanguageServer.Client since we're unable to retrieve it here.
                        Uri = potentialLocation.Uri,
                        Range = generatedDocumentRange.ToRange()
                    };

                    csharpLocations.Add(csharpLocation);
                }
            }

            csharpFocusLocations[i] = [.. csharpLocations];
        }

        return csharpFocusLocations;
    }

    // Map C# code back to Razor file
    private async Task HandleDelegatedResponseAsync(
        LSP.WorkspaceEdit edits,
        ImmutableArray<TextDocumentEdit>.Builder changes,
        CancellationToken cancellationToken)
    {
        if (edits.DocumentChanges is not null && edits.DocumentChanges.Value.TryGetFirst(out var documentEdits))
        {
            // We only support document edits for now. In the future once the client supports it, we should look
            // into also supporting file creation/deletion/rename.
            foreach (var edit in documentEdits)
            {
                await ProcessEdit(edit.TextDocument.Uri, edit.Edits, changes, cancellationToken).ConfigureAwait(false);
            }
        }

        if (edits.Changes is not null)
        {
            foreach (var edit in edits.Changes)
            {
                var generatedUri = new Uri(edit.Key);
                await ProcessEdit(generatedUri, edit.Value, changes, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task ProcessEdit(
            Uri generatedUri,
            TextEdit[] textEdits,
            ImmutableArray<TextDocumentEdit>.Builder changes,
            CancellationToken cancellationToken)
        {
            foreach (var documentEdit in textEdits)
            {
                // The edits we receive from C# can contain preprocessor directives, which we'll ignore
                // so we're successfully able to map back to the host document.
                // For example, C# can send back something like this, but we only care about the
                // non-preprocessor content:
                //     #pragma warning restore 1998
                //     #nullable restore
                //     #line 12 "C:/path/to/razor/file.razor"
                //     
                //     int x2 = 1;

                var updatedEdit = await RemoveStartingPreprocessorDirectivesAsync(generatedUri, documentEdit, cancellationToken).ConfigureAwait(false);
                var (hostDocumentUri, hostDocumentRange) = await _documentMappingService.MapToHostDocumentUriAndRangeAsync(
                    generatedUri, updatedEdit.Range, cancellationToken).ConfigureAwait(false);

                if (hostDocumentUri != generatedUri)
                {
                    var textEdit = new LSP.TextEdit
                    {
                        Range = hostDocumentRange,
                        NewText = updatedEdit.NewText
                    };

                    var textDocumentEdit = new TextDocumentEdit
                    {
                        TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = hostDocumentUri },
                        Edits = [textEdit]
                    };
                    changes.Add(textDocumentEdit);
                }
            }
        }

        async Task<TextEdit> RemoveStartingPreprocessorDirectivesAsync(
            Uri generatedUri,
            TextEdit documentEdit,
            CancellationToken cancellationToken)
        {
            // We assume C# always sends back full lines of code (which they do seem to be doing currently)
            var lines = documentEdit.NewText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            var removedStartLines = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("#"))
                {
                    removedStartLines++;
                    continue;
                }

                break;
            }

            // Don't need to make any adjustments.
            if (removedStartLines == 0)
            {
                return documentEdit;
            }

            var razorDocumentUri = _filePathService.GetRazorDocumentUri(generatedUri);
            var documentContext = _documentContextFactory.TryCreateForOpenDocument(razorDocumentUri);
            if (documentContext is null)
            {
                return documentEdit;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            var csharpDocument = codeDocument.GetCSharpDocument();
            var sourceMapping = csharpDocument.SourceMappings.Where(
                mapping => mapping.GeneratedSpan.LineIndex == documentEdit.Range.Start.Line + removedStartLines).FirstOrDefault();
            if (sourceMapping is null)
            {
                return documentEdit;
            }

            var newText = string.Join(Environment.NewLine, new ArraySegment<string>(lines, offset: removedStartLines, lines.Length - removedStartLines));
            var newRange = new Range
            {
                Start = new Position(
                    documentEdit.Range.Start.Line + removedStartLines, sourceMapping.GeneratedSpan.CharacterIndex),
                End = documentEdit.Range.End
            };

            var updatedTextEdit = new TextEdit
            {
                NewText = newText,
                Range = newRange
            };

            return updatedTextEdit;
        }
    }

    // Resolve edits that are at the same start location by merging them together.
    private static void MergeEdits(ImmutableArray<TextDocumentEdit>.Builder changes)
    {
        var groupedChanges = changes.GroupBy(c => c.TextDocument.Uri);
        foreach (var documentChanges in groupedChanges)
        {
            var edits = documentChanges.ToList();
            edits.Sort((x, y) => x.Edits.Single().Range.Start.CompareTo(y.Edits.Single().Range.Start));

            for (var i = edits.Count - 1; i < edits.Count && i > 0; i--)
            {
                var previousEdit = edits[i - 1].Edits.Single();
                var currentEdit = edits[i].Edits.Single();
                if (currentEdit.Range.Start == previousEdit.Range.Start)
                {
                    // Append the text of the current edit to the previous edit
                    previousEdit.NewText += currentEdit.NewText;
                    previousEdit.Range.End = currentEdit.Range.End;
                    edits.RemoveAt(i);
                }
            }

            var finalEditsForDoc = new TextDocumentEdit
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Uri = documentChanges.Key,
                },
                Edits = edits.SelectMany(e => e.Edits).ToArray()
            };

            changes.Clear();
            changes.Add(finalEditsForDoc);
        }
    }
}
