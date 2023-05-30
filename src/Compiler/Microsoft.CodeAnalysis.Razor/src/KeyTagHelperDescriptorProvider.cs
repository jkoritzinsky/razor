﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.CodeAnalysis.Razor;

internal class KeyTagHelperDescriptorProvider : ITagHelperDescriptorProvider
{
    private static TagHelperDescriptor s_keyTagHelper;

    // Run after the component tag helper provider
    public int Order { get; set; } = 1000;

    public RazorEngine Engine { get; set; }

    public void Execute(TagHelperDescriptorProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var compilation = context.GetCompilation();
        if (compilation == null)
        {
            return;
        }

        var renderTreeBuilderType = compilation.GetTypeByMetadataName(ComponentsApi.RenderTreeBuilder.FullTypeName);
        if (renderTreeBuilderType == null)
        {
            // If we can't find RenderTreeBuilder, then just bail. We won't be able to compile the
            // generated code anyway.
            return;
        }

        var targetSymbol = context.Items.GetTargetSymbol();
        if (targetSymbol is not null && !SymbolEqualityComparer.Default.Equals(targetSymbol, renderTreeBuilderType.ContainingAssembly))
        {
            return;
        }

        context.Results.Add(GetOrCreateKeyTagHelper());
    }

    private static TagHelperDescriptor GetOrCreateKeyTagHelper()
    {
        return s_keyTagHelper ??= CreateKeyTagHelper();

        static TagHelperDescriptor CreateKeyTagHelper()
        {
            using var _ = TagHelperDescriptorBuilder.GetPooledInstance(
                ComponentMetadata.Key.TagHelperKind, "Key", ComponentsApi.AssemblyName,
                out var builder);

            builder.CaseSensitive = true;
            builder.SetDocumentation(DocumentationDescriptor.KeyTagHelper);

            builder.Metadata.Add(ComponentMetadata.SpecialKindKey, ComponentMetadata.Key.TagHelperKind);
            builder.Metadata.Add(TagHelperMetadata.Common.ClassifyAttributesOnly, bool.TrueString);
            builder.Metadata[TagHelperMetadata.Runtime.Name] = ComponentMetadata.Key.RuntimeName;

            // WTE has a bug in 15.7p1 where a Tag Helper without a display-name that looks like
            // a C# property will crash trying to create the tooltips.
            builder.SetTypeName("Microsoft.AspNetCore.Components.Key");

            builder.TagMatchingRule(rule =>
            {
                rule.TagName = "*";
                rule.Attribute(attribute =>
                {
                    attribute.Name = "@key";
                    attribute.Metadata[ComponentMetadata.Common.DirectiveAttribute] = bool.TrueString;
                });
            });

            builder.BindAttribute(attribute =>
            {
                attribute.SetDocumentation(DocumentationDescriptor.KeyTagHelper);
                attribute.Name = "@key";

                attribute.TypeName = typeof(object).FullName;
                attribute.SetMetadata(
                    CommonMetadata.PropertyName("Key"),
                    CommonMetadata.IsDirectiveAttribute);
            });

            return builder.Build();
        }
    }
}
