// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.MapCode.SourceNode;
using Microsoft.AspNetCore.Razor.LanguageServer.MapCode;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

internal static class SyntaxNodeExtensions
{
    internal static bool IsUsingDirective(this SyntaxNode node, [NotNullWhen(true)] out SyntaxList<SyntaxNode>? children)
    {
        // Using directives are weird, because the directive keyword ("using") is part of the C# statement it represents
        if (node is RazorDirectiveSyntax razorDirective &&
            razorDirective.DirectiveDescriptor is null &&
            razorDirective.Body is RazorDirectiveBodySyntax body &&
            body.Keyword is CSharpStatementLiteralSyntax literal &&
            literal.LiteralTokens.Count > 0)
        {
            if (literal.LiteralTokens[0] is { Kind: SyntaxKind.Keyword, Content: "using" })
            {
                children = literal.LiteralTokens;
                return true;
            }
        }

        children = null;
        return false;
    }

    /// <summary>
    /// Walks up the tree through the <paramref name="owner"/>'s parents to find the outermost node that starts at the same position.
    /// </summary>
    internal static SyntaxNode? GetOutermostNode(this SyntaxNode owner)
    {
        var node = owner.Parent;
        if (node is null)
        {
            return owner;
        }

        var lastNode = node;
        while (node.SpanStart == owner.SpanStart)
        {
            lastNode = node;
            node = node.Parent;
            if (node is null)
            {
                break;
            }
        }

        return lastNode;
    }

