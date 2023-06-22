﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal sealed class SnapshotResolver
{
    private readonly ProjectSnapshotManagerAccessor _projectSnapshotManagerAccessor;

    // Internal for testing
    internal readonly HostProject MiscellaneousHostProject;

    public SnapshotResolver(ProjectSnapshotManagerAccessor projectSnapshotManagerAccessor)
    {
        _projectSnapshotManagerAccessor = projectSnapshotManagerAccessor ?? throw new ArgumentNullException(nameof(projectSnapshotManagerAccessor));

        var miscellaneousProjectPath = Path.Combine(TempDirectory.Instance.DirectoryPath, "__MISC_RAZOR_PROJECT__");
        MiscellaneousHostProject = new HostProject(miscellaneousProjectPath, RazorDefaults.Configuration, RazorDefaults.RootNamespace);
    }

    /// <summary>
    /// Resolves a project that contains the given document path.
    /// </summary>
    /// <returns><see langword="true"/> if a project is found</returns>
    public bool TryResolveProject(string documentFilePath, [NotNullWhen(true)] out IProjectSnapshot? projectSnapshot)
        => TryResolve(documentFilePath, out projectSnapshot, out var _);

    /// <summary>
    /// Resolves a document that is contained in a project
    /// </summary>
    /// <returns><see langword="true"/> if a document is found</returns>
    public bool TryResolveDocument(string documentFilePath, [NotNullWhen(true)] out IDocumentSnapshot? documentSnapshot)
        => TryResolve(documentFilePath, out var _, out documentSnapshot);

    /// <summary>
    /// Finds all the projects with a directory that contains the document path. 
    /// </summary>
    /// <param name="documentFilePath"></param>
    /// <param name="includeMiscellaneous">if true, will include the <see cref="MiscellaneousHostProject"/> in the results</param>
    public IEnumerable<IProjectSnapshot> FindPotentialProjects(string documentFilePath, bool includeMiscellaneous)
    {
        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        var projects = _projectSnapshotManagerAccessor.Instance.GetProjects();
        var normalizedDocumentPath = FilePathNormalizer.Normalize(documentFilePath);
        foreach (var projectSnapshot in projects)
        {
            // Always include misc as something to check
            if (projectSnapshot.FilePath == MiscellaneousHostProject.FilePath && includeMiscellaneous)
            {
                yield return projectSnapshot;
            }

            var projectDirectory = FilePathNormalizer.GetDirectory(projectSnapshot.FilePath);
            if (normalizedDocumentPath.StartsWith(projectDirectory, FilePathComparison.Instance))
            {
                yield return projectSnapshot;
            }
        }
    }

    /// <summary>
    /// Resolves a document and containing project given a document path
    /// </summary>
    /// <returns><see langword="true"/> if a document is found and contained in a project</returns>
    public bool TryResolve(string documentFilePath, [NotNullWhen(true)] out IProjectSnapshot? projectSnapshot, [NotNullWhen(true)] out IDocumentSnapshot? document)
    {
        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        document = null;

        var normalizedDocumentPath = FilePathNormalizer.Normalize(documentFilePath);
        var potentialProjects = FindPotentialProjects(documentFilePath, includeMiscellaneous: true);
        foreach (var project in potentialProjects)
        {
            document = project.GetDocument(normalizedDocumentPath);
            if (document is not null)
            {
                projectSnapshot = project;
                return true;
            }
        }

        document = null;
        projectSnapshot = null;
        return false;
    }

    public IProjectSnapshot GetMiscellaneousProject()
        => _projectSnapshotManagerAccessor.Instance.GetOrAddLoadedProject(
            MiscellaneousHostProject.FilePath,
            MiscellaneousHostProject.Configuration,
            MiscellaneousHostProject.RootNamespace);
}