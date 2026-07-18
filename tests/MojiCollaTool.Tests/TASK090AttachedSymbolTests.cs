using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class TASK090AttachedSymbolTests
{
    [TestMethod]
    public void GraphemeSegmentationKeepsUtf16RangesAndEmojiSequencesIntact()
    {
        var text = "A😀e\u0301👩\u200d👩\u200d👧\u200d👦か";
        var clusters = GraphemeService.Segment(text);

        Assert.AreEqual(5, clusters.Count);
        Assert.AreEqual("😀", clusters[1].Text);
        Assert.AreEqual(2, clusters[1].Utf16Length);
        Assert.AreEqual("e\u0301", clusters[2].Text);
        Assert.AreEqual("👩\u200d👩\u200d👧\u200d👦", clusters[3].Text);
        Assert.AreEqual(text, string.Concat(clusters));
    }

    [TestMethod]
    public void GraphemeSegmentationCoversVariationModifierRegionalIndicatorAndCrlf()
    {
        Assert.AreEqual(1, GraphemeService.Count("✈\uFE0F"));
        Assert.AreEqual("✈\uFE0F", GraphemeService.GetAt("✈\uFE0F", 0).Text);
        Assert.AreEqual(1, GraphemeService.Count("👍🏽"));
        Assert.AreEqual(1, GraphemeService.Count("🇯🇵"));
        Assert.AreEqual("🇯🇵", GraphemeService.GetAt("🇯🇵", 0).Text);
        Assert.AreEqual(3, GraphemeService.Count("A\r\nB"));
        Assert.AreEqual("\r\n", GraphemeService.GetAt("A\r\nB", 1).Text);
        Assert.AreEqual(0, GraphemeService.Count(string.Empty));
        Assert.AreEqual(string.Empty, GraphemeService.Slice(string.Empty, 0, 0));
    }

    [TestMethod]
    public void AttachedSymbolIsClonedWithRemappedParentAndSymbolIds()
    {
        var page = new PageDocument("記号", new[]
        {
            new MojiData { FullText = "A😀e\u0301" },
        });
        var parentId = page.MojiDatas[0].ObjectId;
        var symbol = new AttachedSymbolData
        {
            ParentId = parentId,
            Text = "!?",
            GraphemeAnchor = 1,
            OffsetX = 0.2,
            Scale = 0.8,
            IncludeInCharacterSpacing = false,
        };
        page.AddAttachedSymbol(symbol);

        var clone = page.Clone();
        Assert.AreEqual(1, clone.AttachedSymbols.Count);
        Assert.AreNotEqual(symbol.ObjectId, clone.AttachedSymbols[0].ObjectId);
        Assert.AreNotEqual(parentId, clone.AttachedSymbols[0].ParentId);
        Assert.AreEqual("😀", clone.AttachedSymbols[0].AnchorText);
        Assert.AreEqual(1, clone.AttachedSymbols[0].GraphemeAnchor);
    }

    [TestMethod]
    public void TextEditReanchorsAndAttachedSymbolOperationsAreUndoable()
    {
        var document = new ProjectDocument("履歴");
        var page = document.Pages[0];
        page.AddMojiData(new MojiData { FullText = "A😀B" });
        var parentId = page.MojiDatas[0].ObjectId;
        using var session = new ProjectSession(document);
        var symbol = new AttachedSymbolData { ParentId = parentId, Text = "!", GraphemeAnchor = 1 };

        AttachedSymbolCommands.Add(session, page.PageId, symbol);
        Assert.AreEqual(1, document.Pages[0].AttachedSymbols.Count);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, document.Pages[0].AttachedSymbols.Count);
        Assert.IsTrue(session.Redo());

        session.ExecutePage(page.PageId, current => current.UpdateMojiData(parentId, text => text.FullText = "X" + text.FullText), "文字入力");
        Assert.AreEqual(2, document.Pages[0].AttachedSymbols[0].GraphemeAnchor);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(1, document.Pages[0].AttachedSymbols[0].GraphemeAnchor);
    }

    [TestMethod]
    public void SetMojiDatasReanchorsToTheOnlyMatchingGrapheme()
    {
        var page = CreatePage("AB", out var parentId);
        page.AddAttachedSymbol(new AttachedSymbolData { ParentId = parentId, Text = "!", GraphemeAnchor = 1 });
        var replacement = page.MojiDatas.Select(item => item.Clone()).ToArray();
        replacement[0].FullText = "XAB";

        page.SetMojiDatas(replacement);

        Assert.AreEqual(2, page.AttachedSymbols[0].GraphemeAnchor);
        Assert.AreEqual("B", page.AttachedSymbols[0].AnchorText);
    }

    [TestMethod]
    public void AmbiguousReanchorDetachesInsteadOfChoosingAnArbitraryDuplicate()
    {
        var page = CreatePage("AB", out var parentId);
        page.AddAttachedSymbol(new AttachedSymbolData { ParentId = parentId, Text = "!", GraphemeAnchor = 1 });
        var replacement = page.MojiDatas.Select(item => item.Clone()).ToArray();
        replacement[0].FullText = "XABB";

        page.SetMojiDatas(replacement, AttachedSymbolOrphanPolicy.ReanchorToNearest);

        Assert.AreEqual(1, page.AttachedSymbols.Count);
        Assert.IsTrue(page.AttachedSymbols[0].IsDetached);
        Assert.IsNull(page.AttachedSymbols[0].ParentId);
    }

    [TestMethod]
    public void DetachRemoveAndRejectDoNotSearchForAnotherAnchor()
    {
        var detachPage = CreatePage("AB", out var detachParentId);
        detachPage.AddAttachedSymbol(new AttachedSymbolData { ParentId = detachParentId, Text = "!", GraphemeAnchor = 1 });
        var detachReplacement = detachPage.MojiDatas.Select(item => item.Clone()).ToArray();
        detachReplacement[0].FullText = "XAB";
        detachPage.SetMojiDatas(detachReplacement, AttachedSymbolOrphanPolicy.Detach);
        Assert.IsTrue(detachPage.AttachedSymbols[0].IsDetached);

        var removePage = CreatePage("AB", out var removeParentId);
        removePage.AddAttachedSymbol(new AttachedSymbolData { ParentId = removeParentId, Text = "!", GraphemeAnchor = 1 });
        var removeReplacement = removePage.MojiDatas.Select(item => item.Clone()).ToArray();
        removeReplacement[0].FullText = "XAB";
        removePage.SetMojiDatas(removeReplacement, AttachedSymbolOrphanPolicy.Remove);
        Assert.AreEqual(0, removePage.AttachedSymbols.Count);

        var rejectPage = CreatePage("AB", out var rejectParentId);
        rejectPage.AddAttachedSymbol(new AttachedSymbolData { ParentId = rejectParentId, Text = "!", GraphemeAnchor = 1 });
        var rejectReplacement = rejectPage.MojiDatas.Select(item => item.Clone()).ToArray();
        rejectReplacement[0].FullText = "XAB";
        Assert.ThrowsException<InvalidDataException>(() => rejectPage.SetMojiDatas(rejectReplacement, AttachedSymbolOrphanPolicy.Reject));
        Assert.AreEqual("AB", rejectPage.MojiDatas[0].FullText);
        Assert.AreEqual("B", rejectPage.AttachedSymbols[0].AnchorText);
    }

    [TestMethod]
    public void InvalidCrossObjectIdsParentIdsAndNumbersAreNonDestructive()
    {
        var page = CreatePage("AB", out var parentId);
        var balloon = new BalloonData();
        page.AddBalloon(balloon);
        page.AddAttachedSymbol(new AttachedSymbolData { ParentId = parentId, Text = "!", GraphemeAnchor = 1 });

        var duplicateText = page.MojiDatas.Select(item => item.Clone()).ToArray();
        duplicateText[0].ObjectId = balloon.ObjectId;
        Assert.ThrowsException<InvalidOperationException>(() => page.SetMojiDatas(duplicateText));
        Assert.AreEqual("AB", page.MojiDatas[0].FullText);

        var invalidSymbol = page.AttachedSymbols[0].Clone();
        invalidSymbol.ParentId = Guid.NewGuid();
        Assert.ThrowsException<InvalidDataException>(() => page.SetAttachedSymbols(new[] { invalidSymbol }));
        Assert.AreEqual(parentId, page.AttachedSymbols[0].ParentId);

        var originalOffset = page.AttachedSymbols[0].OffsetX;
        Assert.ThrowsException<InvalidDataException>(() => page.UpdateAttachedSymbol(
            page.AttachedSymbols[0].ObjectId, symbol => symbol.OffsetX = double.NaN));
        Assert.AreEqual(originalOffset, page.AttachedSymbols[0].OffsetX);

        var duplicateSymbol = page.AttachedSymbols[0].Clone();
        duplicateSymbol.ObjectId = balloon.ObjectId;
        Assert.ThrowsException<InvalidOperationException>(() => page.SetAttachedSymbols(new[] { duplicateSymbol }));
        Assert.AreEqual(1, page.AttachedSymbols.Count);

        var duplicateBalloon = balloon.Clone();
        duplicateBalloon.ObjectId = page.AttachedSymbols[0].ObjectId;
        Assert.ThrowsException<InvalidOperationException>(() => page.SetBalloons(new[] { duplicateBalloon }));
        Assert.AreEqual(1, page.Balloons.Count);
    }

    [TestMethod]
    public void AttachedSymbolHistorySupportsSavedDirtyAndRedoDiscard()
    {
        var document = new ProjectDocument("履歴");
        var page = document.Pages[0];
        page.AddMojiData(new MojiData { FullText = "AB" });
        var parentId = page.MojiDatas[0].ObjectId;
        using var session = new ProjectSession(document);
        var symbol = new AttachedSymbolData { ParentId = parentId, Text = "!", GraphemeAnchor = 1 };

        AttachedSymbolCommands.Add(session, page.PageId, symbol);
        session.MarkSaved();
        AttachedSymbolCommands.Update(session, page.PageId, symbol.ObjectId, item => item.OffsetX = 0.25);
        Assert.IsTrue(session.IsDirty);
        Assert.IsTrue(session.Undo());
        Assert.IsFalse(session.IsDirty);
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(session.IsDirty);
        Assert.IsTrue(session.Undo());
        AttachedSymbolCommands.Update(session, page.PageId, symbol.ObjectId, item => item.OffsetY = 0.5);
        Assert.IsFalse(session.CanRedo);
    }

    [TestMethod]
    public void AttachedSymbolsRoundTripInProjectFormat22()
    {
        var document = new ProjectDocument("保存");
        var page = document.Pages[0];
        page.AddMojiData(new MojiData { FullText = "日本😀語" });
        page.AddAttachedSymbol(new AttachedSymbolData
        {
            ParentId = page.MojiDatas[0].ObjectId,
            Text = "!!",
            GraphemeAnchor = 2,
            Rotation = 12,
        });

        var directory = Path.Combine(Path.GetTempPath(), "MojiCollaTool-TASK090-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "attached.mctzip");
            DataIO.WriteVersionedProject(path, document);
            var restored = DataIO.ReadVersionedProject(path);

            Assert.AreEqual("日本😀語", restored.Pages[0].MojiDatas[0].FullText);
            Assert.AreEqual(1, restored.Pages[0].AttachedSymbols.Count);
            Assert.AreEqual("!!", restored.Pages[0].AttachedSymbols[0].Text);
            Assert.AreEqual("😀", restored.Pages[0].AttachedSymbols[0].AnchorText);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Version21ArchiveRemainsReadable()
    {
        var document = new ProjectDocument("2.1互換");
        var page = document.Pages[0];
        page.AddMojiData(new MojiData { FullText = "AB" });
        page.AddAttachedSymbol(new AttachedSymbolData
        {
            ParentId = page.MojiDatas[0].ObjectId,
            Text = "!",
            GraphemeAnchor = 1,
        });

        var directory = Path.Combine(Path.GetTempPath(), "MojiCollaTool-TASK090-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var currentPath = Path.Combine(directory, "current.mctzip");
            var version21Path = Path.Combine(directory, "version21.mctzip");
            DataIO.WriteVersionedProject(currentPath, document);
            RewriteManifestVersion(currentPath, version21Path, "2.1");
            var restored = DataIO.ReadVersionedProject(version21Path);
            Assert.AreEqual(1, restored.Pages[0].AttachedSymbols.Count);
            Assert.AreEqual("!", restored.Pages[0].AttachedSymbols[0].Text);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void AttachedSymbolModelDoesNotExposeWpfColor()
    {
        Assert.IsFalse(typeof(AttachedSymbolData).GetProperties()
            .Any(property => property.PropertyType.FullName == "System.Windows.Media.Color"));
    }

    private static PageDocument CreatePage(string text, out Guid parentId)
    {
        var page = new PageDocument("本文", new[] { new MojiData { FullText = text } });
        parentId = page.MojiDatas[0].ObjectId;
        return page;
    }

    private static void RewriteManifestVersion(string sourcePath, string destinationPath, string version)
    {
        using var source = ZipFile.OpenRead(sourcePath);
        using var destination = ZipFile.Open(destinationPath, ZipArchiveMode.Create);
        foreach (var sourceEntry in source.Entries)
        {
            var destinationEntry = destination.CreateEntry(sourceEntry.FullName);
            using var input = sourceEntry.Open();
            using var output = destinationEntry.Open();
            if (sourceEntry.FullName != "manifest.xml")
            {
                input.CopyTo(output);
                continue;
            }

            var manifest = XDocument.Load(input);
            manifest.Root!.Element("FormatVersion")!.Value = version;
            manifest.Root.Element("MinimumReaderVersion")!.Value = version;
            var bytes = Encoding.UTF8.GetBytes(manifest.ToString(SaveOptions.DisableFormatting));
            output.Write(bytes, 0, bytes.Length);
        }
    }
}
