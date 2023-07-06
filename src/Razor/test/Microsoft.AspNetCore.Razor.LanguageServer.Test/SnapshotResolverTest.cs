﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;
public class SnapshotResolverTest : LanguageServerTestBase
{
    public SnapshotResolverTest(ITestOutputHelper testOutput) : base(testOutput)
    {
    }

    [Theory, CombinatorialData]
    public void TryResolveDocument_AsksPotentialParentProjectForDocumentItsTracking_ReturnsTrue(bool includeMiscellaneous)
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var snapshotResolver = CreateSnapshotResolver(normalizedFilePath);

        // Act
        var result = snapshotResolver.TryResolveDocument(documentFilePath, includeMiscellaneous, out var document);

        // Assert
        Assert.True(result);
        Assert.Equal(normalizedFilePath, document.FilePath);
    }

    [Fact]
    public void TryResolveDocument_AsksMiscellaneousProjectForDocumentItIsTracking_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var projectSnapshotManagerAccessor = new TestProjectSnapshotManagerAccessor(TestProjectSnapshotManager.Create(ErrorReporter));
        var snapshotResolver = new SnapshotResolver(projectSnapshotManagerAccessor, LoggerFactory);
        var miscProject = snapshotResolver.GetMiscellaneousProject();

        projectSnapshotManagerAccessor.Instance.DocumentAdded(
            new HostProject(miscProject.FilePath, RazorDefaults.Configuration, miscProject.RootNamespace),
            new HostDocument(normalizedFilePath, "document.cshtml"),
            new EmptyTextLoader(normalizedFilePath));

        // Act
        var result = snapshotResolver.TryResolveDocument(documentFilePath, includeMiscellaneous: true, out var document);

        // Assert
        Assert.True(result);
        Assert.NotNull(document);
        Assert.Equal(normalizedFilePath, document.FilePath);
    }

    [Fact]
    public void TryResolveDocument_AsksMiscellaneousProjectForDocumentItIsTracking_InclueMiscFalse_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var projectSnapshotManagerAccessor = new TestProjectSnapshotManagerAccessor(TestProjectSnapshotManager.Create(ErrorReporter));
        var snapshotResolver = new SnapshotResolver(projectSnapshotManagerAccessor, LoggerFactory);
        var miscProject = snapshotResolver.GetMiscellaneousProject();

        projectSnapshotManagerAccessor.Instance.DocumentAdded(
            new HostProject(miscProject.FilePath, RazorDefaults.Configuration, miscProject.RootNamespace),
            new HostDocument(normalizedFilePath, "document.cshtml"),
            new EmptyTextLoader(normalizedFilePath));

        // Act
        var result = snapshotResolver.TryResolveDocument(documentFilePath, includeMiscellaneous: false, out var document);

        // Assert
        Assert.False(result);
        Assert.Null(document);
    }

    [Theory, CombinatorialData]
    public void TryResolveDocument_AsksPotentialParentProjectForDocumentItsNotTrackingAndMiscellaneousProjectIsNotTrackingEither_ReturnsFalse(bool includeMiscellaneous)
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var projectSnapshotManagerAccessor = new TestProjectSnapshotManagerAccessor(TestProjectSnapshotManager.Create(ErrorReporter));
        var snapshotResolver = new SnapshotResolver(projectSnapshotManagerAccessor, LoggerFactory);

        // Act
        var result = snapshotResolver.TryResolveDocument(documentFilePath, includeMiscellaneous, out var document);

        // Assert
        Assert.False(result);
        Assert.Null(document);
    }

    [Theory, CombinatorialData]
    public void TryResolveProject_NoProjects_ReturnsFalse(bool includeMiscellaneous)
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(TestProjectSnapshotManager.Create(ErrorReporter)), LoggerFactory);

        // Act
        var result = snapshotResolver.TryResolveProject(documentFilePath, includeMiscellaneous, out var project);

        // Assert
        Assert.False(result);
        Assert.Null(project);
    }

    [Theory, CombinatorialData]
    public void TryResolveProject_OnlyMiscellaneousProjectDoesNotContainDocument_ReturnsFalse(bool includeMiscellaneous)
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(TestProjectSnapshotManager.Create(ErrorReporter)), LoggerFactory);
        _ = snapshotResolver.GetMiscellaneousProject();

        // Act
        var result = snapshotResolver.TryResolveProject(documentFilePath, includeMiscellaneous, out var project);

        // Assert
        Assert.False(result);
        Assert.Null(project);
    }

    [Fact]
    public void TryResolveProject_OnlyMiscellaneousProjectContainsDocument_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = Path.Join(TempDirectory.Instance.DirectoryPath, "document.cshtml");
        var snapshotResolver = CreateSnapshotResolver(documentFilePath, addToMiscellaneous: true);

        // Act
        var result = snapshotResolver.TryResolveProject(documentFilePath, includeMiscellaneous: true, out var project);

        // Assert
        Assert.True(result);
        Assert.Equal(snapshotResolver.GetMiscellaneousProject(), project);
    }

    [Theory, CombinatorialData]
    public void TryResolveProject_UnrelatedProject_ReturnsFalse(bool includeMiscellaneous)
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var snapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);
        snapshotManager.ProjectAdded(TestProjectSnapshot.Create("C:/other/path/to/project.csproj").HostProject);

        // Act
        var result = snapshotResolver.TryResolveProject(documentFilePath, includeMiscellaneous, out var project);

        // Assert
        Assert.False(result);
        Assert.Null(project);
    }

    [Theory, CombinatorialData]
    public void TryResolveProject_OwnerProjectWithOthers_ReturnsTrue(bool includeMiscellaneous)
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var snapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var expectedProject = snapshotManager.CreateAndAddProject("C:/path/to/project.csproj");
        snapshotManager.CreateAndAddProject("C:/path/to/other/project.csproj");
        snapshotManager.CreateAndAddDocument(expectedProject, documentFilePath);

        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        // Act
        var result = snapshotResolver.TryResolveProject(documentFilePath, includeMiscellaneous, out var project);

        // Assert
        Assert.True(result);
        AssertSnapshotsEqual(expectedProject, project);
    }

    [Fact]
    public void TryResolveProject_MiscellaneousOwnerProjectWithOthers_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = Path.Join(TempDirectory.Instance.DirectoryPath, "file.cshtml");
        documentFilePath = FilePathNormalizer.Normalize(documentFilePath);

        var snapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);
        var miscProject = (ProjectSnapshot)snapshotResolver.GetMiscellaneousProject();
        snapshotManager.CreateAndAddDocument(miscProject, documentFilePath);
        snapshotManager.CreateAndAddProject("C:/path/to/project.csproj");

        // Act
        var result = snapshotResolver.TryResolveProject(documentFilePath, includeMiscellaneous: true, out var project);

        // Assert
        Assert.True(result);
        AssertSnapshotsEqual(miscProject, project);
    }

    [OSSkipConditionFact(new[] { "OSX", "Linux" })]
    public void TryResolveProject_OwnerProjectDifferentCasing_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = "c:/path/to/document.cshtml";
        var snapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        var ownerProject = snapshotManager.CreateAndAddProject("C:/Path/To/project.csproj");
        snapshotManager.CreateAndAddDocument(ownerProject, documentFilePath);

        // Act
        var result = snapshotResolver.TryResolveProject(documentFilePath, includeMiscellaneous: true, out var project);

        // Assert
        Assert.True(result);
        AssertSnapshotsEqual(ownerProject, project);
    }

    [Fact]
    public void GetMiscellaneousProject_ProjectLoaded_ReturnsExistingProject()
    {
        // Arrange
        var snapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        // Act
        var project = snapshotResolver.GetMiscellaneousProject();
        var inManager = snapshotManager.GetLoadedProject(snapshotResolver.MiscellaneousHostProject.FilePath);

        // Assert
        Assert.Same(inManager, project);
    }

    [Fact]
    public void GetMiscellaneousProject_ProjectNotLoaded_CreatesProjectAndReturnsCreatedProject()
    {
        // Arrange
        var snapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        // Act
        var project = snapshotResolver.GetMiscellaneousProject();

        // Assert
        Assert.Single(snapshotManager.GetProjects());
        Assert.Equal(snapshotResolver.MiscellaneousHostProject.FilePath, project.FilePath);
    }

    private SnapshotResolver CreateSnapshotResolver(string filePath, bool addToMiscellaneous = false)
        => CreateSnapshotResolver(filePath, out var _, addToMiscellaneous);

    private SnapshotResolver CreateSnapshotResolver(string filePath, out TestProjectSnapshotManager snapshotManager, bool addToMiscellaneous = false)
    {
        filePath = FilePathNormalizer.Normalize(filePath);

        snapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        if (addToMiscellaneous)
        {
            var miscProject = (ProjectSnapshot)snapshotResolver.GetMiscellaneousProject();
            snapshotManager.CreateAndAddDocument(miscProject, filePath);
        }
        else
        {
            var projectDirectory = FilePathNormalizer.GetDirectory(filePath);
            var projectSnapshot = TestProjectSnapshot.Create(Path.Join(projectDirectory, "proj.csproj"));

            snapshotManager.ProjectAdded(projectSnapshot.HostProject);
            snapshotManager.CreateAndAddDocument(projectSnapshot, filePath);

        }

        return snapshotResolver;
    }

    private void AssertSnapshotsEqual(IProjectSnapshot first, IProjectSnapshot second)
    {
        Assert.Equal(first.FilePath, second.FilePath);
        Assert.Equal(first.CSharpLanguageVersion, second.CSharpLanguageVersion);
        Assert.Equal(first.RootNamespace, second.RootNamespace);
    }
}
