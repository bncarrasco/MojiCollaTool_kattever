using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Media.Imaging;

namespace MojiCollaTool.Tests;

[TestClass]
public class LegacyPersistenceTests
{
    [TestMethod]
    public void LegacyWriterUsesFreshWorkspaceAndReplacesExistingArchive()
    {
        using var scope = TemporaryDirectory.Create();
        var projectPath = Path.Combine(scope.Path, "保存先.mctzip");

        using (var archive = ZipFile.Open(projectPath, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry("stale.txt").Open(), Encoding.UTF8);
            writer.Write("stale");
        }

        var canvas = new CanvasData { CanvasWidth = 800, CanvasHeight = 600 };
        var moji = new MojiData(1) { FullText = "保存" };
        DataIO.WriteWorkingDirToProjectDataFile(projectPath, new[] { moji }, canvas);

        using var result = DataIO.ReadProjectData(projectPath);
        Assert.AreEqual(800, result.CanvasData.CanvasWidth);
        Assert.AreEqual("保存", result.MojiDatas.Single().FullText);

        using var archiveAfterSave = ZipFile.OpenRead(projectPath);
        CollectionAssert.DoesNotContain(archiveAfterSave.Entries.Select(entry => entry.FullName).ToArray(), "stale.txt");
        CollectionAssert.AreEquivalent(
            new[] { "Info.txt", "CanvasData.xml", "MojiData1.xml" },
            archiveAfterSave.Entries.Select(entry => entry.FullName).ToArray());
    }

    [TestMethod]
    public void ImageBackedProjectSaveAndLoadCopiesCurrentImages()
    {
        using var scope = TemporaryDirectory.Create();
        var sourceWorkingPath = Path.Combine(scope.Path, "SourceWorking");
        var loadedWorkingPath = Path.Combine(scope.Path, "LoadedWorking");
        var projectPath = Path.Combine(scope.Path, "images.mctzip");
        Directory.CreateDirectory(sourceWorkingPath);
        CopyFixtureImages(sourceWorkingPath);

        var canvas = DataIO.ReadCanvasData(FixtureRootPath());
        DataIO.WriteWorkingDirToProjectDataFile(
            projectPath,
            new[] { new MojiData(1) { FullText = "画像付き" } },
            canvas,
            sourceWorkingPath);

        using (var archive = ZipFile.OpenRead(projectPath))
        {
            CollectionAssert.IsSubsetOf(
                new[] { "Image1.png", "Image2.png" },
                archive.Entries.Select(entry => entry.FullName).ToArray());
        }

        DataIO.ReadProjectDataToWorkingDir(projectPath, loadedWorkingPath);
        AssertImageDimensions(Path.Combine(loadedWorkingPath, "Image1.png"), 640, 480);
        AssertImageDimensions(Path.Combine(loadedWorkingPath, "Image2.png"), 320, 240);
        Assert.AreEqual(2, Directory.GetFiles(loadedWorkingPath, "Image*.*").Length);
    }

    [TestMethod]
    public void DeletedImageDoesNotLeaveStaleImageEntryInSavedArchive()
    {
        using var scope = TemporaryDirectory.Create();
        var sourceWorkingPath = Path.Combine(scope.Path, "SourceWorking");
        var loadedWorkingPath = Path.Combine(scope.Path, "LoadedWorking");
        var projectPath = Path.Combine(scope.Path, "image-deleted.mctzip");
        Directory.CreateDirectory(sourceWorkingPath);
        CopyFixtureImages(sourceWorkingPath);

        var canvas = DataIO.ReadCanvasData(FixtureRootPath());
        canvas.ImageData1.Init();
        DataIO.WriteWorkingDirToProjectDataFile(projectPath, Array.Empty<MojiData>(), canvas, sourceWorkingPath);

        using (var archive = ZipFile.OpenRead(projectPath))
        {
            var entries = archive.Entries.Select(entry => entry.FullName).ToArray();
            CollectionAssert.DoesNotContain(entries, "Image1.png");
            CollectionAssert.Contains(entries, "Image2.png");
        }

        DataIO.ReadProjectDataToWorkingDir(projectPath, loadedWorkingPath);
        Assert.IsFalse(File.Exists(Path.Combine(loadedWorkingPath, "Image1.png")));
        Assert.IsTrue(File.Exists(Path.Combine(loadedWorkingPath, "Image2.png")));
    }

