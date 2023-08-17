// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal partial class RazorCustomMessageTarget
{
    // Called by the Razor Language Server to provide ranged semantic tokens from the platform.
    [JsonRpcMethod(CustomMessageNames.RazorProvideSemanticTokensRangeEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task<ProvideSemanticTokensResponse?> ProvideSemanticTokensRangeAsync(
        ProvideSemanticTokensRangeParams semanticTokensParams,
        CancellationToken cancellationToken)
    {
        if (semanticTokensParams is null)
        {
            throw new ArgumentNullException(nameof(semanticTokensParams));
        }

        if (semanticTokensParams.Ranges is null)
        {
            throw new ArgumentNullException(nameof(semanticTokensParams.Ranges));
        }

        var (synchronized, csharpDoc) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
            _documentManager, (int)semanticTokensParams.RequiredHostDocumentVersion, semanticTokensParams.TextDocument, cancellationToken);

        if (csharpDoc is null)
        {
            return null;
        }

        if (!synchronized)
        {
            // If we're unable to synchronize we won't produce useful results, but we have to indicate
            // it's due to out of sync by providing the old version
            return new ProvideSemanticTokensResponse(tokens: null, hostDocumentSyncVersion: -1);
        }

        // Ensure the C# ranges are sorted
        Array.Sort(semanticTokensParams.Ranges, static (r1, r2) => r1.CompareTo(r2));

        semanticTokensParams.TextDocument.Uri = csharpDoc.Uri;
        var requestTasks = new List<Task<ReinvocationResponse<VSSemanticTokensResponse>?>>(semanticTokensParams.Ranges.Length);
        var textBuffer = csharpDoc.Snapshot.TextBuffer;
        var languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        var lspMethodName = Methods.TextDocumentSemanticTokensRangeName;

        foreach (var range in semanticTokensParams.Ranges)
        {
            var newParams = new SemanticTokensRangeParams
            {
                TextDocument = semanticTokensParams.TextDocument,
                PartialResultToken = semanticTokensParams.PartialResultToken,
                Range = range,
            };

            using var _ = _telemetryReporter.TrackLspRequest(lspMethodName, languageServerName, semanticTokensParams.CorrelationId);
            var task = _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensRangeParams, VSSemanticTokensResponse>(
                textBuffer,
                lspMethodName,
                languageServerName,
                newParams,
                cancellationToken);
            requestTasks.Add(task);
        }

        var results = await Task.WhenAll(requestTasks).ConfigureAwait(false);
        var nonEmptyResults = results.Select(r => r?.Response).WithoutNull().ToArray();

        if (nonEmptyResults.Length != semanticTokensParams.Ranges.Length)
        {
            // Weren't able to re-invoke C# semantic tokens but we have to indicate it's due to out of sync by providing the old version
            return new ProvideSemanticTokensResponse(tokens: null, hostDocumentSyncVersion: csharpDoc.HostDocumentSyncVersion);
        }

        var data = StitchSemanticTokenResponsesTogether(nonEmptyResults, semanticTokensParams.Ranges);

        var response = new ProvideSemanticTokensResponse(data, semanticTokensParams.RequiredHostDocumentVersion);

        return response;
    }

    private static int[] StitchSemanticTokenResponsesTogether(SemanticTokens[] responses, Range[] ranges)
    {
        var count = responses.Sum(r => r.Data.Length);
        var data = new int[count];
        var dataIndex = 0;

        for (var i = 0; i < responses.Length; i++)
        {
            var result = responses[i];
            if (i == 0)
            {
                Array.Copy(result.Data, data, result.Data.Length);
            }
            else if (result.Data.Length > 0)
            {
                // The first item in result.Data will need to have it's line/col offset calculated
                var prevRange = ranges[i - 1];
                var curRange = ranges[i];

                var lineDelta = curRange.Start.Line - prevRange.End.Line;
                data[dataIndex] = lineDelta;

                if (lineDelta == 0)
                {
                    data[dataIndex + 1] = curRange.Start.Character - prevRange.End.Character;
                }
                else
                {
                    data[dataIndex + 1] = curRange.Start.Character;
                }

                // remaining items can be copied directly
                Array.Copy(result.Data, 2, data, dataIndex + 1, result.Data.Length - 2);
            }

            dataIndex += result.Data.Length;
        }

        return data;
    }
}
