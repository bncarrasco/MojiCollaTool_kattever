using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace MojiCollaTool.Tests;

[TestClass]
public class LegacyProjectReaderTests
{
    [TestMethod]
    public void LegacyArchiveWithNoBackgroundImagesLoadsWithoutAssetSink()
    {
        using var scope = new TemporaryDirectory();
        var path = CreateLegacyArchive(scope, imageCount: 0);

        var result = DataIO.ReadProject(path);

        Assert.AreEqual(ProjectFormatKind.Legacy, result.Format);
        Assert.AreEqual(1, result.Project.PageCount);
        Assert.IsTrue(result.Project.Pages[0].CanvasData.ImageData1.IsNullData());
        Assert.IsTrue(result.Project.Pages[0].CanvasData.ImageData2.IsNullData());
    }

    [TestMethod]
    public void LegacyArchiveWithOneBackgroundImageUsesNonBatchSink()
    {
        using var scope = new TemporaryDirectory();
        var path = CreateLegacyArchive(scope, imageCount: 1);
        var sink = new RecordingAssets();

        var result = DataIO.ReadProject(path, sink);

        Assert.AreEqual(ProjectFormatKind.Legacy, result.Format);
        Assert.AreEqual(1, sink.Restored.Count);
        Assert.AreEqual(1, sink.Restored[0].ImageNumber);
    }

