// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

/// <summary>
/// Represents the reasons why an insertion operation cannot be performed.
/// </summary>
internal enum InvalidMappedNodeReason
{
    /// <summary>
    /// The reason for the failure is unknown.
    /// </summary>
    Unknown,

    /// <summary>
    /// The identifier being inserted already exists in the target context.
    /// </summary>
    InsertIdentifierAlreadyExistsOnTarget,

    /// <summary>
    /// The identifier being replaced does not exist in the target context.
    /// </summary>
    ReplaceIdentifierMissingOnTarget,
}
