// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.LanguageServer.MapCode.SourceNode;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode.Mappers;

internal static class ReplaceMapper
{
    public static bool TryGetValidReplacementNodes(
    SyntaxNode target,
    RazorSourceNode[] sourceNodes,
    out RazorSourceNode[] validReplacements,
    out InvalidMappedNode[] invalidReplacements)
    {
        var validNodes = new List<RazorSourceNode>();
        var invalidNodes = new List<InvalidMappedNode>();
        foreach (var sourceNode in sourceNodes)
        {
            // For replace we'll validate nodes that already exist in the given target.
            if (sourceNode.ExistsOnTarget(target, out var matchingNode))
            {
                validNodes.Add(sourceNode);
            }
            else
            {
                invalidNodes.Add(new InvalidMappedNode(sourceNode, InvalidMappedNodeReason.ReplaceIdentifierMissingOnTarget));
            }
        }

        validReplacements = [.. validNodes];
        invalidReplacements = [.. invalidNodes];
        return validNodes.Count != 0;
    }

    public static TextSpan? GetReplacementSpan(
        SyntaxNode documentRoot,
        RazorSourceNode nodeToInsert,
        LSP.Location focusArea,
        out string? adjustedInsertion)
    {
        adjustedInsertion = null;
        return null;
    }
}
