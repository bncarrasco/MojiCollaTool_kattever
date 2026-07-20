using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
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
        var source = CreateImageProject();
        var assets = new TestAssets();
        assets.Set(source.Pages[0].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image1.png"));
        assets.Set(source.Pages[0].PageId, 2, "jpg", ReadRepoFixture("TestImage/testimage.jpg"));
        assets.Set(source.Pages[1].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image2.png"));
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
        CollectionAssert.AreEqual(assets.Get(source.Pages[0].PageId, 1), restoredAssets.Get(source.Pages[0].PageId, 1));
        CollectionAssert.AreEqual(assets.Get(source.Pages[0].PageId, 2), restoredAssets.Get(source.Pages[0].PageId, 2));
        CollectionAssert.AreEqual(assets.Get(source.Pages[1].PageId, 1), restoredAssets.Get(source.Pages[1].PageId, 1));

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
        var source = CreateImageProject();
        var assets = new TestAssets();
        assets.Set(source.Pages[0].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image1.png"));
        assets.Set(source.Pages[0].PageId, 2, "jpg", ReadRepoFixture("TestImage/testimage.jpg"));
        assets.Set(source.Pages[1].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image2.png"));

        DataIO.WriteVersionedProject(archivePath, source, assets);
        DataIO.WriteVersionedProject(archivePath, CreateProject());

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.IsFalse(archive.Entries.Any(entry => entry.FullName.Contains("/image", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void WriterRejectsMetadataWithoutAssetAndAssetWithoutMetadata()
    {
        using var scope = TemporaryDirectory.Create();
        var metadataPath = Path.Combine(scope.Path, "metadata-without-asset.mctzip");
        Assert.ThrowsException<InvalidOperationException>(() => DataIO.WriteVersionedProject(metadataPath, CreateImageProject()));

        var assetPath = Path.Combine(scope.Path, "asset-without-metadata.mctzip");
        var assets = new TestAssets();
        var project = CreateProject();
        assets.Set(project.Pages[0].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image1.png"));
        Assert.ThrowsException<InvalidOperationException>(() => DataIO.WriteVersionedProject(assetPath, project, assets));
    }

    [TestMethod]
    public void ReaderRejectsMetadataAndAssetMismatches()
    {
        using var scope = TemporaryDirectory.Create();
        var sourcePath = Path.Combine(scope.Path, "valid.mctzip");
        var missingAssetPath = Path.Combine(scope.Path, "missing-asset.mctzip");
        var missingMetadataPath = Path.Combine(scope.Path, "missing-metadata.mctzip");
        var source = CreateImageProject();
        var assets = new TestAssets();
        assets.Set(source.Pages[0].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image1.png"));
        assets.Set(source.Pages[0].PageId, 2, "jpg", ReadRepoFixture("TestImage/testimage.jpg"));
        assets.Set(source.Pages[1].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image2.png"));
        DataIO.WriteVersionedProject(sourcePath, source, assets);

        RewriteEntry(sourcePath, missingAssetPath, $"pages/{source.Pages[0].PageId:D}/page.xml", null, page =>
        {
            page.Root!.Element("Image1Path")!.Value = string.Empty;
            return page;
        });
        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(missingAssetPath, new TestAssets()));

        RewriteEntry(sourcePath, missingMetadataPath, $"pages/{source.Pages[0].PageId:D}/page.xml", null, page =>
        {
            var imageData = page.Root!.Element("Canvas")!.Element("ImageData1")!;
            imageData.Element("OriginalWidth")!.Value = "0";
            imageData.Element("OriginalHeight")!.Value = "0";
            imageData.Element("ModifiedWidth")!.Value = "0";
            imageData.Element("ModifiedHeight")!.Value = "0";
            return page;
        });
        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(missingMetadataPath, new TestAssets()));
    }

    [TestMethod]
    public void ReaderRejectsCorruptImageAndSinklessAssetArchive()
    {
        using var scope = TemporaryDirectory.Create();
        var sourcePath = Path.Combine(scope.Path, "valid.mctzip");
        var corruptPath = Path.Combine(scope.Path, "corrupt.mctzip");
        var source = CreateImageProject();
        var assets = new TestAssets();
        assets.Set(source.Pages[0].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image1.png"));
        assets.Set(source.Pages[0].PageId, 2, "jpg", ReadRepoFixture("TestImage/testimage.jpg"));
        assets.Set(source.Pages[1].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image2.png"));
        DataIO.WriteVersionedProject(sourcePath, source, assets);

        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(sourcePath));
        RewriteEntry(sourcePath, corruptPath, $"pages/{source.Pages[0].PageId:D}/image1.png", new string('x', 32), null);
        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(corruptPath, new TestAssets()));
    }

    [TestMethod]
    public void BatchSinkFailureDoesNotApplyPartialAssetRestoration()
    {
        using var scope = TemporaryDirectory.Create();
        var archivePath = Path.Combine(scope.Path, "project.mctzip");
        var source = CreateImageProject();
        var assets = new TestAssets();
        assets.Set(source.Pages[0].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image1.png"));
        assets.Set(source.Pages[0].PageId, 2, "jpg", ReadRepoFixture("TestImage/testimage.jpg"));
        assets.Set(source.Pages[1].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image2.png"));
        DataIO.WriteVersionedProject(archivePath, source, assets);

        var sink = new ThrowingBatchSink();
        Assert.ThrowsException<InvalidOperationException>(() => DataIO.ReadVersionedProject(archivePath, sink));
        Assert.AreEqual(1, sink.CallCount);
        Assert.AreEqual(0, sink.AppliedAssetCount);
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
    public void Version24PersistsAllThreeLayoutStatesAndManifestVersions()
    {
        using var scope = TemporaryDirectory.Create();
        var archivePath = Path.Combine(scope.Path, "layout-2.3.mctzip");
        var source = CreateLayoutStateProject();

        DataIO.WriteVersionedProject(archivePath, source);

        using (var archive = ZipFile.OpenRead(archivePath))
        {
            var manifest = XDocument.Load(archive.GetEntry("manifest.xml")!.Open());
            Assert.AreEqual("2.4", manifest.Root!.Element("FormatVersion")!.Value);
            Assert.AreEqual("2.4", manifest.Root.Element("MinimumReaderVersion")!.Value);
        }

        var restored = DataIO.ReadVersionedProject(archivePath);
        CollectionAssert.AreEqual(
            new[]
            {
                BalloonTextLayoutMode.Unapplied,
                BalloonTextLayoutMode.FitTextToBalloon,
                BalloonTextLayoutMode.FitBalloonToText,
            },
            restored.Pages.Single().Balloons.Select(balloon => balloon.TextLink!.LayoutMode).ToArray());
    }

    [TestMethod]
    public void Version22LinksMigrateOnlyLayoutStateToUnappliedWithoutImplicitPlan()
    {
        using var scope = TemporaryDirectory.Create();
        var sourcePath = Path.Combine(scope.Path, "source-2.3.mctzip");
        var pagePath = Path.Combine(scope.Path, "legacy-page.mctzip");
        var legacyPath = Path.Combine(scope.Path, "legacy-2.2.mctzip");
        var source = CreateLayoutStateProject(includeUnknownFixtureLink: true);
        DataIO.WriteVersionedProject(sourcePath, source);

        var pageEntry = $"pages/{source.Pages.Single().PageId:D}/page.xml";
        RewriteEntry(sourcePath, pagePath, pageEntry, null, page =>
        {
            var links = page.Root!.Element("Balloons")!.Elements("Balloon")
                .Select(balloon => balloon.Element("TextLink")!).ToArray();
            links[0].Element("LayoutMode")!.Value = "FitTextToBalloon";
            links[1].Element("LayoutMode")!.Value = "FitBalloonToText";
            links[2].Element("LayoutMode")!.Remove();
            links[3].Element("LayoutMode")!.Value = "FutureLayoutMode";
            return page;
        });
        RewriteEntry(pagePath, legacyPath, "manifest.xml", null, manifest =>
        {
            manifest.Root!.Element("FormatVersion")!.Value = "2.2";
            manifest.Root.Element("MinimumReaderVersion")!.Value = "2.2";
            return manifest;
        });

        var restored = DataIO.ReadVersionedProject(legacyPath);
        var restoredPage = restored.Pages.Single();
        Assert.IsTrue(restoredPage.Balloons.All(balloon =>
            balloon.TextLink!.LayoutMode == BalloonTextLayoutMode.Unapplied));
        Assert.AreEqual(source.Pages.Single().MojiDatas[0].FullText, restoredPage.MojiDatas[0].FullText);
        Assert.AreEqual(source.Pages.Single().Balloons[1].TextLink!.Padding,
            restoredPage.Balloons[1].TextLink!.Padding);
        Assert.AreEqual(source.Pages.Single().Balloons[1].TextLink!.Alignment,
            restoredPage.Balloons[1].TextLink!.Alignment);

        RunOnSta(() =>
        {
            using var session = new ProjectSession(restored);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            Assert.IsFalse(session.IsDirty);
            Assert.AreEqual(0, session.UndoCount);
            Assert.IsTrue(editor.MojiPanels.All(panel => panel.ComputedLayout == null));
        });
    }

    [TestMethod]
    public void Version22ReadExplicitApplyThenVersion24SavePreservesLayoutAndCompositionState()
    {
        using var scope = TemporaryDirectory.Create();
        var sourcePath = Path.Combine(scope.Path, "source-2.3.mctzip");
        var legacyPath = Path.Combine(scope.Path, "source-2.2.mctzip");
        var savedPath = Path.Combine(scope.Path, "saved-2.3.mctzip");
        var text = new MojiData
        {
            FullText = "legacy\n明示Apply\r混在\r\n😀",
            FontSize = 28,
            X = 18,
            Y = 26,
            ZIndex = 2,
        };
        var balloon = new BalloonData
        {
            X = 90,
            Y = 110,
            Bounds = new System.Windows.Rect(0, 0, 82, 48),
            ZIndex = 0,
            Tail = new BalloonTailData { TipX = 130, TipY = 210, RootParameter = .7, Width = 18 },
            TextLink = new TextLinkData
            {
                TextObjectId = text.ObjectId,
                LayoutMode = BalloonTextLayoutMode.FitTextToBalloon,
                Padding = 5,
                MinimumFontSize = 11,
                Alignment = BalloonTextAlignment.End,
            },
        };
        var symbol = new AttachedSymbolData
        {
            ParentId = text.ObjectId,
            GraphemeAnchor = 0,
            AnchorText = "l",
            Text = "!",
            OffsetX = .25,
            OffsetY = -.5,
            ZIndex = 1,
        };
        var page = new PageDocument(Guid.NewGuid(), "legacy page", new CanvasData(), new[] { text },
            new[] { balloon }, new[] { symbol });
        var source = new ProjectDocument(Guid.NewGuid(), "legacy project", new[] { page });
        DataIO.WriteVersionedProject(sourcePath, source);

        RewriteEntry(sourcePath, legacyPath, "manifest.xml", null, manifest =>
        {
            manifest.Root!.Element("FormatVersion")!.Value = "2.2";
            manifest.Root.Element("MinimumReaderVersion")!.Value = "2.2";
            return manifest;
        });

        var migrated = DataIO.ReadVersionedProject(legacyPath);
        Assert.AreEqual(BalloonTextLayoutMode.Unapplied, migrated.Pages.Single().Balloons.Single().TextLink!.LayoutMode);
        Assert.AreEqual(text.FullText, migrated.Pages.Single().MojiDatas.Single().FullText);
        var originalOrder = migrated.Pages.Single().AllObjects.Select(item => item.ObjectId).ToArray();

        RunOnSta(() =>
        {
            using var session = new ProjectSession(migrated);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.RestoreViewState(100, migrated.Pages.Single().Balloons.Single().ObjectId);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(migrated.Pages.Single().PageId, editor.ContentChangeDescription,
                    editor.ContentChangeCoalesceKey);
            };

            Assert.IsTrue(editor.FitTextToBalloon());
            Assert.AreEqual(BalloonTextLayoutMode.FitTextToBalloon,
                migrated.Pages.Single().Balloons.Single().TextLink!.LayoutMode);
            Assert.IsNotNull(editor.MojiPanels.Single().ComputedLayout);
            Assert.AreEqual(1, session.UndoCount);
            DataIO.WriteVersionedProject(savedPath, session.Document);
        });

        using (var archive = ZipFile.OpenRead(savedPath))
        {
            var manifest = XDocument.Load(archive.GetEntry("manifest.xml")!.Open());
            Assert.AreEqual("2.4", manifest.Root!.Element("FormatVersion")!.Value);
        }

        var restored = DataIO.ReadVersionedProject(savedPath);
        var restoredPage = restored.Pages.Single();
        var restoredText = restoredPage.MojiDatas.Single();
        var restoredBalloon = restoredPage.Balloons.Single();
        var restoredSymbol = restoredPage.AttachedSymbols.Single();
        CollectionAssert.AreEqual(originalOrder, restoredPage.AllObjects.Select(item => item.ObjectId).ToArray());
        Assert.AreEqual("legacy\n明示Apply\r混在\r\n😀", restoredText.FullText);
        Assert.AreEqual(BalloonTextLayoutMode.FitTextToBalloon, restoredBalloon.TextLink!.LayoutMode);
        Assert.AreEqual(5, restoredBalloon.TextLink.Padding);
        Assert.AreEqual(11, restoredBalloon.TextLink.MinimumFontSize);
        Assert.AreEqual(BalloonTextAlignment.End, restoredBalloon.TextLink.Alignment);
        Assert.AreEqual(balloon.Tail!.Tip, restoredBalloon.Tail!.Tip);
        Assert.AreEqual(balloon.Tail.RootParameter, restoredBalloon.Tail.RootParameter);
        Assert.AreEqual(balloon.Tail.Width, restoredBalloon.Tail.Width);
        Assert.AreEqual(symbol.Text, restoredSymbol.Text);
        Assert.AreEqual(symbol.OffsetX, restoredSymbol.OffsetX);
        Assert.AreEqual(symbol.OffsetY, restoredSymbol.OffsetY);
        Assert.AreEqual(symbol.ParentId, restoredSymbol.ParentId);
    }

    [TestMethod]
    public void AssetSourceFailureLeavesOriginalArchiveUntouched()
    {
        using var scope = TemporaryDirectory.Create();
        var archivePath = Path.Combine(scope.Path, "project.mctzip");
        var source = CreateImageProject();
        var assets = new TestAssets();
        assets.Set(source.Pages[0].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image1.png"));
        assets.Set(source.Pages[0].PageId, 2, "jpg", ReadRepoFixture("TestImage/testimage.jpg"));
        assets.Set(source.Pages[1].PageId, 1, "png", ReadRepoFixture("tests/MojiCollaTool.Tests/Fixtures/current-mctzip-root/Image2.png"));
        DataIO.WriteVersionedProject(archivePath, source, assets);
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

    private static ProjectDocument CreateImageProject()
    {
        var project = CreateProject();
        var firstCanvas = project.Pages[0].CanvasData;
        firstCanvas.ImageData1 = new ImageData(640, 480) { ModifiedWidth = 640, ModifiedHeight = 480 };
        firstCanvas.ImageData2 = new ImageData(1000, 600) { ModifiedWidth = 320, ModifiedHeight = 480 };
        var secondCanvas = project.Pages[1].CanvasData;
        secondCanvas.ImageData1 = new ImageData(320, 240) { ModifiedWidth = 320, ModifiedHeight = 240 };
        return project;
    }

    private static ProjectDocument CreateLayoutStateProject(bool includeUnknownFixtureLink = false)
    {
        var texts = Enumerable.Range(0, includeUnknownFixtureLink ? 4 : 3)
            .Select(index => new MojiData { FullText = $"layout state {index}\n日本語" })
            .ToArray();
        var modes = new[]
        {
            BalloonTextLayoutMode.Unapplied,
            BalloonTextLayoutMode.FitTextToBalloon,
            BalloonTextLayoutMode.FitBalloonToText,
            BalloonTextLayoutMode.FitTextToBalloon,
        };
        var balloons = texts.Select((text, index) => new BalloonData
        {
            Bounds = new System.Windows.Rect(20 + index * 60, 30, 50, 40),
            TextLink = new TextLinkData
            {
                TextObjectId = text.ObjectId,
                LayoutMode = modes[index],
                Padding = 3 + index,
                MinimumFontSize = 8 + index,
                Alignment = (BalloonTextAlignment)(index % 3),
            },
        }).ToArray();
        return new ProjectDocument(Guid.NewGuid(), "layout states", new[]
        {
            new PageDocument("状態ページ", texts, balloons),
        });
    }

    private static byte[] ReadRepoFixture(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        if (directory == null) throw new InvalidOperationException("Repository root was not found.");
        return File.ReadAllBytes(Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new AssertFailedException(failure.ToString());
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

    private sealed class TestAssets : IProjectAssetSource, IProjectAssetSink, IProjectAssetBatchSink
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

        public void SaveImages(ProjectDocument project, IReadOnlyList<ProjectAssetRestore> assets)
        {
            foreach (var asset in assets)
            {
                Set(asset.PageId, asset.ImageNumber, asset.Extension, asset.Content);
            }
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

    private sealed class ThrowingBatchSink : IProjectAssetSink, IProjectAssetBatchSink
    {
        public int CallCount { get; private set; }

        public int AppliedAssetCount { get; private set; }

        public void SaveImage(Guid pageId, int imageNumber, string extension, Stream content)
        {
            throw new InvalidOperationException("A batch sink is required.");
        }

        public void SaveImages(ProjectDocument project, IReadOnlyList<ProjectAssetRestore> assets)
        {
            CallCount++;
            throw new InvalidOperationException("restore failed before commit");
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
