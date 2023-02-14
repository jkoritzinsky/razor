// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.VisualStudio.Text;

namespace Microsoft.CodeAnalysis.Text.Shared.Extensions
{
    internal static class TextSpanExtensions
    {
        /// <summary>
        /// Convert a <see cref="TextSpan"/> instance to a <see cref="TextSpan"/>.
        /// </summary>
        public static Span ToSpan(this TextSpan textSpan)
            => new Span(textSpan.Start, textSpan.Length);

        /// <summary>
        /// Add an offset to a <see cref="TextSpan"/>.
        /// </summary>
        public static TextSpan MoveTo(this TextSpan textSpan, int offset)
            => new TextSpan(textSpan.Start + offset, textSpan.Length);

        /// <summary>
        /// Convert a <see cref="TextSpan"/> to a <see cref="SnapshotSpan"/> on the given <see cref="ITextSnapshot"/> instance
        /// </summary>
        public static SnapshotSpan ToSnapshotSpan(this TextSpan textSpan, ITextSnapshot snapshot)
        {
            Debug.Assert(snapshot != null);
            var span = textSpan.ToSpan();
            return new SnapshotSpan(snapshot, span);
        }
    }
}
