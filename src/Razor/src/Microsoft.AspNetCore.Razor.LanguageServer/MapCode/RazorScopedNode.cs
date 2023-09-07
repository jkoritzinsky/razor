// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

internal class RazorScopedNode : RazorSourceNode
{
    public Scope Scope { get; private set; }

    public RazorScopedNode(SyntaxNode node, Scope scope)
    {
        Scope = scope;
    }
}

internal enum Scope
{
    Unknown,
    Embedded,
    TopLevel,
}
