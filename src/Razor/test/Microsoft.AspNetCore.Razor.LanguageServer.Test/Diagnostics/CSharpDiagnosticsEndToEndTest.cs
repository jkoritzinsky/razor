// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;

public class CSharpDiagnosticsEndToEndTest : SingleServerDelegatingEndpointTestBase
{
    public CSharpDiagnosticsEndToEndTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    private string GetFileContents()
    {
        var sb = new StringBuilder();

        sb.Append("""
            @using System;
            """);

        for (var i = 0; i < (1); i++) // not 100
        {
            sb.Append($$"""
            @{
                var y{{i}} = 456;
            }

            <div>
                <p>Hello there Mr {{i}}</p>
            </div>
            """);
        }

        sb.Append("""

             <div></div>

             @functions
             {
                public void M()
                {
                    {|CS0104:CallOnMe|}();
                }
             }

             """);

        return sb.ToString();
    }

    [Fact]
    public async Task Handle()
    {
        var input = GetFileContents();
        //var input = """

        //    <div></div>

        //    @functions
        //    {
        //        public void M()
        //        {
        //            {|CS0104:CallOnMe|}();
        //        }
        //    }

        //    """;

        await ValidateDiagnosticsAsync(input);
    }

    private async Task ValidateDiagnosticsAsync(string input)
    {
        TestFileMarkupParser.GetSpans(input, out input, out ImmutableDictionary<string, ImmutableArray<TextSpan>> spans);

        var codeDocument = CreateCodeDocument(input);
        var sourceText = codeDocument.GetSourceText();
        var razorFilePath = "file://C:/path/test.razor";
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var requestContext = new RazorRequestContext(documentContext, Logger, null!);

        var translateDiagnosticsService = new RazorTranslateDiagnosticsService(DocumentMappingService, LoggerFactory);
        var diagnosticsEndPoint = new DocumentPullDiagnosticsEndpoint(LanguageServerFeatureOptions, translateDiagnosticsService, LanguageServer);//DiagnosticsLanguageServer

        var diagnosticsRequest = new VSInternalDocumentDiagnosticsParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri }
        };
        var diagnostics = await diagnosticsEndPoint.HandleRequestAsync(diagnosticsRequest, requestContext, DisposalToken);

        var actual = diagnostics!.SelectMany(d => d.Diagnostics!);

        // Because the test razor project isn't set up properly, we get some extra diagnostics that we don't care about
        // so lets just validate that we get the ones we expect. We're testing the communication and translation between
        // Razor and C# after all, not whether our test infra can create a fully working project with all references.
        foreach (var (code, span) in spans)
        {
            // If any future test requires multiple diagnostics of the same type, please change this code :)
            var diagnostic = Assert.Single(actual, d => d.Code == code);
            Assert.Equal(span.First(), diagnostic.Range.AsTextSpan(sourceText));
        }
    }
}
