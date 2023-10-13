// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode.SourceNode;

/// <summary>
/// Represents a scoped node.
/// A scope node is a syntax node that contains a scope or a body.
/// </summary>
internal class RazorScopedNode(SyntaxNode node, Scope scope) : RazorSourceNode(node)
{
    /// <summary>
    /// Gets the scope of the node.
    /// </summary>
    public Scope Scope { get; private set; } = scope;
}
