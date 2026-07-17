using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}
