// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.MapCode.SourceNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

/// <summary>
/// Represents an invalid insertion or replacement operation.
/// </summary>
internal record InvalidMappedNode
{
    /// <summary>
    /// The invalid source node.
    /// </summary>
    public readonly RazorSourceNode Node;

    /// <summary>
    /// The reason why the mapping operation is invalid.
    /// </summary>
    public readonly InvalidMappedNodeReason Reason;

    public InvalidMappedNode(RazorSourceNode node, InvalidMappedNodeReason reason)
    {
        Node = node;
        Reason = reason;
    }
}