    [TestMethod]
    public void LegacyArchiveWithTwoBackgroundImagesUsesBatchSink()
    {
        using var scope = new TemporaryDirectory();
        var path = CreateLegacyArchive(scope, imageCount: 2);
        var sink = new RecordingAssets();

        var result = DataIO.ReadProject(path, sink);

        Assert.AreEqual(ProjectFormatKind.Legacy, result.Format);
        Assert.AreEqual(2, sink.Restored.Count);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, sink.Restored.Select(asset => asset.ImageNumber).ToArray());
        Assert.AreEqual(result.Project.Pages[0].PageId, sink.Restored[0].PageId);
    }

    [TestMethod]
    public void ThreeMojiDatasPreserveConcreteDirectionsAndUnicodeValues()
    {
        using var scope = new TemporaryDirectory();
        var path = CreateLegacyArchive(scope, imageCount: 0);

        var page = DataIO.ReadProject(path).Project.Pages[0];
        var moji = page.MojiDatas.OrderBy(data => data.Id).ToArray();

        Assert.AreEqual(3, moji.Length);
        Assert.AreEqual("横", moji[0].FullText);
        Assert.AreEqual("Yokogaki", moji[0].TextDirection.ToString());
        Assert.AreEqual("縦", moji[1].FullText);
        Assert.AreEqual("Tategaki", moji[1].TextDirection.ToString());
        Assert.AreEqual("日本語 😀 が", moji[2].FullText);
        Assert.AreEqual("Yokogaki", moji[2].TextDirection.ToString());
    }

    [TestMethod]
    public void UnknownLegacyXmlFieldsAreIgnored()
    {
        using var scope = new TemporaryDirectory();
        var path = CreateLegacyArchive(scope, imageCount: 0, addUnknownFields: true);

        var result = DataIO.ReadProject(path);

        Assert.AreEqual(3, result.Project.Pages[0].MojiDatas.Count);
        Assert.AreEqual(DataIO.ReadCanvasData(FixtureRootPath()).CanvasWidth, result.Project.Pages[0].CanvasData.CanvasWidth);
    }

    [TestMethod]
    public void BrokenManifestNeverFallsBackToLegacyRootEntries()
    {
        using var scope = new TemporaryDirectory();
        var path = CreateLegacyArchive(scope, imageCount: 0, includeBrokenManifest: true);

        Assert.AreEqual(ProjectFormatKind.Versioned, ProjectFormatDetector.Detect(path));
        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadProject(path));
    }

    [TestMethod]
    public void EntryLimitIsRejectedBeforeFormatNameMaterialization()
    {
        using var scope = new TemporaryDirectory();
        var path = Path.Combine(scope.Path, "too-many-entries.mctzip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            for (var index = 0; index <= ProjectFormatDetector.MaxArchiveEntries; index++)
            {
                archive.CreateEntry($"entry-{index}.txt");
            }
        }

        var exception = Assert.ThrowsException<InvalidDataException>(() => ProjectFormatDetector.Detect(path));
        StringAssert.Contains(exception.Message, "too many entries");
    }

    [TestMethod]
    public void JapanesePathLegacyArchivePreservesProjectNameAndUnicodeObjects()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "日本語フォルダー", "現行プロジェクト.mctzip");

        var result = DataIO.ReadProject(path, new RecordingAssets());

        Assert.AreEqual(ProjectFormatKind.Legacy, result.Format);
        Assert.AreEqual("現行プロジェクト", result.Project.Name);
        Assert.IsTrue(result.Project.Pages[0].MojiDatas.Any(data => data.FullText.Contains("日本語", StringComparison.Ordinal)));
    }

    private static string CreateLegacyArchive(
        TemporaryDirectory scope,
        int imageCount,
        bool addUnknownFields = false,
        bool includeBrokenManifest = false)
    {
        var fixtureRoot = FixtureRootPath();
        var sourceRoot = Path.Combine(scope.Path, "legacy-root");
        Directory.CreateDirectory(sourceRoot);
        File.Copy(Path.Combine(fixtureRoot, "Info.txt"), Path.Combine(sourceRoot, "Info.txt"));

        var canvas = DataIO.ReadCanvasData(fixtureRoot);
        if (imageCount < 1) canvas.ImageData1.Init();
        if (imageCount < 2) canvas.ImageData2.Init();
        DataIO.WriteCanvasData(canvas, sourceRoot);

        foreach (var mojiData in DataIO.ReadMojiDatas(fixtureRoot))
        {
            DataIO.WriteMojiData(mojiData, sourceRoot);
        }

        if (imageCount >= 1) File.Copy(Path.Combine(fixtureRoot, "Image1.png"), Path.Combine(sourceRoot, "Image1.png"));
        if (imageCount >= 2) File.Copy(Path.Combine(fixtureRoot, "Image2.png"), Path.Combine(sourceRoot, "Image2.png"));

        if (addUnknownFields)
        {
            var canvasPath = Path.Combine(sourceRoot, "CanvasData.xml");
            File.WriteAllText(canvasPath, File.ReadAllText(canvasPath).Replace("</CanvasData>", "<UnknownCanvasField>ignored</UnknownCanvasField></CanvasData>"), new UTF8Encoding(false));
            var mojiPath = Path.Combine(sourceRoot, "MojiData1.xml");
            File.WriteAllText(mojiPath, File.ReadAllText(mojiPath).Replace("</MojiData>", "<UnknownMojiField>ignored</UnknownMojiField></MojiData>"), new UTF8Encoding(false));
        }

        if (includeBrokenManifest)
        {
            File.WriteAllText(Path.Combine(sourceRoot, "manifest.xml"), "<Manifest><FormatVersion>broken</FormatVersion>", new UTF8Encoding(false));
        }

        var archivePath = Path.Combine(scope.Path, "legacy.mctzip");
        ZipFile.CreateFromDirectory(sourceRoot, archivePath);
        return archivePath;
    }

    private static string FixtureRootPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "current-mctzip-root");
    }

    private sealed class RecordingAssets : IProjectAssetSink, IProjectAssetBatchSink
    {
        public List<RestoredAsset> Restored { get; } = new List<RestoredAsset>();

        public void SaveImage(Guid pageId, int imageNumber, string extension, Stream content)
        {
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            Restored.Add(new RestoredAsset(pageId, imageNumber, extension, copy.ToArray()));
        }

        public void SaveImages(ProjectDocument project, IReadOnlyList<ProjectAssetRestore> assets)
        {
            Restored.AddRange(assets.Select(asset =>
                new RestoredAsset(asset.PageId, asset.ImageNumber, asset.Extension, asset.Content)));
        }
    }

    private sealed class RestoredAsset
    {
        public RestoredAsset(Guid pageId, int imageNumber, string extension, byte[] content)
        {
            PageId = pageId;
            ImageNumber = imageNumber;
            Extension = extension;
            Content = content;
        }

        public Guid PageId { get; }

        public int ImageNumber { get; }

        public string Extension { get; }

        public byte[] Content { get; }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mct041-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
