using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class XmlRoundTripTests
{
    [TestMethod]
    public void CanvasDataXmlRoundTripPreservesCurrentFields()
    {
        var source = new CanvasData
        {
            CanvasWidth = 1280,
            CanvasHeight = 720,
            ImageData1 = new ImageData(800, 600) { ModifiedWidth = 640, ModifiedHeight = 480 },
            ImageData2 = new ImageData(400, 300) { ModifiedWidth = 320, ModifiedHeight = 240 },
            Image2LocatePosition = LocatePosition.Bottom,
            ImageMarginTop = 11,
            ImageMarginLeft = 12,
            ImageMarginBottom = 13,
            ImageMarginRight = 14,
            CanvasColor = Color.FromArgb(255, 12, 34, 56),
        };

        using var scope = TemporaryDirectory.Create();
        var path = Path.Combine(scope.Path, "CanvasData.xml");
        DataIO.WriteXMLData(source, path);

        AssertUtf8WithoutBom(path);
        var restored = DataIO.ReadXMLData<CanvasData>(path);

        Assert.AreEqual(source.CanvasWidth, restored.CanvasWidth);
        Assert.AreEqual(source.CanvasHeight, restored.CanvasHeight);
        Assert.AreEqual(source.Image2LocatePosition, restored.Image2LocatePosition);
        Assert.AreEqual(source.ImageData1.OriginalWidth, restored.ImageData1.OriginalWidth);
        Assert.AreEqual(source.ImageData1.ModifiedHeight, restored.ImageData1.ModifiedHeight);
        Assert.AreEqual(source.ImageData2.ModifiedWidth, restored.ImageData2.ModifiedWidth);
        Assert.AreEqual(source.ImageMarginBottom, restored.ImageMarginBottom);
        Assert.AreEqual(source.CanvasColor, restored.CanvasColor);
    }

    [TestMethod]
    public void ImageDataXmlRoundTripPreservesOriginalAndModifiedSize()
    {
        var source = new ImageData(1920, 1080)
        {
            ModifiedWidth = 960,
            ModifiedHeight = 540,
        };

        using var scope = TemporaryDirectory.Create();
        var path = Path.Combine(scope.Path, "ImageData.xml");
        DataIO.WriteXMLData(source, path);

        AssertUtf8WithoutBom(path);
        var restored = DataIO.ReadXMLData<ImageData>(path);

        Assert.AreEqual(source.OriginalWidth, restored.OriginalWidth);
        Assert.AreEqual(source.OriginalHeight, restored.OriginalHeight);
        Assert.AreEqual(source.ModifiedWidth, restored.ModifiedWidth);
        Assert.AreEqual(source.ModifiedHeight, restored.ModifiedHeight);
    }

    [TestMethod]
    public void MojiDataXmlRoundTripPreservesJapaneseDirectionsAndUnicodeText()
    {
        var source = new MojiData(7)
        {
            FullText = "横書き ASCII\r\n縦書き\n日本語 😀👩‍💻 か\u3099",
            X = 12.5,
            Y = 34.75,
            FontSize = 72,
            FontFamilyName = "ＭＳ ゴシック",
            TextDirection = TextDirection.Tategaki,
            IsBold = true,
            IsItalic = true,
            CharacterMargin = 1.5,
            LineMargin = 2.5,
            ForeColor = Color.FromArgb(255, 1, 2, 3),
            BorderColor = Colors.White,
            BorderThickness = 4,
            BorderBlurrRadius = 0,
            SecondBorderColor = Colors.Black,
            SecondBorderThickness = 2,
            SecondBorderBlurrRadius = 0,
            IsBackgroundBoxExists = true,
            BackgroundBoxColor = Colors.Yellow,
            BackgroundBoxPadding = 6,
            BackgroundBoxBorderThickness = 1,
            BackgroundBoxBorderColor = Colors.Blue,
            BackgroundBoxCornerRadius = 8,
            RotateAngle = 15,
        };

        using var scope = TemporaryDirectory.Create();
        var path = Path.Combine(scope.Path, "MojiData7.xml");
        DataIO.WriteMojiData(source, scope.Path);

        AssertUtf8WithoutBom(path);
        var restored = DataIO.ReadMojiData(path);

        Assert.AreEqual(source.Id, restored.Id);
        Assert.AreEqual(NormalizeNewLines(source.FullText), restored.FullText);
        Assert.AreEqual(source.X, restored.X);
        Assert.AreEqual(source.Y, restored.Y);
        Assert.AreEqual(source.FontFamilyName, restored.FontFamilyName);
        Assert.AreEqual(source.TextDirection, restored.TextDirection);
        Assert.AreEqual(source.IsBold, restored.IsBold);
        Assert.AreEqual(source.IsItalic, restored.IsItalic);
        Assert.AreEqual(source.ForeColor, restored.ForeColor);
        Assert.AreEqual(source.BorderThickness, restored.BorderThickness);
        Assert.AreEqual(source.SecondBorderThickness, restored.SecondBorderThickness);
        Assert.AreEqual(source.BackgroundBoxColor, restored.BackgroundBoxColor);
        Assert.AreEqual(source.RotateAngle, restored.RotateAngle);
    }

    [TestMethod]
    public void CurrentMctzipRootFixtureContainsExpectedEntries()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "current-mctzip-root");
        var expectedEntries = FixtureEntries;

        foreach (var entry in expectedEntries)
        {
            Assert.IsTrue(File.Exists(Path.Combine(fixturePath, entry)), $"Missing fixture entry: {entry}");
        }

        var canvas = DataIO.ReadCanvasData(fixturePath);
        var mojiDatas = DataIO.ReadMojiDatas(fixturePath).OrderBy(data => data.Id).ToArray();

        Assert.AreEqual(980, canvas.CanvasWidth);
        Assert.AreEqual(500, canvas.CanvasHeight);
        Assert.AreEqual(LocatePosition.Right, canvas.Image2LocatePosition);
        Assert.AreEqual(320, canvas.ImageData2.OriginalWidth);
        AssertImageDimensions(Path.Combine(fixturePath, "Image1.png"), 640, 480);
        AssertImageDimensions(Path.Combine(fixturePath, "Image2.png"), 320, 240);
        Assert.AreEqual(3, mojiDatas.Length);
        Assert.AreEqual(1, mojiDatas[0].FullText.Length);
        Assert.AreEqual("横", mojiDatas[0].FullText);
        Assert.AreEqual(TextDirection.Yokogaki, mojiDatas[0].TextDirection);
        Assert.AreNotEqual(0, mojiDatas[0].RotateAngle);
        Assert.AreEqual(4, mojiDatas[0].BorderThickness);
        Assert.AreEqual(2, mojiDatas[0].SecondBorderThickness);
        Assert.AreEqual(0, mojiDatas[0].BorderBlurrRadius);
        Assert.AreEqual(0, mojiDatas[0].SecondBorderBlurrRadius);
        Assert.IsTrue(mojiDatas[0].IsBackgroundBoxExists);
        Assert.AreEqual(1, mojiDatas[1].FullText.Length);
        Assert.AreEqual("縦", mojiDatas[1].FullText);
        Assert.AreEqual(TextDirection.Tategaki, mojiDatas[1].TextDirection);
        Assert.AreNotEqual(0, mojiDatas[1].RotateAngle);
        Assert.IsTrue(mojiDatas[2].FullText.Contains("日本語", StringComparison.Ordinal));
        Assert.IsTrue(mojiDatas[2].FullText.Contains("😀", StringComparison.Ordinal));
        Assert.IsTrue(mojiDatas[2].FullText.Contains("か\u3099", StringComparison.Ordinal));
        Assert.IsTrue(mojiDatas.All(data => data.BorderBlurrRadius == 0 && data.SecondBorderBlurrRadius == 0));
        Assert.IsTrue(mojiDatas.All(data => data.IsBackgroundBoxExists));
        Assert.IsTrue(mojiDatas.All(data => data.BorderThickness > 0 && data.SecondBorderThickness > 0));
    }

    [TestMethod]
    public void CurrentMctzipArchiveContainsExpectedRootEntries()
    {
        var archivePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "current-mctzip.mctzip");

        using var archive = ZipFile.OpenRead(archivePath);
        CollectionAssert.AreEquivalent(FixtureEntries, archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    [TestMethod]
    public void PerformanceMctzipFixtureContainsBlurDataset()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "performance-mctzip-root");
        var mojiDatas = DataIO.ReadMojiDatas(fixturePath);

        CollectionAssert.AreEquivalent(FixtureEntries, Directory.GetFiles(fixturePath).Select(Path.GetFileName).ToArray());
        Assert.AreEqual(LocatePosition.Right, DataIO.ReadCanvasData(fixturePath).Image2LocatePosition);
        AssertImageDimensions(Path.Combine(fixturePath, "Image1.png"), 640, 480);
        AssertImageDimensions(Path.Combine(fixturePath, "Image2.png"), 320, 240);
        Assert.AreEqual(3, mojiDatas.Count);
        Assert.IsTrue(mojiDatas.All(data => data.BorderBlurrRadius > 0));
        Assert.IsTrue(mojiDatas.All(data => data.SecondBorderBlurrRadius > 0));
    }

    [TestMethod]
    public void JapanesePathMctzipFixtureCanBeOpened()
    {
        var archivePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "日本語フォルダー", "現行プロジェクト.mctzip");

        Assert.IsTrue(File.Exists(archivePath));
        using var archive = ZipFile.OpenRead(archivePath);
        CollectionAssert.AreEquivalent(FixtureEntries, archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    private static readonly string[] FixtureEntries =
    {
        "Info.txt",
        "CanvasData.xml",
        "MojiData1.xml",
        "MojiData2.xml",
        "MojiData3.xml",
        "Image1.png",
        "Image2.png",
    };

    private static string NormalizeNewLines(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    private static void AssertImageDimensions(string path, int width, int height)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.AreEqual(width, decoder.Frames[0].PixelWidth, $"Unexpected width for {path}");
        Assert.AreEqual(height, decoder.Frames[0].PixelHeight, $"Unexpected height for {path}");
    }

    private static void AssertUtf8WithoutBom(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.IsFalse(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        _ = Encoding.UTF8.GetString(bytes);
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
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
