using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class VersionedProjectPersistenceTests
{
    [TestMethod]
    public void MultiplePagesRoundTripPreservesProjectPageIdentityAndUnicode()
    {
        using var scope = TemporaryDirectory.Create();
        var archivePath = Path.Combine(scope.Path, "日本語", "複数ページ.mctzip");
        var source = CreateProject();

        DataIO.WriteVersionedProject(archivePath, source);
        var restored = DataIO.ReadVersionedProject(archivePath);

        Assert.AreEqual(source.ProjectId, restored.ProjectId);
        Assert.AreEqual(source.Name, restored.Name);
        Assert.AreEqual(2, restored.PageCount);
        Assert.AreEqual(source.Pages[0].PageId, restored.Pages[0].PageId);
        Assert.AreEqual("縦書きページ", restored.Pages[0].Name);
        Assert.AreEqual(TextDirection.Tategaki, restored.Pages[0].MojiDatas[0].TextDirection);
        Assert.AreEqual("二枚目のページ", restored.Pages[1].Name);
        Assert.AreEqual("日本語の本文", restored.Pages[1].MojiDatas[0].FullText);

        using var archive = ZipFile.OpenRead(archivePath);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "manifest.xml",
                $"pages/{source.Pages[0].PageId:D}/page.xml",
                $"pages/{source.Pages[1].PageId:D}/page.xml",
            },
            archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    [TestMethod]
    public void UnknownMajorVersionIsRejectedWithoutReadingPages()
    {
        using var scope = TemporaryDirectory.Create();
        var validPath = Path.Combine(scope.Path, "valid.mctzip");
        var futurePath = Path.Combine(scope.Path, "future.mctzip");
        DataIO.WriteVersionedProject(validPath, CreateProject());

        using (var source = ZipFile.OpenRead(validPath))
        using (var future = ZipFile.Open(futurePath, ZipArchiveMode.Create))
        {
            foreach (var sourceEntry in source.Entries)
            {
                var destination = future.CreateEntry(sourceEntry.FullName);
                using var input = sourceEntry.Open();
                using var output = destination.Open();
                if (sourceEntry.FullName == "manifest.xml")
                {
                    var manifest = XDocument.Load(input);
                    manifest.Root!.Element("FormatVersion")!.Value = "9.0";
                    var bytes = Encoding.UTF8.GetBytes(manifest.ToString(SaveOptions.DisableFormatting));
                    output.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    input.CopyTo(output);
                }
            }
        }

        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(futurePath));
    }

    [TestMethod]
    public void TraversalEntryIsRejected()
    {
        using var scope = TemporaryDirectory.Create();
        var archivePath = Path.Combine(scope.Path, "unsafe.mctzip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            using var stream = archive.CreateEntry("../outside.txt").Open();
            stream.WriteByte(1);
        }

        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(archivePath));
    }

    [TestMethod]
    public void SuccessfulReplacementCanKeepOneBackup()
    {
        using var scope = TemporaryDirectory.Create();
        var archivePath = Path.Combine(scope.Path, "project.mctzip");
        var backupPath = archivePath + ".backup";
        var source = CreateProject();

        DataIO.WriteVersionedProject(archivePath, source);
        var firstBytes = File.ReadAllBytes(archivePath);
        source.Rename("更新後のプロジェクト");
        DataIO.WriteVersionedProject(archivePath, source, createBackup: true);

        Assert.IsTrue(File.Exists(backupPath));
        CollectionAssert.AreEqual(firstBytes, File.ReadAllBytes(backupPath));
        Assert.AreEqual("更新後のプロジェクト", DataIO.ReadVersionedProject(archivePath).Name);
    }

    private static ProjectDocument CreateProject()
    {
        var firstPage = new PageDocument(Guid.Parse("11111111-1111-1111-1111-111111111111"), "縦書きページ", new CanvasData
        {
            CanvasWidth = 640,
            CanvasHeight = 480,
        }, new[]
        {
            new MojiData(1)
            {
                FullText = "縦書きの本文\n日本語",
                TextDirection = TextDirection.Tategaki,
                X = 12.5,
            },
        });
        var secondPage = new PageDocument(Guid.Parse("22222222-2222-2222-2222-222222222222"), "二枚目のページ", new CanvasData(), new[]
        {
            new MojiData(2) { FullText = "日本語の本文" },
        });
        return new ProjectDocument(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "保存テスト", new[] { firstPage, secondPage });
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

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
}
