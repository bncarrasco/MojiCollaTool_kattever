using System;
using System.Collections.Generic;
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
    public void MultiplePagesRoundTripPreservesProjectPageIdentityUnicodeAndPageAssets()
    {
        using var scope = TemporaryDirectory.Create();
        var archivePath = Path.Combine(scope.Path, "日本語", "複数ページ.mctzip");
        var source = CreateProject();
        var assets = new TestAssets();
        assets.Set(source.Pages[0].PageId, 1, "png", new byte[] { 1, 2, 3 });
        assets.Set(source.Pages[0].PageId, 2, "jpg", new byte[] { 4, 5, 6, 7 });
        assets.Set(source.Pages[1].PageId, 1, "png", new byte[] { 8, 9 });
        var restoredAssets = new TestAssets();

        DataIO.WriteVersionedProject(archivePath, source, assets);
        var restored = DataIO.ReadVersionedProject(archivePath, restoredAssets);

        Assert.AreEqual(source.ProjectId, restored.ProjectId);
        Assert.AreEqual(source.Name, restored.Name);
        Assert.AreEqual(2, restored.PageCount);
        Assert.AreEqual(source.Pages[0].PageId, restored.Pages[0].PageId);
        Assert.AreEqual("縦書きページ", restored.Pages[0].Name);
        Assert.AreEqual(TextDirection.Tategaki, restored.Pages[0].MojiDatas[0].TextDirection);
        Assert.AreEqual("二枚目のページ", restored.Pages[1].Name);
        Assert.AreEqual("日本語の本文", restored.Pages[1].MojiDatas[0].FullText);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, restoredAssets.Get(source.Pages[0].PageId, 1));
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6, 7 }, restoredAssets.Get(source.Pages[0].PageId, 2));
        CollectionAssert.AreEqual(new byte[] { 8, 9 }, restoredAssets.Get(source.Pages[1].PageId, 1));

        using var archive = ZipFile.OpenRead(archivePath);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "manifest.xml",
                $"pages/{source.Pages[0].PageId:D}/page.xml",
                $"pages/{source.Pages[0].PageId:D}/image1.png",
                $"pages/{source.Pages[0].PageId:D}/image2.jpg",
                $"pages/{source.Pages[1].PageId:D}/page.xml",
                $"pages/{source.Pages[1].PageId:D}/image1.png",
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
    public void PageWithNoAssetsDoesNotCreateStaleImageEntries()
    {
        using var scope = TemporaryDirectory.Create();
        var archivePath = Path.Combine(scope.Path, "project.mctzip");
        var source = CreateProject();
        var assets = new TestAssets();
        assets.Set(source.Pages[0].PageId, 1, "png", new byte[] { 1 });

        DataIO.WriteVersionedProject(archivePath, source, assets);
        DataIO.WriteVersionedProject(archivePath, source);

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.IsFalse(archive.Entries.Any(entry => entry.FullName.Contains("/image", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void NonContiguousManifestOrderIsRejected()
    {
        using var scope = TemporaryDirectory.Create();
        var sourcePath = Path.Combine(scope.Path, "valid.mctzip");
        var invalidPath = Path.Combine(scope.Path, "invalid-order.mctzip");
        DataIO.WriteVersionedProject(sourcePath, CreateProject());
        RewriteEntry(sourcePath, invalidPath, "manifest.xml", null, manifest =>
        {
            manifest.Root!.Element("Pages")!.Elements("Page").Last().Element("Order")!.Value = "2";
            return manifest;
        });

        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(invalidPath));
    }

    [TestMethod]
    public void PageNameMismatchIsRejected()
    {
        using var scope = TemporaryDirectory.Create();
        var sourcePath = Path.Combine(scope.Path, "valid.mctzip");
        var invalidPath = Path.Combine(scope.Path, "invalid-name.mctzip");
        var source = CreateProject();
        DataIO.WriteVersionedProject(sourcePath, source);
        RewriteEntry(sourcePath, invalidPath, $"pages/{source.Pages[0].PageId:D}/page.xml", null, page =>
        {
            page.Root!.Element("Name")!.Value = "別名";
            return page;
        });

        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(invalidPath));
    }

    [TestMethod]
    public void DtdAndFutureMinimumReaderVersionAreRejected()
    {
        using var scope = TemporaryDirectory.Create();
        var sourcePath = Path.Combine(scope.Path, "valid.mctzip");
        var dtdPath = Path.Combine(scope.Path, "dtd.mctzip");
        var futurePath = Path.Combine(scope.Path, "future-reader.mctzip");
        DataIO.WriteVersionedProject(sourcePath, CreateProject());

        RewriteEntry(sourcePath, dtdPath, "manifest.xml", "<!DOCTYPE Manifest [<!ENTITY xxe SYSTEM 'file:///secret'>]><Manifest><FormatVersion>2.0</FormatVersion><MinimumReaderVersion>2.0</MinimumReaderVersion><ProjectId>aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa</ProjectId><ProjectName>&xxe;</ProjectName></Manifest>", null);
        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(dtdPath));

        RewriteEntry(sourcePath, futurePath, "manifest.xml", null, manifest =>
        {
            manifest.Root!.Element("MinimumReaderVersion")!.Value = "99.0";
            return manifest;
        });
        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(futurePath));
    }

    [TestMethod]
    public void AssetSourceFailureLeavesOriginalArchiveUntouched()
    {
        using var scope = TemporaryDirectory.Create();
        var archivePath = Path.Combine(scope.Path, "project.mctzip");
        var source = CreateProject();
        DataIO.WriteVersionedProject(archivePath, source);
        var original = File.ReadAllBytes(archivePath);

        Assert.ThrowsException<InvalidOperationException>(() => DataIO.WriteVersionedProject(archivePath, source, new ThrowingAssets()));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(archivePath));
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

    private static void RewriteEntry(string sourcePath, string destinationPath, string entryName, string? rawContent, Func<XDocument, XDocument>? transform)
    {
        using var source = ZipFile.OpenRead(sourcePath);
        using var destination = ZipFile.Open(destinationPath, ZipArchiveMode.Create);
        foreach (var sourceEntry in source.Entries)
        {
            var destinationEntry = destination.CreateEntry(sourceEntry.FullName);
            using var input = sourceEntry.Open();
            using var output = destinationEntry.Open();
            if (!string.Equals(sourceEntry.FullName, entryName, StringComparison.Ordinal))
            {
                input.CopyTo(output);
                continue;
            }

            byte[] bytes;
            if (rawContent != null)
            {
                bytes = Encoding.UTF8.GetBytes(rawContent);
            }
            else
            {
                var document = XDocument.Load(input);
                bytes = Encoding.UTF8.GetBytes(transform!(document).ToString(SaveOptions.DisableFormatting));
            }

            output.Write(bytes, 0, bytes.Length);
        }
    }

    private sealed class TestAssets : IProjectAssetSource, IProjectAssetSink
    {
        private readonly Dictionary<string, (string Extension, byte[] Content)> _assets = new();

        public int Count => _assets.Count;

        public void Set(Guid pageId, int imageNumber, string extension, byte[] content)
        {
            _assets[Key(pageId, imageNumber)] = (extension, content);
        }

        public ProjectImageAsset? OpenImage(PageDocument page, int imageNumber)
        {
            if (!_assets.TryGetValue(Key(page.PageId, imageNumber), out var asset)) return null;
            return new ProjectImageAsset(asset.Extension, new MemoryStream(asset.Content, writable: false));
        }

        public void SaveImage(Guid pageId, int imageNumber, string extension, Stream content)
        {
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            Set(pageId, imageNumber, extension, copy.ToArray());
        }

        public byte[] Get(Guid pageId, int imageNumber) => _assets[Key(pageId, imageNumber)].Content;

        private static string Key(Guid pageId, int imageNumber) => $"{pageId:D}:{imageNumber}";
    }

    private sealed class ThrowingAssets : IProjectAssetSource
    {
        public ProjectImageAsset? OpenImage(PageDocument page, int imageNumber)
        {
            throw new IOException("asset source failure");
        }
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
