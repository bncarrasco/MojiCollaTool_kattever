using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MojiCollaTool.Export;

namespace MojiCollaTool.Tests;

[TestClass]
public class TASK050ExportTests
{
    [TestMethod]
    public void AllPagesUseProjectOrderZeroPaddingAndSafeUnicodeNames()
    {
        var project = new ProjectDocument("出力");
        project.Rename("出力");
        project.RenamePage(project.Pages[0].PageId, "A<>. ");
        project.AddPage("CON");
        project.AddPage("日本語");
        var directory = CreateTempDirectory();
        try
        {
            var plan = new PageExportService().CreatePlan(new PageExportRequest(
                project, PageExportScope.AllPages, project.Pages[0], directory, "接頭:辞", PageExportFormat.Png));

            Assert.AreEqual(3, plan.Items.Count);
            Assert.AreEqual("接頭_辞_01_A__.png", Path.GetFileName(plan.Items[0].FilePath));
            Assert.AreEqual("接頭_辞_02_CON_.png", Path.GetFileName(plan.Items[1].FilePath));
            Assert.AreEqual("接頭_辞_03_日本語.png", Path.GetFileName(plan.Items[2].FilePath));
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [TestMethod]
    public void ExistingFileCollisionIsDetectedBeforeExecution()
    {
        var project = new ProjectDocument("衝突");
        var directory = CreateTempDirectory();
        try
        {
            var service = new PageExportService();
            var request = new PageExportRequest(project, PageExportScope.AllPages, project.Pages[0], directory, "同名", PageExportFormat.Jpeg);
            var firstPlan = service.CreatePlan(request);
            File.WriteAllText(firstPlan.Items[0].FilePath, "既存");

            var exception = Assert.ThrowsException<PageExportCollisionException>(() => service.CreatePlan(request));
            Assert.AreEqual(firstPlan.Items[0].FilePath, exception.Paths.Single());
            Assert.AreEqual("jpg", Path.GetExtension(firstPlan.Items[0].FilePath).TrimStart('.'));
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [TestMethod]
    public void OnePageFailureDoesNotStopRemainingPages()
    {
        var project = new ProjectDocument("継続");
        project.AddPage("失敗");
        project.AddPage("成功");
        var directory = CreateTempDirectory();
        try
        {
            var service = new PageExportService();
            var plan = service.CreatePlan(new PageExportRequest(
                project, PageExportScope.AllPages, project.Pages[0], directory, "batch", PageExportFormat.Png));
            var result = service.Execute(plan, (item, temporaryPath, _) =>
            {
                if (item.Page.Name == "失敗") throw new InvalidOperationException("テスト失敗");
                File.WriteAllText(temporaryPath, item.Page.Name);
            });

            Assert.AreEqual(2, result.SuccessCount);
            Assert.AreEqual(1, result.FailureCount);
            Assert.AreEqual("失敗", result.Failed[0].Item.Page.Name);
            Assert.IsTrue(File.Exists(result.Succeeded[0].FilePath));
            Assert.IsTrue(File.Exists(result.Succeeded[1].FilePath));
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [TestMethod]
    public void InvalidOutputDirectoryIsAWholeBatchStartFailure()
    {
        var project = new ProjectDocument("出力先");
        var directory = Path.Combine(Path.GetTempPath(), "mct-export-parent-file-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(directory, "not a directory");
        try
        {
            var service = new PageExportService();
            var plan = service.CreatePlan(new PageExportRequest(
                project, PageExportScope.AllPages, project.Pages[0], directory, "batch", PageExportFormat.Png));

            Assert.ThrowsException<PageExportStartException>(() => service.Execute(plan, (_, _, _) => Assert.Fail("renderer must not run")));
        }
        finally
        {
            File.Delete(directory);
        }
    }

    [TestMethod]
    public void RendererKeepsCanvasDimensionsAndUsesWhiteJpegBackground()
    {
        var directory = CreateTempDirectory();
        try
        {
            var paths = RunOnSta(() =>
            {
                using var editor = new PageEditorControl();
                editor.SetCanvasData(new CanvasData
                {
                    CanvasWidth = 24,
                    CanvasHeight = 16,
                    CanvasColor = Colors.Transparent,
                });
                var pngPath = Path.Combine(directory, "transparent.png");
                var jpegPath = Path.Combine(directory, "white.jpg");
                editor.ExportImage(pngPath, new PngBitmapEncoder());
                editor.ExportImage(jpegPath, new JpegBitmapEncoder());
                return (pngPath, jpegPath);
            });

            var png = ReadBitmap(paths.pngPath);
            var jpeg = ReadBitmap(paths.jpegPath);
            Assert.AreEqual(24, png.PixelWidth);
            Assert.AreEqual(16, png.PixelHeight);
            Assert.AreEqual(0, png.CopyPixelsAndGetAlpha());
            Assert.AreEqual(24, jpeg.PixelWidth);
            Assert.AreEqual(16, jpeg.PixelHeight);
            Assert.IsTrue(jpeg.CopyPixelsAndGetRed() >= 240);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [TestMethod]
    public void JpegPreservesOpaqueCanvasColor()
    {
        var pixel = RenderJpegPixel(Colors.Red);

        Assert.IsTrue(pixel.red >= 200, $"red={pixel.red}");
        Assert.IsTrue(pixel.green <= 80, $"green={pixel.green}");
        Assert.IsTrue(pixel.blue <= 80, $"blue={pixel.blue}");
    }

    [TestMethod]
    public void JpegCompositesSemiTransparentCanvasColorOverWhite()
    {
        var pixel = RenderJpegPixel(Color.FromArgb(128, 255, 0, 0));

        Assert.IsTrue(pixel.red >= 200, $"red={pixel.red}");
        Assert.IsTrue(pixel.green is >= 80 and <= 190, $"green={pixel.green}");
        Assert.IsTrue(pixel.blue is >= 80 and <= 190, $"blue={pixel.blue}");
    }

    [TestMethod]
    public void JpegCompositesFullyTransparentCanvasColorToWhite()
    {
        var pixel = RenderJpegPixel(Colors.Transparent);

        Assert.IsTrue(pixel.red >= 240, $"red={pixel.red}");
        Assert.IsTrue(pixel.green >= 240, $"green={pixel.green}");
        Assert.IsTrue(pixel.blue >= 240, $"blue={pixel.blue}");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mct-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private static BitmapSource ReadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return decoder.Frames[0];
    }

    private static (byte red, byte green, byte blue) RenderJpegPixel(Color canvasColor)
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "color.jpg");
        try
        {
            RunOnSta(() =>
            {
                using var editor = new PageEditorControl();
                editor.SetCanvasData(new CanvasData
                {
                    CanvasWidth = 32,
                    CanvasHeight = 32,
                    CanvasColor = canvasColor,
                });
                editor.ExportImage(path, new JpegBitmapEncoder());
                return true;
            });

            var bitmap = new FormatConvertedBitmap(ReadBitmap(path), PixelFormats.Bgra32, null, 0);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return (pixels[2], pixels[1], pixels[0]);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null) Assert.Fail(exception.ToString());
        return result!;
    }
}

internal static class BitmapSourceTestExtensions
{
    public static byte CopyPixelsAndGetAlpha(this BitmapSource bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return pixels[3];
    }

    public static byte CopyPixelsAndGetRed(this BitmapSource bitmap)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        return pixels[2];
    }
}
