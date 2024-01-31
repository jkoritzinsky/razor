﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;

[RazorLanguageServerEndpoint(Methods.WorkspaceDidChangeConfigurationName)]
internal interface IDidChangeConfigurationEndpoint : IRazorNotificationHandler<DidChangeConfigurationParams>
{
}
