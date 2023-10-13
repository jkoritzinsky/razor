// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode.SourceNode;

internal class RazorSourceNode
{
    private readonly Lazy<string> _identifierName;

    /// <summary>
    /// The name of the node's identifier.
    /// </summary>
    public string IdentifierName => _identifierName.Value;

    public string ToFullString() => Node.ToFullString();

    /// <summary>
    /// The syntax node wrapped by this class.
    /// </summary>
    public readonly SyntaxNode Node;

    /// <summary>
    /// Creates a new instance of the SourceNode class.
    /// </summary>
    /// <param name="node">The syntax node to represent.</param>
    public RazorSourceNode(SyntaxNode node)
    {
        Node = node;
        _identifierName = new Lazy<string>(() =>
        {
            return GetIdentifierName(Node);
        });
    }

    /// <summary>
    /// Checks whether this node exists on a given target node.
    /// </summary>
    /// <param name="node">The target node to check.</param>
    /// <param name="matchingNode">If this node exists on the target node, this is the matching node.</param>
    /// <returns>True if this node exists on the target node, false otherwise.</returns>
    public bool ExistsOnTarget(SyntaxNode node, out SyntaxNode? matchingNode)
    {
        matchingNode = null;
        if (this is RazorSimpleNode)
        {
            matchingNode = node.DescendantNodesAndSelf()
                .Where(n => n.IsSimpleNode() && GetIdentifierName(n) == IdentifierName)
                .FirstOrDefault();
            if (matchingNode is not null)
            {
                return true;
            }
        }
        else if (this is RazorScopedNode scopedNode)
        {
            matchingNode = node.DescendantNodesAndSelf()
                .Where(n => n.IsScopedNode(out var scope)
                && scope == scopedNode.Scope
                && GetIdentifierName(n) == IdentifierName)
                .FirstOrDefault();
            if (matchingNode is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns a string representation of the node.
    /// </summary>
    /// <returns>A string representation of the node.</returns>
    public override string ToString() => Node.ToFullString();

    /// <summary>
    /// Gets the identifier name of a syntax node.
    /// </summary>
    /// <param name="node">The syntax node to get the identifier name for.</param>
    /// <returns>The identifier name of the syntax node.</returns>
    private static string GetIdentifierName(SyntaxNode node)
    {
        return node switch
        {
            MarkupStartTagSyntax markupStartTag => markupStartTag.Name.Content,
            MarkupEndTagSyntax markupEndTag => markupEndTag.Name.Content,
            MarkupTagHelperStartTagSyntax markupTagHelperStartTag => markupTagHelperStartTag.Name.Content,
            MarkupTagHelperEndTagSyntax markupTagHelperEndTag => markupTagHelperEndTag.Name.Content,
            _ => node.ToString(),
        };
    }
}
