// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.LanguageServer.Protocol
{
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
        public Location? Location
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
}
