// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode.SourceNode;

/// <summary>
/// A simple node can be represented as any node that doesn't have
/// a scope. It can be a declaration field, an event, delegate, etc.
/// </summary>
internal class RazorSimpleNode(SyntaxNode node) : RazorSourceNode(node)
{
}
