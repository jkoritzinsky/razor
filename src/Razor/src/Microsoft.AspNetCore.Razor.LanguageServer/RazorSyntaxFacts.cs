﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal static class RazorSyntaxFacts
{
    /// <summary>
    /// Given an absolute index positioned in an attribute, finds the absolute index of the part of the
    /// attribute that represents the attribute name. eg. for @bi$$nd-Value it will find the absolute index
    /// of "Value"
    /// </summary>
    public static bool TryGetAttributeNameAbsoluteIndex(RazorCodeDocument codeDocument, int absoluteIndex, out int attributeNameAbsoluteIndex, out TextSpan fullAttributeNameSpan)
    {
        attributeNameAbsoluteIndex = 0;
        fullAttributeNameSpan = default;

        var tree = codeDocument.GetSyntaxTree();
        var owner = tree.GetOwner(absoluteIndex);

        var attributeName = owner?.Parent switch
        {
            MarkupTagHelperAttributeSyntax att => att.Name,
            MarkupMinimizedTagHelperAttributeSyntax att => att.Name,
            MarkupTagHelperDirectiveAttributeSyntax att => att.Name,
            MarkupMinimizedTagHelperDirectiveAttributeSyntax att => att.Name,
            _ => null
        };

        if (attributeName is null)
        {
            return false;
        }

        // Can't get to this point if owner was null, but the compiler doesn't know that
        Assumes.NotNull(owner);

        fullAttributeNameSpan = GetFullAttributeNameSpan(owner.Parent);

        // The GetOwner method can be surprising, eg. Foo="$$Bar" will return the starting quote of the attribute value,
        // but its parent is the attribute name. Easy enough to filter that sort of thing out by just requiring
        // the caret position to be somewhere within the attribute name.
        if (!fullAttributeNameSpan.Contains(absoluteIndex))
        {
            return false;
        }

        if (attributeName.LiteralTokens is [{ } name])
        {
            var attribute = name.Content;
            if (attribute.StartsWith("bind-"))
            {
                attributeNameAbsoluteIndex = attributeName.SpanStart + 5;
            }
            else
            {
                attributeNameAbsoluteIndex = attributeName.SpanStart;
            }

            return true;
        }

        return false;
    }

    private static TextSpan GetFullAttributeNameSpan(SyntaxNode node)
    {
        return node switch
        {
            MarkupTagHelperAttributeSyntax att => att.Name.Span,
            MarkupMinimizedTagHelperAttributeSyntax att => att.Name.Span,
            MarkupTagHelperDirectiveAttributeSyntax att => CalculateFullSpan(att.Name, att.ParameterName, att.Transition),
            MarkupMinimizedTagHelperDirectiveAttributeSyntax att => CalculateFullSpan(att.Name, att.ParameterName, att.Transition),
            _ => Assumed.Unreachable<TextSpan>(),
        };

        static TextSpan CalculateFullSpan(MarkupTextLiteralSyntax attributeName, MarkupTextLiteralSyntax? parameterName, RazorMetaCodeSyntax? transition)
        {
            var start = attributeName.SpanStart;
            var length = attributeName.Span.Length;

            // The transition is the "@" if its present
            if (transition is not null)
            {
                start -= 1;
                length += 1;
            }

            // The parameter is, for example, the ":after" but does not include the colon, so we have to account for it
            if (parameterName is not null)
            {
                length += 1 + parameterName.Span.Length;
            }

            return new TextSpan(start, length);
        }
    }
}
