// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.MapCode;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.MapCode;

[UseExportProvider]
public class MapCodeTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    private const string RazorFilePath = "C:/path/to/file.razor";

    [Fact]
    public async Task HandleRazorInsertionAsync()
    {
        var originalCode = """
                <h3>Component</h3>

                @code {

                }
                $$
                """;

        var codeToMap = """
            <PageTitle>Title</PageTitle>
            """;

        var expectedEdit = new WorkspaceEdit
        {
            Changes = new Dictionary<string, TextEdit[]>
            {
                {
                    RazorFilePath,
                    new TextEdit[]
                    {
                        new()
                        {
                            NewText = "<PageTitle>Title</PageTitle>",
                            Range = new Range
                            {
                                Start = new Position(1, 0),
                                End = new Position(1, 0)
                            }
                        }
                    }
                }
            }
        };

        await VerifyCodeMappingAsync(originalCode, new[] { codeToMap }, expectedEdit);
    }

    private async Task VerifyCodeMappingAsync(string originalCode, string[] codeToMap, LSP.WorkspaceEdit expectedEdit, string razorFilePath = RazorFilePath)
    {
        // Arrange
        TestFileMarkupParser.GetPositionAndSpans(originalCode, out var output, out int cursorPosition, out ImmutableArray<TextSpan> spans);
        var codeDocument = CreateCodeDocument(output);
        var csharpSourceText = codeDocument.GetCSharpSourceText();
        var csharpDocumentUri = new Uri(razorFilePath + "__virtual.g.cs");
        var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(
            csharpSourceText, csharpDocumentUri, new VSInternalServerCapabilities(), razorSpanMappingService: null, DisposalToken);
        await csharpServer.OpenDocumentAsync(csharpDocumentUri, csharpSourceText.ToString());

        var documentContextFactory = new TestDocumentContextFactory(razorFilePath, codeDocument, version: 1337);
        var languageServer = new MapCodeServer(csharpServer, csharpDocumentUri);
        var documentMappingService = new RazorDocumentMappingService(FilePathService, documentContextFactory, LoggerFactory);

        var endpoint = new MapCodeEndpoint(documentMappingService, documentContextFactory, languageServer);

        codeDocument.GetSourceText().GetLineAndOffset(cursorPosition, out var line, out var offset);

        var mappings = new MapCodeMapping[]
        {
            new MapCodeMapping
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = new Uri(razorFilePath)
                },
                FocusLocations =
                [
                    [
                        new Location
                        {
                            Range = new Range
                            {
                                Start = new Position(line, offset),
                                End = new Position(line, offset)
                            },
                            Uri = new Uri(razorFilePath)
                        }
                    ]
                ],
                Contents = codeToMap
            }
        };
        var request = new MapCodeParams
        {
            Mappings = mappings
        };

        var documentContext = documentContextFactory.TryCreateForOpenDocument(request.Mappings[0].TextDocument!);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.Equal(expectedEdit, result);
    }

    private class MapCodeServer : ClientNotifierServiceBase
    {
        private readonly CSharpTestLspServer _csharpServer;
        private readonly Uri _csharpDocumentUri;

        public MapCodeServer(CSharpTestLspServer csharpServer, Uri csharpDocumentUri)
        {
            _csharpServer = csharpServer;
            _csharpDocumentUri = csharpDocumentUri;
        }

        public override Task OnInitializedAsync(VSInternalClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override Task SendNotificationAsync<TParams>(string method, TParams @params, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task SendNotificationAsync(string method, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override async Task<TResponse> SendRequestAsync<TParams, TResponse>(string method, TParams @params, CancellationToken cancellationToken)
        {
            Assert.Equal(CustomMessageNames.RazorMapCodeEndpoint, method);
            var delegatedMapCodeParams = Assert.IsType<DelegatedMapCodeParams>(@params);

            var mappings = new MapCodeMapping[]
            {
                new() {
                    TextDocument = new TextDocumentIdentifier()
                    {
                        Uri = _csharpDocumentUri
                    },
                    Contents = delegatedMapCodeParams.Contents,
                    FocusLocations = delegatedMapCodeParams.FocusLocations
                }
            };
            var mapCodeRequest = new MapCodeParams()
            {
                Mappings = mappings
            };

            var result = await _csharpServer.ExecuteRequestAsync<MapCodeParams, WorkspaceEdit?>(
                MapperMethods.WorkspaceMapCodeName, mapCodeRequest, cancellationToken);
            if (result is null)
            {
                return (TResponse)(object)new WorkspaceEdit();
            }

            return (TResponse)(object)result;
        }
    }
}
