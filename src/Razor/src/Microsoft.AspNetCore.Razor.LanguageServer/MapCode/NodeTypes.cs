// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

internal static class NodeTypes
{
    /// <summary>
    /// These are excluded types that we know we don't want to compare against
    /// all other scoped or simple types.
    /// </summary>
    public static HashSet<Type> Exclude =
    [
        typeof(RazorDocumentSyntax),
    ];

    /// <summary>
    /// The list of supported Scoped nodes.
    /// </summary>
    public static IReadOnlyDictionary<Scope, Type[]> Scoped = new Dictionary<Scope, Type[]>
    {
        [Scope.CSharpExpression] = [
            typeof(CSharpExplicitExpressionSyntax),
            typeof(CSharpImplicitExpressionSyntax),
            typeof(CSharpStatementSyntax),
            typeof(RazorDirectiveSyntax)
        ]
    };

    /// <summary>
    /// The simple node types.
    /// </summary>
    public static Type[] Simple =
    [
        // C#
        typeof(CSharpExpressionLiteralSyntax),
        typeof(CSharpStatementLiteralSyntax),
        typeof(CSharpTransitionSyntax),

        // Comment
        typeof(RazorCommentBlockSyntax),

        // Markup
        typeof(MarkupEphemeralTextLiteralSyntax),
        typeof(MarkupTagHelperElementSyntax),
        typeof(MarkupTextLiteralSyntax),
        typeof(MarkupElementSyntax),
    ];
}
