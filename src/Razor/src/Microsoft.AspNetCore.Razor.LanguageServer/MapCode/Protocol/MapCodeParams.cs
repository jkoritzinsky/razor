// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.LanguageServer.Protocol
{
    /// <summary>
    /// LSP Params for textDocument/mapCode calls.
    /// </summary>
    [DataContract]
    public class MapCodeParams : ITextDocumentParams
    {
        /// <summary>
        /// Identifier for the document the contents are supposed to be mapped into.
        /// </summary>
        [DataMember(Name = "textDocument")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public TextDocumentIdentifier TextDocument
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            get;
            set;
        }

        /// <summary>
        /// Strings of code/text to map into TextDocument.
        /// </summary>
        [DataMember(Name = "contents")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string[] Contents
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            get;
            set;
        }

        /// <summary>
        /// Prioritized Locations to be used when applying heuristics. For example, cursor location,
        /// related classes (in other documents), viewport, etc.
        /// </summary>
        [DataMember(Name = "focusLocations")]
        public MapCodeFocusLocation[]? FocusLocations
        {
            get;
            set;
        }

        /// <summary>
        /// Changes that should be applied to the workspace by the mapper before performing
        /// the mapping operation.
        /// </summary>
        [DataMember(Name = "updates")]
        public WorkspaceEdit? Updates
        {
            get;
            set;
        }
    }
}
