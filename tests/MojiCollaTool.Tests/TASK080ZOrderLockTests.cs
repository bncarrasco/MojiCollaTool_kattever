using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public sealed class TASK080ZOrderLockTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void CommonOrderOperationMovesEveryTypedMemberAsOneBlock()
    {
        var linkedText = new MojiData { FullText = "linked" };
        var balloon = new BalloonData();
        var linkedSymbol = new AttachedSymbolData
        {
            ParentId = linkedText.ObjectId,
            GraphemeAnchor = 0,
            Text = "!",
        };
        var unrelated = new MojiData { FullText = "other" };
        var front = new BalloonData();
        var page = new PageDocument("01", new[] { linkedText, unrelated }, new[] { balloon });
        page.AddAttachedSymbol(linkedSymbol);
        page.LinkBalloonText(balloon.ObjectId, linkedText.ObjectId);
        page.AddBalloon(front);

        Assert.AreEqual(3, page.GetObjectOrderBlock(linkedSymbol.ObjectId).Count);
        CollectionAssert.AreEqual(
            new[] { balloon.ObjectId, linkedText.ObjectId, linkedSymbol.ObjectId },
            page.GetObjectOrderBlock(linkedSymbol.ObjectId).ToArray());
        Assert.IsTrue(page.MoveObjectOrder(linkedSymbol.ObjectId, ObjectOrderOperation.BringToFront));
        CollectionAssert.AreEqual(
            new[] { unrelated.ObjectId, front.ObjectId, balloon.ObjectId, linkedText.ObjectId, linkedSymbol.ObjectId },
            page.AllObjects.Select(item => item.ObjectId).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, page.AllObjects.Select(item => item.ZIndex).ToArray());
    }

    [TestMethod]
    public void LockedCompositionMemberRejectsOrderChangeAtomically()
    {
        var text = new MojiData { FullText = "linked" };
        var balloon = new BalloonData();
        var page = new PageDocument("01", new[] { text }, new[] { balloon });
        page.LinkBalloonText(balloon.ObjectId, text.ObjectId);
        page.SetObjectLocked(text.ObjectId, true);
        var before = page.AllObjects.Select(item => item.ObjectId).ToArray();

        Assert.IsFalse(page.MoveObjectOrder(balloon.ObjectId, ObjectOrderOperation.BringToFront));
        CollectionAssert.AreEqual(before, page.AllObjects.Select(item => item.ObjectId).ToArray());
        Assert.IsTrue(page.GetDocumentObject(text.ObjectId).IsLocked);
    }

    [TestMethod]
    public void UnlinkedTextSymbolsShareABlockButDetachedSymbolsDoNot()
    {
        var text = new MojiData { FullText = "unlinked" };
        var symbol = new AttachedSymbolData { ParentId = text.ObjectId, GraphemeAnchor = 0, Text = "?" };
        var detached = new AttachedSymbolData { IsDetached = true, Text = "detached" };
        var page = new PageDocument("01", new[] { text });
        page.AddAttachedSymbol(symbol);
        page.AddAttachedSymbol(detached);

        CollectionAssert.AreEqual(new[] { text.ObjectId, symbol.ObjectId }, page.GetObjectOrderBlock(symbol.ObjectId).ToArray());
        CollectionAssert.AreEqual(new[] { detached.ObjectId }, page.GetObjectOrderBlock(detached.ObjectId).ToArray());
        Assert.IsTrue(page.MoveObjectOrder(symbol.ObjectId, ObjectOrderOperation.BringToFront));
        CollectionAssert.AreEqual(
            new[] { detached.ObjectId, text.ObjectId, symbol.ObjectId },
            page.AllObjects.Select(item => item.ObjectId).ToArray());
    }

    [TestMethod]
    public void CommonCommandsHaveOneHistoryEntryAndPreserveLockThroughUndoRedo()
    {
        var first = new MojiData { FullText = "first" };
        var second = new BalloonData();
        var page = new PageDocument("01", new[] { first }, new[] { second });
        using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "project", new[] { page }));

        Assert.IsTrue(ObjectCommands.Lock(session, page.PageId, first.ObjectId));
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsFalse(ObjectCommands.Lock(session, page.PageId, first.ObjectId));
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.IsFalse(session.Document.Pages[0].GetDocumentObject(first.ObjectId).IsLocked);
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(session.Document.Pages[0].GetDocumentObject(first.ObjectId).IsLocked);
        Assert.IsFalse(ObjectCommands.MoveOrder(session, page.PageId, first.ObjectId, ObjectOrderOperation.BringToFront));
        Assert.AreEqual(1, session.UndoCount);
    }

    [TestMethod]
    public void Version23RoundTripPreservesCommonOrderAndLockState()
    {
        var text = new MojiData { FullText = "保存" };
        var balloon = new BalloonData();
        var page = new PageDocument("日本語", new[] { text }, new[] { balloon });
        page.SetObjectLocked(text.ObjectId, true);
        page.MoveObjectOrder(text.ObjectId, ObjectOrderOperation.BringToFront);
        var project = new ProjectDocument(Guid.NewGuid(), "保存テスト", new[] { page });
        var path = Path.Combine(Path.GetTempPath(), $"task080-{Guid.NewGuid():N}.mctzip");
        try
        {
            DataIO.WriteVersionedProject(path, project);
            var restored = DataIO.ReadVersionedProject(path);
            CollectionAssert.AreEqual(
                page.AllObjects.Select(item => item.ObjectId).ToArray(),
                restored.Pages[0].AllObjects.Select(item => item.ObjectId).ToArray());
            Assert.IsTrue(restored.Pages[0].GetDocumentObject(text.ObjectId).IsLocked);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void HundredMixedObjectsMeetOrderOperationBudget()
    {
        var texts = Enumerable.Range(0, 60).Select(index => new MojiData { Id = index + 1, FullText = $"text-{index}" }).ToArray();
        var balloons = Enumerable.Range(0, 40).Select(_ => new BalloonData()).ToArray();
        var page = new PageDocument("performance", texts, balloons);
        var ids = page.AllObjects.Select(item => item.ObjectId).ToArray();
        var samples = new double[100];
        var total = Stopwatch.StartNew();
        for (var index = 0; index < samples.Length; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            page.MoveObjectOrder(ids[index], (ObjectOrderOperation)(index % 4));
            stopwatch.Stop();
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }
        total.Stop();
        Array.Sort(samples);
        TestContext.WriteLine($"mixed-objects=100; order-p95-ms={samples[94]:F3}; order-batch-100-ms={total.Elapsed.TotalMilliseconds:F3}");
        Assert.IsTrue(samples[94] < 50, $"single operation p95 was {samples[94]:F3} ms");
        Assert.IsTrue(total.Elapsed.TotalMilliseconds < 1000, $"100 operations took {total.Elapsed.TotalMilliseconds:F3} ms");
    }

    [TestMethod]
    public void CommonToolbarAndContextMenuExposeLockUnlockForText()
    {
        RunOnSta(() =>
        {
            var text = new MojiData { FullText = "text" };
            var page = new PageDocument("01", new[] { text });
            using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "project", new[] { page }));
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };
            editor.HandleMojiListItemClickFromUi(editor.MojiPanels.Single());

            ((Button)editor.FindName("LockButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsTrue(page.GetDocumentObject(text.ObjectId).IsLocked);
            Assert.IsFalse(((Button)editor.FindName("LockButton")!).IsEnabled);
            Assert.IsTrue(((Button)editor.FindName("UnlockButton")!).IsEnabled);
            var menu = editor.MojiPanels.Single().ContextMenu!;
            CollectionAssert.AreEquivalent(
                new[] { "最前面へ", "前面へ", "背面へ", "最背面へ", "ロック", "ロック解除" },
                menu.Items.OfType<MenuItem>().Select(item => item.Header as string).ToArray());
            menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "ロック解除")
                .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.IsFalse(page.GetDocumentObject(text.ObjectId).IsLocked);
            Assert.AreEqual(2, session.UndoCount);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null) Assert.Fail(exception.ToString());
    }
}
