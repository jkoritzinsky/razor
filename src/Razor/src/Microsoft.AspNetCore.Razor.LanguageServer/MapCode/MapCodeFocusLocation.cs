// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.LanguageServer.Protocol;

#pragma warning disable RS0016 // Add public types and members to the declared API
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

/// <summary>
/// Prioritized document locations used for MapCodeParams contextual focus information
/// (typically things like cursor location, viewport ranges, current selection, etc).
/// </summary>
[DataContract]

public class MapCodeFocusLocation
{
    /// <summary>
    /// Location for this focus item.
    /// </summary>
    [DataMember(Name = "location")]
    public Location Location
    {
        get;
        set;
    }

    /// <summary>
    /// Priority to evaluate this focus item under. Multiple items can share the same Priority.
    /// </summary>
    [DataMember(Name = "priority")]
    public int Priority
    {
        get;
        set;
    }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning restore RS0016 // Add public types and members to the declared API
