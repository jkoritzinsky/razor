// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.MapCode.SourceNode;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode.Mappers;

internal static class InsertMapper
{
    public static bool TryGetValidInsertions(
        SyntaxNode target,
        RazorSourceNode[] sourceNodes,
        out RazorSourceNode[] validInsertions,
        out InvalidMappedNode[] invalidMappedNodes)
    {
        var validNodes = new List<RazorSourceNode>();
        var invalidNodes = new List<InvalidMappedNode>();
        validInsertions = [];
        invalidMappedNodes = [];

        foreach (var sn in sourceNodes)
        {
            // For insertions we want the nodes that don't already exist on the target.
            if (!sn.ExistsOnTarget(target, out _))
            {
                validNodes.Add(sn);
            }
            else
            {
                invalidNodes.Add(new InvalidMappedNode(sn, InvalidMappedNodeReason.InsertIdentifierAlreadyExistsOnTarget));
            }
        }

        // As long as we can find a valid node to insert, we will return true.
        if (validNodes.Count != 0)
        {
            validInsertions = [.. validNodes];
            return true;
        }

        invalidMappedNodes = [.. invalidNodes];
        return false;
    }

    public static int? GetInsertionPoint(
        SyntaxNode documentRoot,
        RazorSourceNode nodeToInsert,
        LSP.Location focusArea)
    {
        // If there's an specific focus area, or caret provided, we should try to insert as close as possible.
        // As long as the focused area is not empty.
        if (TryGetFocusedInsertionPoint(focusArea, documentRoot, nodeToInsert, out var focusedInsertionPoint))
        {
            return focusedInsertionPoint;
        }

        // Fallback: Attempt to infer the insertion point without a caret or line.
        // This will attempt to get a default insertion point for the insert node within the
        // current document.
        if (TryGetDefaultInsertionPoint(documentRoot, out var defaultInsertionPoint))
        {
            return defaultInsertionPoint;
        }

        return null;
    }

    private static bool TryGetFocusedInsertionPoint(
        LSP.Location focusArea,
        SyntaxNode documentRoot,
        RazorSourceNode insertion,
        out int insertionPoint)
    {
        // If there's an specific focus area, or caret provided, we should try to insert as close as possible.
        // As long as the focused area is not empty.
        insertionPoint = 0;
        return true;
    }

    private static bool TryGetDefaultInsertionPoint(SyntaxNode node, out int insertionPoint)
    {
        insertionPoint = 0;
        return true;
    }
}