    [TestMethod]
    public void BrokenImageDoesNotReplaceExistingWorkingDirectory()
    {
        using var scope = TemporaryDirectory.Create();
        var sourceWorkingPath = Path.Combine(scope.Path, "SourceWorking");
        var existingWorkingPath = Path.Combine(scope.Path, "ExistingWorking");
        var validProjectPath = Path.Combine(scope.Path, "valid.mctzip");
        var brokenProjectPath = Path.Combine(scope.Path, "broken-image.mctzip");
        Directory.CreateDirectory(sourceWorkingPath);
        Directory.CreateDirectory(existingWorkingPath);
        CopyFixtureImages(sourceWorkingPath);
        File.WriteAllText(Path.Combine(existingWorkingPath, "keep.txt"), "current session");

        var canvas = DataIO.ReadCanvasData(FixtureRootPath());
        DataIO.WriteWorkingDirToProjectDataFile(validProjectPath, Array.Empty<MojiData>(), canvas, sourceWorkingPath);
        CreateArchiveWithCorruptImage(validProjectPath, brokenProjectPath);

        Assert.ThrowsException<InvalidOperationException>(() =>
            DataIO.ReadProjectDataToWorkingDir(brokenProjectPath, existingWorkingPath));

        Assert.AreEqual("current session", File.ReadAllText(Path.Combine(existingWorkingPath, "keep.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(existingWorkingPath, "CanvasData.xml")));
    }

    [TestMethod]
    public void MultipleImageExtensionsForOneNumberAreRejected()
    {
        using var scope = TemporaryDirectory.Create();
        var sourceWorkingPath = Path.Combine(scope.Path, "SourceWorking");
        var existingWorkingPath = Path.Combine(scope.Path, "ExistingWorking");
        var validProjectPath = Path.Combine(scope.Path, "valid.mctzip");
        var duplicateProjectPath = Path.Combine(scope.Path, "duplicate-image.mctzip");
        Directory.CreateDirectory(sourceWorkingPath);
        Directory.CreateDirectory(existingWorkingPath);
        CopyFixtureImages(sourceWorkingPath);
        File.WriteAllText(Path.Combine(existingWorkingPath, "keep.txt"), "current session");

        var canvas = DataIO.ReadCanvasData(FixtureRootPath());
        DataIO.WriteWorkingDirToProjectDataFile(validProjectPath, Array.Empty<MojiData>(), canvas, sourceWorkingPath);
        CreateArchiveWithDuplicateImageExtension(validProjectPath, duplicateProjectPath);

        Assert.ThrowsException<InvalidOperationException>(() =>
            DataIO.ReadProjectDataToWorkingDir(duplicateProjectPath, existingWorkingPath));

        Assert.AreEqual("current session", File.ReadAllText(Path.Combine(existingWorkingPath, "keep.txt")));
    }

    [TestMethod]
    public void CorruptArchiveDoesNotModifyExistingWorkingDirectory()
    {
        using var scope = TemporaryDirectory.Create();
        var projectPath = Path.Combine(scope.Path, "broken.mctzip");
        var workingPath = Path.Combine(scope.Path, "Working");
        Directory.CreateDirectory(workingPath);
        File.WriteAllText(Path.Combine(workingPath, "keep.txt"), "current session");
        File.WriteAllBytes(projectPath, new byte[] { 0x01, 0x02, 0x03, 0x04 });

        Assert.ThrowsException<InvalidOperationException>(() =>
            DataIO.ReadProjectDataToWorkingDir(projectPath, workingPath));

        Assert.IsTrue(File.Exists(Path.Combine(workingPath, "keep.txt")));
        Assert.AreEqual("current session", File.ReadAllText(Path.Combine(workingPath, "keep.txt")));
    }

    [TestMethod]
    public void SaveFailureLeavesExistingArchiveUnchanged()
    {
        using var scope = TemporaryDirectory.Create();
        var projectPath = Path.Combine(scope.Path, "existing.mctzip");
        DataIO.WriteWorkingDirToProjectDataFile(
            projectPath,
            new[] { new MojiData(1) { FullText = "old" } },
            new CanvasData { CanvasWidth = 10, CanvasHeight = 20 });
        var originalBytes = File.ReadAllBytes(projectPath);

        Assert.ThrowsException<InvalidOperationException>(() =>
            DataIO.WriteWorkingDirToProjectDataFile(projectPath, ThrowAfterFirstMoji(), new CanvasData()));

        Assert.IsTrue(originalBytes.SequenceEqual(File.ReadAllBytes(projectPath)));
    }

    [TestMethod]
    public void ValidArchiveCommitsOnlyAfterValidationAndRemovesStaleWorkingFiles()
    {
        using var scope = TemporaryDirectory.Create();
        var projectPath = Path.Combine(scope.Path, "valid.mctzip");
        var workingPath = Path.Combine(scope.Path, "Working");
        Directory.CreateDirectory(workingPath);
        File.WriteAllText(Path.Combine(workingPath, "stale.xml"), "old");

        DataIO.WriteWorkingDirToProjectDataFile(
            projectPath,
            new[] { new MojiData(4) { FullText = "新しい文字" } },
            new CanvasData { CanvasWidth = 123, CanvasHeight = 456 });

        DataIO.ReadProjectDataToWorkingDir(projectPath, workingPath);

        Assert.IsFalse(File.Exists(Path.Combine(workingPath, "stale.xml")));
        Assert.IsTrue(File.Exists(Path.Combine(workingPath, "CanvasData.xml")));
        Assert.AreEqual("新しい文字", DataIO.ReadMojiDatas(workingPath).Single().FullText);
    }

    [TestMethod]
    public void ArchiveTraversalIsRejectedWithoutModifyingExistingWorkingDirectory()
    {
        using var scope = TemporaryDirectory.Create();
        var projectPath = Path.Combine(scope.Path, "unsafe.mctzip");
        var workingPath = Path.Combine(scope.Path, "Working");
        Directory.CreateDirectory(workingPath);
        File.WriteAllText(Path.Combine(workingPath, "keep.txt"), "current session");

        using (var archive = ZipFile.Open(projectPath, ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open(), Encoding.UTF8);
            writer.Write("must not escape");
        }

        Assert.ThrowsException<InvalidOperationException>(() =>
            DataIO.ReadProjectDataToWorkingDir(projectPath, workingPath));

        Assert.IsTrue(File.Exists(Path.Combine(workingPath, "keep.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(scope.Path, "escape.txt")));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MojiCollaTool.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }

    private static IEnumerable<MojiData> ThrowAfterFirstMoji()
    {
        yield return new MojiData(9) { FullText = "temporary" };
        throw new IOException("Injected save failure");
    }

    private static string FixtureRootPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "current-mctzip-root");
    }

    private static void CopyFixtureImages(string destinationDirectoryPath)
    {
        File.Copy(Path.Combine(FixtureRootPath(), "Image1.png"), Path.Combine(destinationDirectoryPath, "Image1.png"));
        File.Copy(Path.Combine(FixtureRootPath(), "Image2.png"), Path.Combine(destinationDirectoryPath, "Image2.png"));
    }

    private static void AssertImageDimensions(string path, int width, int height)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.AreEqual(width, decoder.Frames[0].PixelWidth);
        Assert.AreEqual(height, decoder.Frames[0].PixelHeight);
    }

    private static void CreateArchiveWithCorruptImage(string sourcePath, string destinationPath)
    {
        using var source = ZipFile.OpenRead(sourcePath);
        using var destination = ZipFile.Open(destinationPath, ZipArchiveMode.Create);
        foreach (var entry in source.Entries)
        {
            var output = destination.CreateEntry(entry.FullName);
            using var outputStream = output.Open();
            if (entry.FullName.Equals("Image1.png", StringComparison.OrdinalIgnoreCase))
            {
                outputStream.Write(new byte[] { 0x00, 0x01, 0x02, 0x03 });
                continue;
            }

            using var inputStream = entry.Open();
            inputStream.CopyTo(outputStream);
        }
    }

    private static void CreateArchiveWithDuplicateImageExtension(string sourcePath, string destinationPath)
    {
        using var source = ZipFile.OpenRead(sourcePath);
        using var destination = ZipFile.Open(destinationPath, ZipArchiveMode.Create);
        foreach (var entry in source.Entries)
        {
            var output = destination.CreateEntry(entry.FullName);
            using var outputStream = output.Open();
            using var inputStream = entry.Open();
            inputStream.CopyTo(outputStream);
        }

        var originalImage = source.GetEntry("Image1.png");
        Assert.IsNotNull(originalImage);
        var duplicate = destination.CreateEntry("Image1.jpg");
        using var duplicateStream = duplicate.Open();
        using var originalStream = originalImage!.Open();
        originalStream.CopyTo(duplicateStream);
    }
}
