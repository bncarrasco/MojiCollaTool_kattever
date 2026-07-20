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

    [TestMethod]
    public void LockedCompositionGeometryAndMidGestureLockAreAtomic()
    {
        RunOnSta(() =>
        {
            var text = new MojiData { FullText = "縦書き", TextDirection = TextDirection.Tategaki };
            var balloon = new BalloonData { Tail = null };
            var symbol = new AttachedSymbolData
            {
                ParentId = text.ObjectId, GraphemeAnchor = 0, Text = "!", OffsetX = 1, OffsetY = 2,
            };
            var page = new PageDocument("01", new[] { text }, new[] { balloon });
            page.AddAttachedSymbol(symbol);
            page.LinkBalloonText(balloon.ObjectId, text.ObjectId);
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            var panel = editor.MojiPanels.Single();
            var visual = editor.BalloonVisuals.Single();
            var symbolVisual = editor.AttachedSymbolVisuals.Single();
            var beforeBalloonX = visual.BalloonData.X;
            var beforeTextX = panel.MojiData.X;

            panel.MojiData.IsLocked = true;
            Assert.IsFalse(editor.BeginBalloonGesture(visual.ObjectId, new Point(0, 0)));
            Assert.AreEqual(beforeBalloonX, visual.BalloonData.X);
            Assert.AreEqual(beforeTextX, panel.MojiData.X);

            panel.MojiData.IsLocked = false;
            symbolVisual.SymbolData.IsLocked = true;
            Assert.IsFalse(editor.BeginBalloonGesture(visual.ObjectId, new Point(0, 0)));
            symbolVisual.SymbolData.IsLocked = false;
            Assert.IsTrue(editor.BeginBalloonGesture(visual.ObjectId, new Point(0, 0)));
            editor.UpdateBalloonGesture(new Point(50, 40));
            panel.MojiData.IsLocked = true;
            Assert.IsFalse(editor.CommitBalloonGesture(new Point(50, 40)));
            Assert.AreEqual(beforeBalloonX, visual.BalloonData.X);
            Assert.AreEqual(beforeTextX, panel.MojiData.X);
            Assert.IsTrue(panel.MojiData.IsLocked);

            panel.MojiData.IsLocked = false;
            var beforeOffset = symbolVisual.SymbolData.OffsetX;
            Assert.IsTrue(editor.BeginAttachedSymbolGesture(symbolVisual.ObjectId, new Point(0, 0)));
            editor.UpdateAttachedSymbolGesture(new Point(30, 0));
            symbolVisual.SymbolData.IsLocked = true;
            Assert.IsFalse(editor.CommitAttachedSymbolGesture(new Point(30, 0)));
            Assert.AreEqual(beforeOffset, symbolVisual.SymbolData.OffsetX);
            Assert.IsTrue(symbolVisual.SymbolData.IsLocked);

            // A resize/tail operation changes only the balloon, so a locked
            // linked text does not block that independent geometry target.
            symbolVisual.SymbolData.IsLocked = false;
            visual.BalloonData.Tail = BalloonTailGeometry.CreateDefault(visual.BalloonData);
            panel.MojiData.IsLocked = true;
            Assert.IsTrue(editor.BeginBalloonGesture(visual.ObjectId, new Point(0, 0), BalloonResizeHandle.TailTip));
            editor.UpdateBalloonGesture(new Point(20, 20));
            Assert.IsTrue(editor.CommitBalloonGesture(new Point(20, 20)));
        });
    }

    [TestMethod]
    public void LockedMojiWindowBlocksPropertyColorFormatAndAttachedCrudUntilUnlock()
    {
        RunOnSta(() =>
        {
            var text = new MojiData { FullText = "編集対象" };
            var page = new PageDocument("01", new[] { text });
            using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "project", new[] { page }));
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };
            var panel = editor.MojiPanels.Single();
            var window = panel.MojiWindow!;
            var lockButton = (Button)editor.FindName("LockButton")!;
            editor.HandleMojiListItemClickFromUi(panel);
            lockButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            session.MarkSaved();
            var before = panel.MojiData.Clone();
            var beforeUndo = session.UndoCount;

            Assert.IsFalse(((TextBox)window.FindName("TextTextBox")!).IsEnabled);
            Assert.IsFalse(((Button)window.FindName("ForeColorButton")!).IsEnabled);
            Assert.IsFalse(((Button)window.FindName("LoadFormatButton")!).IsEnabled);
            Assert.IsFalse(((Button)window.FindName("AttachedSymbolAddButton")!).IsEnabled);
            ((TextBox)window.FindName("TextTextBox")!).Text = "迂回変更";
            Assert.IsFalse(window.TryApplyLoadedFormat(new MojiData { ForeColor = System.Windows.Media.Colors.Red }));
            Assert.IsFalse(window.TryAddAttachedSymbolFromUi(0, "!", out _));
            Assert.AreEqual(before.FullText, panel.MojiData.FullText);
            Assert.AreEqual(before.ForeColor, panel.MojiData.ForeColor);
            Assert.AreEqual(beforeUndo, session.UndoCount);
            Assert.IsFalse(session.IsDirty);

            editor.HandleMojiListItemClickFromUi(panel);
            Assert.IsNull(editor.SelectedObjectId);
            editor.SelectObjectForContextFromUi(text.ObjectId);
            var contextMenu = panel.ContextMenu!;
            contextMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            var unlockItem = contextMenu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "ロック解除");
            Assert.IsTrue(unlockItem.IsEnabled);
            unlockItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.IsTrue(((TextBox)window.FindName("TextTextBox")!).IsEnabled);
            ((TextBox)window.FindName("TextTextBox")!).Text = "解除後の変更";
            Assert.AreEqual("解除後の変更", panel.MojiData.FullText);
            Assert.IsTrue(window.TryAddAttachedSymbolFromUi(0, "!", out var error), error);
        });
    }

    [TestMethod]
    public void SemanticCommandsRefuseLockedTargetsWithoutHistory()
    {
        var text = new MojiData { FullText = "text" };
        var balloon = new BalloonData();
        var symbol = new AttachedSymbolData { ParentId = text.ObjectId, GraphemeAnchor = 0, Text = "!" };
        var page = new PageDocument("01", new[] { text }, new[] { balloon });
        page.AddAttachedSymbol(symbol);
        page.LinkBalloonText(balloon.ObjectId, text.ObjectId);
        using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "project", new[] { page }));
        var symbolPage = session.Document.GetPage(page.PageId);
        symbolPage.SetObjectLocked(balloon.ObjectId, true);
        symbolPage.SetObjectLocked(text.ObjectId, true);
        symbolPage.SetObjectLocked(symbol.ObjectId, true);
        var beforeBalloonX = symbolPage.GetBalloon(balloon.ObjectId).X;
        var beforeUndo = session.UndoCount;

        Assert.IsFalse(BalloonCommands.Remove(session, page.PageId, balloon.ObjectId));
        BalloonCommands.Update(session, page.PageId, balloon.ObjectId, target => target.X += 10);
        BalloonCommands.SetTail(session, page.PageId, balloon.ObjectId, BalloonTailGeometry.CreateDefault(balloon));
        BalloonCommands.LinkText(session, page.PageId, balloon.ObjectId, text.ObjectId);
        BalloonCommands.UnlinkText(session, page.PageId, balloon.ObjectId);
        AttachedSymbolCommands.Update(session, page.PageId, symbol.ObjectId, target => target.OffsetX += 1);
        AttachedSymbolCommands.Reanchor(session, page.PageId, symbol.ObjectId, 0);
        AttachedSymbolCommands.Remove(session, page.PageId, symbol.ObjectId);

        Assert.AreEqual(beforeUndo, session.UndoCount);
        Assert.IsFalse(session.IsDirty);
        Assert.AreEqual(beforeBalloonX, symbolPage.GetBalloon(balloon.ObjectId).X);
        Assert.IsTrue(symbolPage.GetDocumentObject(balloon.ObjectId).IsLocked);
        Assert.IsTrue(symbolPage.GetDocumentObject(text.ObjectId).IsLocked);
        Assert.IsTrue(symbolPage.GetDocumentObject(symbol.ObjectId).IsLocked);
    }

    [TestMethod]
    public void ProductionAvailabilityCoversTypesEdgesLockedMembersAndContextMenuOpened()
    {
        RunOnSta(() =>
        {
            var text = new MojiData { FullText = "linked", TextDirection = TextDirection.Tategaki };
            var other = new MojiData { FullText = "other" };
            var balloon = new BalloonData();
            var symbol = new AttachedSymbolData { ParentId = text.ObjectId, GraphemeAnchor = 0, Text = "!" };
            var page = new PageDocument("01", new[] { text, other }, new[] { balloon });
            page.AddAttachedSymbol(symbol);
            page.LinkBalloonText(balloon.ObjectId, text.ObjectId);
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            var front = (Button)editor.FindName("BringToFrontButton")!;
            var back = (Button)editor.FindName("SendToBackButton")!;
            var lockButton = (Button)editor.FindName("LockButton")!;
            var unlockButton = (Button)editor.FindName("UnlockButton")!;
            var panel = editor.MojiPanels.First(item => item.MojiData.ObjectId == text.ObjectId);
            var visual = editor.BalloonVisuals.Single();
            var symbolVisual = editor.AttachedSymbolVisuals.Single();

            editor.HandleMojiListItemClickFromUi(panel);
            Assert.IsTrue(front.IsEnabled || back.IsEnabled);
            Assert.IsFalse(front.IsEnabled && back.IsEnabled);
            Assert.IsTrue(lockButton.IsEnabled);
            Assert.IsFalse(unlockButton.IsEnabled);
            var beforeTextOrder = page.AllObjects.Select(item => item.ObjectId).ToArray();
            (front.IsEnabled ? front : back).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            CollectionAssert.AreNotEqual(beforeTextOrder, page.AllObjects.Select(item => item.ObjectId).ToArray());
            Assert.AreEqual(text.ObjectId, editor.SelectedObjectId);
            editor.SelectAttachedSymbolFromUi(symbolVisual);
            Assert.IsTrue(front.IsEnabled || back.IsEnabled);
            Assert.IsFalse(front.IsEnabled && back.IsEnabled);
            (front.IsEnabled ? front : back).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(symbol.ObjectId, editor.SelectedObjectId);

            editor.SelectBalloonFromUi(visual);
            lockButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            editor.SelectBalloonFromUi(visual);
            var menu = visual.ContextMenu!;
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            var menuItems = menu.Items.OfType<MenuItem>().ToDictionary(item => (string)item.Header);
            Assert.IsFalse(front.IsEnabled);
            Assert.IsFalse(lockButton.IsEnabled);
            Assert.IsTrue(unlockButton.IsEnabled);
            Assert.IsTrue(menuItems["ロック解除"].IsEnabled);
            Assert.IsFalse(menuItems["最前面へ"].IsEnabled);

            // The selected balloon is unlocked, but its linked text is locked;
            // Z operations are blocked while locking the selected balloon is not.
            ((Button)editor.FindName("UnlockButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            editor.HandleMojiListItemClickFromUi(panel);
            lockButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            editor.SelectBalloonFromUi(visual);
            Assert.IsFalse(front.IsEnabled);
            Assert.IsTrue(lockButton.IsEnabled);
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            Assert.IsTrue(menuItems["ロック"].IsEnabled);
            Assert.IsFalse(menuItems["最前面へ"].IsEnabled);
        });
    }

    [TestMethod]
    public void SelectionLifecycleAndDisposeDoNotLeakObjectContextState()
    {
        RunOnSta(() =>
        {
            var firstText = new MojiData { FullText = "first" };
            var secondText = new MojiData { FullText = "second" };
            var firstPage = new PageDocument("01", new[] { firstText });
            var secondPage = new PageDocument("02", new[] { secondText });
            var project = new ProjectDocument(Guid.NewGuid(), "日本語 project", new[] { firstPage, secondPage });
            using var editor = new PageEditorControl();
            editor.BindPage(firstPage, null);
            editor.HandleMojiListItemClickFromUi(editor.MojiPanels.Single());
            editor.BindPage(secondPage, null);
            Assert.IsNull(editor.SelectedObjectId);
            Assert.IsTrue(editor.MojiPanels.Single().ContextMenu != null);
            editor.BindPage(firstPage, null);
            Assert.AreEqual(firstText.ObjectId, editor.SelectedObjectId);
            var menu = editor.MojiPanels.Single().ContextMenu!;
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            editor.Dispose();
            Assert.IsNull(editor.MojiPanels.SingleOrDefault()?.ContextMenu);
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