    internal static bool TryGetPreviousSibling(this SyntaxNode syntaxNode, [NotNullWhen(true)] out SyntaxNode? previousSibling)
    {
        var syntaxNodeParent = syntaxNode.Parent;
        if (syntaxNodeParent is null)
        {
            previousSibling = default;
            return false;
        }

        var nodes = syntaxNodeParent.ChildNodes();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == syntaxNode)
            {
                if (i == 0)
                {
                    previousSibling = default;
                    return false;
                }
                else
                {
                    previousSibling = nodes[i - 1];
                    return true;
                }
            }
        }

        previousSibling = default;
        return false;
    }

    public static bool ContainsOnlyWhitespace(this SyntaxNode node, bool includingNewLines = true)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var tokens = node.GetTokens();

        for (var i = 0; i < tokens.Count; i++)
        {
            var tokenKind = tokens[i].Kind;
            if (tokenKind != SyntaxKind.Whitespace && (tokenKind != SyntaxKind.NewLine || !includingNewLines))
            {
                return false;
            }
        }

        // All tokens were either whitespace or newlines.
        return true;
    }

    public static LinePositionSpan GetLinePositionSpan(this SyntaxNode node, RazorSourceDocument source)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var start = node.Position;
        var end = node.EndPosition;

        Debug.Assert(start <= source.Length && end <= source.Length, "Node position exceeds source length.");

        if (start == source.Length && node.FullWidth == 0)
        {
            // Marker symbol at the end of the document.
            var location = node.GetSourceLocation(source);
            var position = GetLinePosition(location);
            return new LinePositionSpan(position, position);
        }

        var startLocation = source.Lines.GetLocation(start);
        var endLocation = source.Lines.GetLocation(end);
        var startPosition = GetLinePosition(startLocation);
        var endPosition = GetLinePosition(endLocation);

        return new LinePositionSpan(startPosition, endPosition);

        static LinePosition GetLinePosition(SourceLocation location)
        {
            return new LinePosition(location.LineIndex, location.CharacterIndex);
        }
    }

    public static Range GetRange(this SyntaxNode node, RazorSourceDocument source)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var lineSpan = node.GetLinePositionSpan(source);
        var range = new Range
        {
            Start = new Position(lineSpan.Start.Line, lineSpan.Start.Character),
            End = new Position(lineSpan.End.Line, lineSpan.End.Character)
        };

        return range;
    }

    public static Range? GetRangeWithoutWhitespace(this SyntaxNode node, RazorSourceDocument source)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var tokens = node.GetTokens();

        SyntaxToken? firstToken = null;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (!token.IsWhitespace())
            {
                firstToken = token;
                break;
            }
        }

        SyntaxToken? lastToken = null;
        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            var token = tokens[i];
            if (!token.IsWhitespace())
            {
                lastToken = token;
                break;
            }
        }

        if (firstToken is null && lastToken is null)
        {
            return null;
        }

        var startPositionSpan = GetLinePositionSpan(firstToken, source, node.SpanStart);
        var endPositionSpan = GetLinePositionSpan(lastToken, source, node.SpanStart);

        var range = new Range
        {
            Start = new Position(startPositionSpan.Start.Line, startPositionSpan.Start.Character),
            End = new Position(endPositionSpan.End.Line, endPositionSpan.End.Character)
        };

        return range;

        // This is needed because SyntaxToken positions taken from GetTokens
        // are relative to their parent node and not to the document.
        static LinePositionSpan GetLinePositionSpan(SyntaxNode? node, RazorSourceDocument source, int parentStart)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var start = node.Position + parentStart;
            var end = node.EndPosition + parentStart;

            if (start == source.Length && node.FullWidth == 0)
            {
                // Marker symbol at the end of the document.
                var location = node.GetSourceLocation(source);
                var position = GetLinePosition(location);
                return new LinePositionSpan(position, position);
            }

            var startLocation = source.Lines.GetLocation(start);
            var endLocation = source.Lines.GetLocation(end);
            var startPosition = GetLinePosition(startLocation);
            var endPosition = GetLinePosition(endLocation);

            return new LinePositionSpan(startPosition, endPosition);

            static LinePosition GetLinePosition(SourceLocation location)
            {
                return new LinePosition(location.LineIndex, location.CharacterIndex);
            }
        }
    }

    public static int GetLeadingWhitespaceLength(this SyntaxNode node, FormattingContext context)
    {
        var tokens = node.GetTokens();
        var whitespaceLength = 0;

        foreach (var token in tokens)
        {
            if (token.IsWhitespace())
            {
                if (token.Kind == SyntaxKind.NewLine)
                {
                    // We need to reset when we move to a new line.
                    whitespaceLength = 0;
                }
                else if (token.IsSpace())
                {
                    whitespaceLength++;
                }
                else if (token.IsTab())
                {
                    whitespaceLength += (int)context.Options.TabSize;
                }
            }
            else
            {
                break;
            }
        }

        return whitespaceLength;
    }

    public static int GetTrailingWhitespaceLength(this SyntaxNode node, FormattingContext context)
    {
        var tokens = node.GetTokens();
        var whitespaceLength = 0;

        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            var token = tokens[i];
            if (token.IsWhitespace())
            {
                if (token.Kind == SyntaxKind.NewLine)
                {
                    whitespaceLength = 0;
                }
                else if (token.IsSpace())
                {
                    whitespaceLength++;
                }
                else if (token.IsTab())
                {
                    whitespaceLength += (int)context.Options.TabSize;
                }
            }
            else
            {
                break;
            }
        }

        return whitespaceLength;
    }

    public static SyntaxNode? FindInnermostNode(this SyntaxNode node, int index, bool includeWhitespace = false)
    {
        var token = node.FindToken(index, includeWhitespace);

        // If the index is EOF but the node has index-1,
        // then try to get a token to the left of the index.
        // patterns like
        // <button></button>$$
        // should get the button node instead of the razor document (which is the parent
        // of the EOF token)
        if (token.Kind == SyntaxKind.EndOfFile && node.Span.Contains(index - 1))
        {
            token = node.FindToken(index - 1, includeWhitespace);
        }

        return token.Parent;
    }

    public static SyntaxNode? FindNode(this SyntaxNode @this, Language.Syntax.TextSpan span, bool includeWhitespace = false, bool getInnermostNodeForTie = false)
    {
        if (!@this.FullSpan.Contains(span))
        {
            throw new ArgumentOutOfRangeException(nameof(span));
        }

        var node = @this.FindToken(span.Start, includeWhitespace)
            .Parent
            !.FirstAncestorOrSelf<SyntaxNode>(a => a.FullSpan.Contains(span));

        node.AssumeNotNull();

        // Tie-breaking.
        if (!getInnermostNodeForTie)
        {
            var cuRoot = node.Ancestors().Last();

            while (true)
            {
                var parent = node.Parent;
                // NOTE: We care about FullSpan equality, but FullWidth is cheaper and equivalent.
                if (parent == null || parent.FullWidth != node.FullWidth)
                {
                    break;
                }

                // prefer child over compilation unit
                if (parent == cuRoot)
                {
                    break;
                }

                node = parent;
            }
        }

        return node;
    }

    /// <summary>
    /// Extracts source nodes from a syntax node.
    /// </summary>
    /// <param name="rootNode">The root node.</param>
    /// <returns>A list of source nodes.</returns>
    public static IList<RazorSourceNode> ExtractSourceNodes(this SyntaxNode rootNode)
    {
        var sourceNodes = new List<RazorSourceNode>();
        var stack = new Stack<SyntaxNode>();
        stack.Push(rootNode);

        while (stack.Count > 0)
        {
            var currentNode = stack.Pop();

            if (currentNode.IsScopedNode(out var scope) || currentNode.IsSimpleNode())
            {
                RazorSourceNode sourceNode;
                if (scope != Scope.Unknown)
                {
                    sourceNode = new RazorScopedNode(currentNode, scope);
                }
                else
                {
                    sourceNode = new RazorSimpleNode(currentNode);
                }

                sourceNodes.Add(sourceNode);
                continue;
            }

            // Add child nodes to the stack in reverse order for depth-first search
            foreach (var childNode in currentNode.ChildNodes().Reverse())
            {
                stack.Push(childNode);
            }
        }

        return sourceNodes;
    }

    /// <summary>
    /// Determines whether the node is a scoped node.
    /// </summary>
    /// <param name="node">The syntax node.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>True if node is a scoped node, false otherwise.</returns>
    public static bool IsScopedNode(this SyntaxNode node, out Scope scope)
    {
        scope = Scope.Unknown;
        var nodeType = node.GetType();
        if (NodeTypes.Exclude.Contains(nodeType))
        {
            return false;
        }

        foreach (var scopeTypes in NodeTypes.Scoped)
        {
            foreach (var type in scopeTypes.Value)
            {
                if (nodeType == type)
                {
                    scope = scopeTypes.Key;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the node is a simple node.
    /// </summary>
    /// <param name="node">The syntax node.</param>
    /// <returns>True if node is a simple node, false otherwise.</returns>
    public static bool IsSimpleNode(this SyntaxNode node)
    {
        var nodeType = node.GetType();
        if (NodeTypes.Exclude.Contains(nodeType))
        {
            return false;
        }

        foreach (var type in NodeTypes.Simple)
        {
            if (node.GetType() == type)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the body syntax of the specified node.
    /// </summary>
    /// <param name="node">The node to get the body syntax of.</param>
    /// <returns>The body syntax of the specified node, if it has any.</returns>
    public static SyntaxNode? GetBodySyntax(this SyntaxNode node)
    {
        return node switch
        {
            CSharpExplicitExpressionSyntax explicitExpression => explicitExpression.Body,
            CSharpImplicitExpressionSyntax implicitExpression => implicitExpression.Body,
            CSharpStatementSyntax statementSyntax => statementSyntax.Body,
            RazorDirectiveSyntax razorDirectiveSyntax => razorDirectiveSyntax.Body,
            _ => null
        };
    }
}
