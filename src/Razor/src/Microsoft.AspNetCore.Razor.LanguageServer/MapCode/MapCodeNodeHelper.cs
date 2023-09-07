// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;
internal static class MapCodeNodeHelper
{
    /*public static IList<RazorSourceNode> ExtractSourceNodes(SyntaxNode rootNode)
    {
        var sourceNodes = new List<RazorSourceNode>();
        var stack = new Stack<SyntaxNode>();
        stack.Push(rootNode);

        while (stack.Count > 0)
        {
            var currentNode = stack.Pop();

            if (IsScopedNode(currentNode, out var scope) || IsSimpleNode(currentNode))
            {

            }
        }
    }

    public static bool IsScopedNode(SyntaxNode node, out Scope scope)
    {
        scope = Scope.Unknown;
        var nodeType = node.GetType();
        if (NodeTypes.Exclude.Contains(nodeType))
        {
            return false;
        }
    }*/
}
