// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

internal class NodeTypes
{
    public static HashSet<Type> Exclude = new HashSet<Type> { };

    public static IReadOnlyDictionary<Scope, Type[]> Scoped = new Dictionary<Scope, Type[]>
    {
        [Scope.TopLevel] = new[]
        {
            typeof(RazorBlockSyntax)
        }
    };
};
