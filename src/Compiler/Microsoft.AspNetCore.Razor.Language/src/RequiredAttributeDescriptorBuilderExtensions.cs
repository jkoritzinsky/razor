﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.AspNetCore.Razor.Language;

public static class RequiredAttributeDescriptorBuilderExtensions
{
    internal static bool IsDirectiveAttribute(this RequiredAttributeDescriptorBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder.TryGetMetadataValue(ComponentMetadata.Common.DirectiveAttribute, out var value) &&
               value == bool.TrueString;
    }
}
