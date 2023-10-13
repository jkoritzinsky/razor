// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

/// <summary>
/// The level of the scope where this node is located.
/// NOTE: The scope does not represent where this node will be inserted.
/// The scope represents the hierarchy this node has.
/// For example, a Class Scope node means that it needs to be placed next to
/// other classes or interfaces, and not inside them.
/// NOTE: The order in which these scopes are setup on this enum matter.
/// They should go from lower to higher in terms of what goes inside what.
/// For example, Class is the highest scope here, because the class will usually contain methods, and methods cannot contain classes.
/// Same with statements.
/// </summary>
internal enum Scope
{
    Unknown,
    CSharpExpression
}
