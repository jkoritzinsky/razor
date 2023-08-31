// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

public abstract class RazorSemanticTokenInfoServiceTest : SemanticTokenTestBase
{
    public RazorSemanticTokenInfoServiceTest(ITestOutputHelper testOutput, bool usePreciseSemanticTokenRanges)
        : base(testOutput, usePreciseSemanticTokenRanges)
    {
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_RazorIfNotReady()
    {
        var documentText =
            """
                <p></p>@{
                    var d = "t";
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = (
            new ProvideSemanticTokensResponse(tokens: Array.Empty<int[]>(), hostDocumentSyncVersion: 1),
            new Dictionary<Range, int[]>()
            {
                { razorRange, Array.Empty<int>() },
            });

        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens, documentVersion: 1);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharpBlock_HTML()
    {
        var documentText =
            """
                @{
                    var d = "t";
                    <p>HTML @d</p>
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_Nested_HTML()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <!--@{var d = "string";@<a></a>}-->
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_VSCodeWorks()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                @{ var d = }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = (
            new ProvideSemanticTokensResponse(tokens: Array.Empty<int[]>(), hostDocumentSyncVersion: null),
            new Dictionary<Range, int[]>()
            {
                { razorRange, Array.Empty<int>() },
            });
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens, documentVersion: 1);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_Explicit()
    {
        var documentText =
            """
                @using System
                @addTagHelper *, TestAssembly
                @(DateTime.Now)
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_Implicit()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                @{ var d = "txt";}
                @d
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_VersionMismatch()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                @{ var d = }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens, documentVersion: 21);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_FunctionAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                @{ var d = }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_StaticModifier()
    {
        var documentText =
            """
                @code
                {
                    static int x = 1;
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_MultipleBlankLines()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly

                <p>first
                second</p>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_IncompleteTag()
    {
        var documentText =
            """
                <str class='
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.Empty(csharpTokens.Item1.Tokens);
    }

    [Fact]
    public async Task GetSemanticTokens_MinimizedHTMLAttribute()
    {
        var documentText =
            """
                <p attr />
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.Empty(csharpTokens.Item1.Tokens);
    }

    [Fact]
    public async Task GetSemanticTokens_MinimizedHTMLAsync()
    {
        var documentText = """
                @addTagHelper *, TestAssembly
                <input/>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_HTMLCommentAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <!-- comment with comma's -->
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_PartialHTMLCommentAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <!-- comment
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_HTMLIncludesBang()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <!input/>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_HalfOfCommentAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                @* comment
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_NoAttributesAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_WithAttributeAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 bool-val='true'></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_MinimizedAttribute_BoundAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 bool-val></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_MinimizedAttribute_NotBoundAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 notbound></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_IgnoresNonTagHelperAttributesAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 bool-val='true' class='display:none'></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_TagHelpersNotAvailableInRazorAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 bool-val='true' class='display:none'></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_DoesNotApplyOnNonTagHelpersAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <p bool-val='true'></p>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_MinimizedDirectiveAttributeParameters()
    {
        // Capitalized, non-well-known-HTML elements should not be marked as TagHelpers
        var documentText =
            """
                @addTagHelper *, TestAssembly
                }<NotATagHelp @minimized:something />
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_ComponentAttributeAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <Component1 bool-val=""true""></Component1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_ComponentAttribute_DoesntGetABackground()
    {
        // Need C# around the component for the C# range to be valid, to correctly validate the attribute handling
        var documentText =
            """
                @DateTime.Now

                <Component1 Title=""Hi there I'm a string""></Component1>

                @DateTime.Now
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens, withCSharpBackground: true);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_DirectiveAttributesParametersAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <Component1 @test:something='Function'></Component1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_NonComponentsDoNotShowInRazorAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 bool-val='true'></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_DirectivesAsync()
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <Component1 @test='Function'></Component1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_HandleTransitionEscape()
    {
        var documentText =
            """
                @@text
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.Empty(csharpTokens.Item1.Tokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_DoNotColorNonTagHelpersAsync()
    {
        var documentText =
            """
                <p @test='Function'></p>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.Empty(csharpTokens.Item1.Tokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_DoesNotApplyOnNonTagHelpersAsync()
    {
        var documentText = """
                @addTagHelpers *, TestAssembly
                <p></p>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_CodeDirectiveAsync()
    {
        var documentText =
            """
                @code {}
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.Empty(csharpTokens.Item1.Tokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_CodeDirectiveBodyAsync()
    {
        var documentText = """
                @using System
                @code {
                    public void SomeMethod()
                    {
                        @DateTime.Now
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_UsingDirective()
    {
        var documentText =
            """
                @using System.Threading
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_FunctionsDirectiveAsync()
    {
        var documentText =
            """
                @functions {}
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.Empty(csharpTokens.Item1.Tokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_NestedTextDirectives()
    {
        var documentText =
            """
                @using System
                @functions {
                    private void BidsByShipment(string generatedId, int bids)
                    {
                        if (bids > 0)
                        {
                            <a class=""Thing"">
                                @if(bids > 0)
                                {
                                    <text>@DateTime.Now</text>
                                }
                            </a>
                        }
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_NestedTransitions()
    {
        var documentText =
            """
                @using System
                @functions {
                    Action<object> abc = @<span></span>;
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.NotEmpty(csharpTokens.Item1.Tokens);

        if (!UsePreciseSemanticTokenRanges)
        {
            Assert.NotNull(csharpTokens.Item2);
            var fullResponse = Assert.Single(csharpTokens.Item2);
            Assert.True(fullResponse.Value.Count() % 5 == 0);
            return;
        }

        var savedToStitch = csharpTokens.Item2;
        Assert.NotNull(savedToStitch);
        Assert.True(savedToStitch.Count() > 1);
        var stitched = Array.Empty<int>();
        foreach (var item in savedToStitch)
        {
            stitched = RazorSemanticTokensInfoService.StitchSemanticTokenResponsesTogether(savedToStitch.Values.ToArray());
        }

        UsePreciseSemanticTokenRanges = false;
        csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.NotEmpty(csharpTokens.Item1.Tokens);
        Assert.NotNull(csharpTokens.Item2);
        Assert.Single(csharpTokens.Item2);
        var old = csharpTokens.Item2.First().Value;
        Assert.True(old.Length >= stitched.Length);

        var stitchedhashset = new HashSet<int[]>();
        for (var i = 0; i < stitched.Length; i += 5)
        {
            var arr = new int[5];
            Array.Copy(stitched, i, arr, 0, 5);
            stitchedhashset.Add(arr);
        }

        var hashset2 = new HashSet<int[]>();
        for (var i = 0; i < old.Length; i += 5)
        {
            var arr = new int[5];
            Array.Copy(old, i, arr, 0, 5);
            hashset2.Add(arr);
        }


        foreach (var item in stitchedhashset)
        {
            Assert.Contains(item, hashset2);
        }
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_CommentAsync()
    {
        var documentText = """
                @* A comment *@
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.Empty(csharpTokens.Item1.Tokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_MultiLineCommentMidlineAsync()
    {
        var documentText =
            """
                <a />@* kdl
                skd
                slf*@
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.Empty(csharpTokens.Item1.Tokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_MultiLineCommentWithBlankLines()
    {
        var documentText =
            """
                @* kdl

                skd

                        sdfasdfasdf
                slf*@
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.Empty(csharpTokens.Item1.Tokens);
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/razor/issues/8176")]
    public async Task GetSemanticTokens_Razor_MultiLineCommentWithBlankLines_LF()
    {
        var documentText = "@* kdl\n\nskd\n    \n        sdfasdfasdf\nslf*@";

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.Empty(csharpTokens.Item1.Tokens);
    }

    [Fact]
    public async Task GetSemanticTokens_Razor_MultiLineCommentAsync()
    {
        var documentText =
            """
                @*stuff
                things *@
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Item1.Tokens);
        Assert.Empty(csharpTokens.Item1.Tokens);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_Static()
    {
        var documentText = """
                @using System
                @code
                {
                    private static bool _isStatic;

                    public void M()
                    {
                        if (_isStatic)
                        {
                        }
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_Static_WithBackground()
    {
        var documentText = """
                @using System
                @code
                {
                    private static bool
                        _isStatic;

                    public void M()
                    {
                        if (_isStatic)
                        {
                        }
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens, withCSharpBackground: true);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_WithBackground()
    {
        var documentText = """
                @using System
                @code
                {
                    private static bool _isStatic;

                    public void M()
                    {
                        if (_isStatic)
                        {
                        }
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens, withCSharpBackground: true);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_WitRenderFragment()
    {
        var documentText = """
                <div>This is some HTML</div>
                @code
                {
                    public void M()
                    {
                        RenderFragment x = @<div>This is some HTML</div>;
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_WitRenderFragmentAndBackground()
    {
        var documentText = """
                <div>This is some HTML</div>
                @code
                {
                    public void M()
                    {
                        RenderFragment x = @<div>This is some HTML</div>;
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens, withCSharpBackground: true);
    }

    [Fact]
    public async Task GetSemanticTokens_CSharp_ExplicitStatement_WithBackground()
    {
        var documentText = """
            @DateTime.Now

            @("hello" + "\\n" + "world" + Environment.NewLine + "how are you?")
            """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens, withCSharpBackground: true);
    }

    [Fact]
    public void StitchSemanticTokenResponsesTogether_OnNullInput_ReturnsEmptyResponseData()
    {
        // Arrange
        int[][]? responseData = null;

        // Act
        var result = RazorSemanticTokensInfoService.StitchSemanticTokenResponsesTogether(responseData);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void StitchSemanticTokenResponsesTogether_OnEmptyInput_ReturnsEmptyResponseData()
    {
        // Arrange
        var responseData = Array.Empty<int[]>();

        // Act
        var result = RazorSemanticTokensInfoService.StitchSemanticTokenResponsesTogether(responseData);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void StitchSemanticTokenResponsesTogether_ReturnsCombinedResponseData()
    {
        // Arrange
        var responseData = new int[][] {
             new int[] { 0, 0, 0, 0, 0,
                         1, 0, 0, 0, 0,
                         1, 0, 0, 0, 0,
                         0, 5, 0, 0, 0,
                         0, 3, 0, 0, 0,
                         2, 2, 0, 0, 0,
                         0, 3, 0, 0, 0 },
             new int[] { 10, 0, 0, 0, 0,
                         1, 0, 0, 0, 0,
                         1, 0, 0, 0, 0,
                         0, 5, 0, 0, 0,
                         0, 3, 0, 0, 0,
                         2, 2, 0, 0, 0,
                         0, 3, 0, 0, 0 },
             new int[] { 14, 7, 0, 0, 0,
                         1, 0, 0, 0, 0,
                         1, 0, 0, 0, 0,
                         0, 5, 0, 0, 0,
                         0, 3, 0, 0, 0,
                         2, 2, 0, 0, 0,
                         0, 3, 0, 0, 0 },
         };

        var expectedResponseData = new int[] {
            0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 3, 0, 0, 0, 2, 2, 0, 0, 0, 0, 3, 0, 0, 0,
            6, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 3, 0, 0, 0, 2, 2, 0, 0, 0, 0, 3, 0, 0, 0,
            0, 2, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 3, 0, 0, 0, 2, 2, 0, 0, 0, 0, 3, 0, 0, 0
        };

        // Act
        var result = RazorSemanticTokensInfoService.StitchSemanticTokenResponsesTogether(responseData);

        // Assert
        Assert.Equal(expectedResponseData, result);
    }

    private async Task AssertSemanticTokensAsync(
        string documentText,
        bool isRazorFile,
        Range range,
        (ProvideSemanticTokensResponse? csResponse, Dictionary<Range, int[]>? csTokens) csharpTokens,
        IRazorSemanticTokensInfoService? service = null,
        int documentVersion = 0,
        bool withCSharpBackground = false)
    {
        await AssertSemanticTokensAsync(new DocumentContentVersion[]
        {
            new DocumentContentVersion(documentText, documentVersion)
        },
        isRazorArray: new bool[] { isRazorFile },
        range,
        service,
        csharpTokens,
        documentVersion,
        withCSharpBackground);
    }

    private async Task AssertSemanticTokensAsync(
        DocumentContentVersion[] documentTexts,
        bool[] isRazorArray,
        Range range,
        IRazorSemanticTokensInfoService? service,
        (ProvideSemanticTokensResponse? csResponse, Dictionary<Range, int[]>? csTokens) csharpTokens,
        int documentVersion,
        bool withCSharpBackground)
    {
        // Arrange
        if (csharpTokens.csTokens is null)
        {
            csharpTokens.csResponse = new ProvideSemanticTokensResponse(tokens: null, csharpTokens.csResponse?.HostDocumentSyncVersion);
        }

        var (documentContexts, textDocumentIdentifiers) = CreateDocumentContext(
            documentTexts, isRazorArray, DefaultTagHelpers, documentVersion: documentVersion);

        if (service is null)
        {
            service = await GetDefaultRazorSemanticTokenInfoServiceAsync(documentContexts, csharpTokens, withCSharpBackground);
        }

        var textDocumentIdentifier = textDocumentIdentifiers.Dequeue();
        var documentContext = documentContexts.Peek();
        var correlationId = Guid.Empty;

        // Act
        var tokens = await service.GetSemanticTokensAsync(textDocumentIdentifier, range, documentContext, TestRazorSemanticTokensLegend.Instance, correlationId, DisposalToken);

        // Assert
        var sourceText = await documentContext.GetSourceTextAsync(DisposalToken);
        AssertSemanticTokensMatchesBaseline(sourceText, tokens?.Data);
    }

    private async Task<IRazorSemanticTokensInfoService> GetDefaultRazorSemanticTokenInfoServiceAsync(
        Queue<VersionedDocumentContext> documentSnapshots,
        (ProvideSemanticTokensResponse? csResponse, Dictionary<Range, int[]>? csTokens) csharpTokens,
        bool withCSharpBackground)
    {
        var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);

        if (csharpTokens.csTokens is not null)
        {
            var ranges = csharpTokens.csTokens.Keys.ToArray();
            // times called should be checked with this
        }

        languageServer
            .Setup(l => l.SendRequestAsync<SemanticTokensParams, ProvideSemanticTokensResponse?>(
                CustomMessageNames.RazorProvideSemanticTokensRangeEndpoint,
                It.IsAny<SemanticTokensParams>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(csharpTokens.csResponse);

        var documentContextFactory = new TestDocumentContextFactory(documentSnapshots);
        var documentMappingService = new RazorDocumentMappingService(FilePathService, documentContextFactory, LoggerFactory);

        var configurationSyncService = new Mock<IConfigurationSyncService>(MockBehavior.Strict);

        var options = RazorLSPOptions.Default with { ColorBackground = withCSharpBackground };
        configurationSyncService
            .Setup(c => c.GetLatestOptionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<RazorLSPOptions?>(options));

        var optionsMonitorCache = new OptionsCache<RazorLSPOptions>();

        var optionsMonitor = TestRazorLSPOptionsMonitor.Create(
            configurationSyncService.Object,
            optionsMonitorCache);

        await optionsMonitor.UpdateAsync(CancellationToken.None);

        var featureOptions = Mock.Of<LanguageServerFeatureOptions>(options =>
            options.DelegateToCSharpOnDiagnosticPublish == true &&
            options.UsePreciseSemanticTokenRanges == UsePreciseSemanticTokenRanges &&
            options.CSharpVirtualDocumentSuffix == ".ide.g.cs" &&
            options.HtmlVirtualDocumentSuffix == "__virtual.html",
            MockBehavior.Strict);

        return new RazorSemanticTokensInfoService(
            languageServer.Object,
            documentMappingService,
            optionsMonitor,
            featureOptions,
            LoggerFactory);
    }

    private static Range GetRange(string text)
    {
        var lines = text.Split(Environment.NewLine);

        var range = new Range
        {
            Start = new Position { Line = 0, Character = 0 },
            End = new Position { Line = lines.Length, Character = 0 }
        };

        return range;
    }

    private class TestInitializeManager : IInitializeManager<InitializeParams, InitializeResult>
    {
        public InitializeParams GetInitializeParams()
        {
            throw new NotImplementedException();
        }

        public InitializeResult GetInitializeResult()
        {
            throw new NotImplementedException();
        }

        public void SetInitializeParams(InitializeParams request)
        {
            throw new NotImplementedException();
        }
    }

    private class TestDocumentContextFactory : DocumentContextFactory
    {
        private readonly Queue<VersionedDocumentContext> _documentContexts;

        public TestDocumentContextFactory(Queue<VersionedDocumentContext> documentContexts)
        {
            _documentContexts = documentContexts;
        }

        protected override DocumentContext? TryCreateCore(Uri documentUri, VSProjectContext? projectContext, bool versioned)
        {
            var document = _documentContexts.Count == 1 ? _documentContexts.Peek() : _documentContexts.Dequeue();

            return document;
        }
    }
}
