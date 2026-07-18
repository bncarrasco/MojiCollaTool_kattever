using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class TASK100AttachedSymbolUiTests
{
    [TestMethod]
    public void PageEditorBuildsOneTextVisualPerGraphemeCluster()
    {
        RunOnSta(() =>
        {
            using var editor = new PageEditorControl();
            var page = new PageDocument("書記素", new[]
            {
                new MojiData { FullText = "A😀e\u0301👩\u200d👩" },
            });
            editor.BindPage(page, null);

            var panel = editor.MojiPanels[0];
            Assert.AreEqual(4, panel.GraphemeVisuals.Count);
            Assert.AreEqual("😀", panel.GraphemeVisuals[1].GraphemeText);
            Assert.AreEqual("e\u0301", panel.GraphemeVisuals[2].GraphemeText);
            Assert.AreEqual("👩\u200d👩", panel.GraphemeVisuals[3].GraphemeText);
        });
    }

    [TestMethod]
    public void AttachedSymbolUiCaptureReanchorsAndSupportsUndoRedo()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("付加記号UI");
            var page = document.Pages[0];
            page.AddMojiData(new MojiData { FullText = "A😀B" });
            using var session = new ProjectSession(document);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(session.ActivePage!.PageId, editor.ContentChangeDescription,
                    editor.ContentChangeCoalesceKey);
            };

            var parentId = editor.MojiPanels[0].MojiData.ObjectId;
            var visual = editor.AddAttachedSymbol(parentId, 1, "!?");
            Assert.AreEqual("😀", visual.SymbolData.AnchorText);
            Assert.AreEqual(1, session.ActivePage!.AttachedSymbols.Count);

            editor.MojiPanels[0].MojiData.FullText = "X" + editor.MojiPanels[0].MojiData.FullText;
            editor.MojiPanels[0].UpdateMojiView(false);
            editor.NotifyContentChanged("文字入力", parentId.ToString("D"));
            Assert.AreEqual(2, session.ActivePage!.AttachedSymbols[0].GraphemeAnchor);
            Assert.AreEqual(2, editor.AttachedSymbolVisuals[0].SymbolData.GraphemeAnchor);

            Assert.IsTrue(session.Undo());
            editor.BindPage(session.ActivePage!, null);
            Assert.AreEqual(1, editor.AttachedSymbolVisuals[0].SymbolData.GraphemeAnchor);
            Assert.IsTrue(session.Redo());
            editor.BindPage(session.ActivePage!, null);
            Assert.AreEqual(2, editor.AttachedSymbolVisuals[0].SymbolData.GraphemeAnchor);
        });
    }

    [TestMethod]
    public void GraphemeLayoutCoversSelectorsModifiersZwjFlagsNewlinesAndEmptyLines()
    {
        RunOnSta(() =>
        {
            const string text = "A\uFE0F\U0001F44D\U0001F3FD\U0001F469\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466\U0001F1EF\U0001F1F5\U0001F1FA\U0001F1F8\r\n\r\n";
            var clusters = GraphemeService.Segment(text);
            Assert.IsTrue(clusters.Any(item => item.Text.Contains('\uFE0F')));
            Assert.IsTrue(clusters.Any(item => item.Text.Contains('\u200D')));
            Assert.AreEqual(2, clusters.Count(item => item.Text == "\U0001F1EF\U0001F1F5" || item.Text == "\U0001F1FA\U0001F1F8"));

            using var editor = new PageEditorControl();
            var page = new PageDocument("graphemes", new[] { new MojiData { FullText = text } });
            editor.BindPage(page, null);
            Assert.AreEqual(clusters.Count(item => item.Text != "\r\n"), editor.MojiPanels[0].GraphemeVisuals.Count);
            Assert.IsTrue(editor.MojiPanels[0].GraphemeVisuals.Values.All(control => control.Width >= 0));
        });
    }

    [TestMethod]
    public void AttachedSymbolAddValidationIsAtomicForInvalidTextAnchorAndId()
    {
        RunOnSta(() =>
        {
            var page = new PageDocument("atomic", new[] { new MojiData { FullText = "AB" } });
            using var session = new ProjectSession(new ProjectDocument("atomic-project"));
            session.Document.Pages[0].SetMojiDatas(page.MojiDatas);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            var parentId = editor.MojiPanels[0].MojiData.ObjectId;
            var beforeVisuals = editor.AttachedSymbolVisuals.Count;
            var beforeModels = editor.AttachedSymbols.Count();
            var beforeHistory = session.UndoCount;

            Assert.ThrowsException<ArgumentException>(() => editor.AddAttachedSymbol(parentId, 0, string.Empty));
            Assert.ThrowsException<InvalidDataException>(() => editor.AddAttachedSymbol(parentId, 0, new string('x', 257)));
            Assert.ThrowsException<InvalidOperationException>(() => editor.AddAttachedSymbol(parentId, 9, "!"));
            Assert.ThrowsException<InvalidOperationException>(() => editor.AddAttachedSymbol(new AttachedSymbolData
            {
                ObjectId = parentId,
                ParentId = parentId,
                GraphemeAnchor = 0,
                Text = "!"
            }));

            Assert.AreEqual(beforeVisuals, editor.AttachedSymbolVisuals.Count);
            Assert.AreEqual(beforeModels, editor.AttachedSymbols.Count());
            Assert.AreEqual(beforeHistory, session.UndoCount);
            Assert.AreEqual(0, session.ActivePage!.AttachedSymbols.Count);
        });
    }

    [TestMethod]
    public void AttachedSymbolDragUsesTwoIndependentUndoGesturesAndCaptureLossRestores()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("drag-project");
            var page = document.Pages[0];
            page.AddMojiData(new MojiData { FullText = "AB" });
            using var session = new ProjectSession(document);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(session.ActivePage!.PageId, editor.ContentChangeDescription,
                    editor.ContentChangeCoalesceKey);
            };
            var visual = editor.AddAttachedSymbol(editor.MojiPanels[0].MojiData.ObjectId, 0, "!");
            var initialHistory = session.UndoCount;
            var initialX = visual.SymbolData.OffsetX;

            Assert.IsTrue(editor.BeginAttachedSymbolGesture(visual.ObjectId, new Point(0, 0)));
            editor.UpdateAttachedSymbolGesture(new Point(10, 0));
            Assert.IsTrue(editor.CommitAttachedSymbolGesture(new Point(10, 0)));
            Assert.IsTrue(editor.BeginAttachedSymbolGesture(visual.ObjectId, new Point(0, 0)));
            editor.UpdateAttachedSymbolGesture(new Point(20, 0));
            Assert.IsTrue(editor.CommitAttachedSymbolGesture(new Point(20, 0)));
            Assert.AreEqual(initialHistory + 2, session.UndoCount);

            var afterTwo = visual.SymbolData.OffsetX;
            Assert.IsTrue(editor.BeginAttachedSymbolGesture(visual.ObjectId, new Point(0, 0)));
            editor.UpdateAttachedSymbolGesture(new Point(40, 0));
            editor.CancelAttachedSymbolGesture();
            Assert.AreEqual(afterTwo, visual.SymbolData.OffsetX);
            Assert.AreNotEqual(initialX, afterTwo);
        });
    }

    [TestMethod]
    public void DetachedAttachedSymbolSurvivesParentDeletePageSwitchAndReload()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("detached-project");
            var first = document.Pages[0];
            first.AddMojiData(new MojiData { FullText = "AB" });
            var second = document.AddPage("second");
            using var session = new ProjectSession(document);
            using var editor = new PageEditorControl();
            editor.BindPage(first, null);
            var parent = editor.MojiPanels[0];
            editor.AddAttachedSymbol(parent.MojiData.ObjectId, 0, "!");
            editor.RemoveMojiPanel(parent);
            Assert.AreEqual(1, editor.AttachedSymbols.Count());
            Assert.IsTrue(editor.AttachedSymbols.Single().IsDetached);
            Assert.AreEqual(0, editor.AttachedSymbolVisuals.Count);

            session.ActivatePage(second.PageId);
            editor.BindPage(second, null);
            session.ActivatePage(first.PageId);
            editor.BindPage(first, null);
            editor.CapturePage();
            Assert.IsTrue(first.AttachedSymbols.Single().IsDetached);

            var path = Path.Combine(Path.GetTempPath(), $"task100-{Guid.NewGuid():N}.mctzip");
            try
            {
                DataIO.WriteVersionedProject(path, session.Document);
                var restored = DataIO.ReadVersionedProject(path);
                Assert.IsTrue(restored.Pages.Single(page => page.PageId == first.PageId).AttachedSymbols.Single().IsDetached);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        });
    }

    [TestMethod]
    public void AttachedSymbolCandidatesInheritanceFallbackVisibilityAndPlacementArePersisted()
    {
        RunOnSta(() =>
        {
            var page = new PageDocument("features", new[] { new MojiData { FullText = "AB", IsItalic = true } });
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            var parent = editor.MojiPanels[0];
            foreach (var candidate in new[] { "!", "?", "!?", "!!", "?!", "\u3099", "\u309A", "任意文字" })
                editor.AddAttachedSymbol(parent.MojiData.ObjectId, 0, candidate);
            var selected = editor.AttachedSymbolVisuals.Last();
            editor.UpdateAttachedSymbol(selected.ObjectId, symbol =>
            {
                symbol.Inherit = AttachedSymbolInheritance.Decoration;
                symbol.IncludeInCharacterSpacing = true;
                symbol.FontFamilyName = "not-a-real-font";
                symbol.IsVisible = false;
            });
            Assert.IsTrue(selected.IsFontFallback);
            Assert.AreEqual(Visibility.Hidden, selected.Visibility);
            Assert.AreEqual(0, VisualTreeHelper.GetChildrenCount(selected));

            var before = selected.AnchorBounds;
            parent.MojiData.TextDirection = TextDirection.Tategaki;
            parent.MojiData.X = 120;
            parent.MojiData.Y = 80;
            parent.MojiData.RotateAngle = 15;
            parent.MojiData.FontSize = 72;
            parent.UpdateXYView();
            parent.UpdateMojiView(true);
            Assert.IsFalse(selected.AnchorBounds.IsEmpty);
            Assert.AreNotEqual(before, selected.AnchorBounds);
            editor.CapturePage();
            var saved = page.AttachedSymbols.Single(symbol => symbol.ObjectId == selected.ObjectId);
            Assert.IsTrue(saved.IncludeInCharacterSpacing);
            Assert.AreEqual("not-a-real-font", saved.FontFamilyName);
            Assert.IsTrue(saved.Inherit.HasFlag(AttachedSymbolInheritance.Decoration));
        });
    }

    [TestMethod]
    public void GraphemeRefreshPerformanceUsesFullClusterPool()
    {
        RunOnSta(() =>
        {
            using var editor = new PageEditorControl();
            var page = new PageDocument("performance", new[] { new MojiData { FullText = string.Concat(Enumerable.Repeat("A\U0001F44D\U0001F3FD\u0301", 80)) } });
            editor.BindPage(page, null);
            var panel = editor.MojiPanels[0];
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < 100; i++) panel.UpdateMojiView(false);
            stopwatch.Stop();
            Assert.IsTrue(panel.GraphemeVisuals.Count > 0);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, $"Grapheme refresh took {stopwatch.ElapsedMilliseconds} ms.");
        });
    }

    [TestMethod]
    public void AttachedSymbolSelectionRestoresAcrossPageSwitchAndDisposeIsSafe()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("selection-project");
            var first = document.Pages[0];
            first.AddMojiData(new MojiData { FullText = "AB" });
            var second = document.AddPage("second");
            using var session = new ProjectSession(document);
            var editor = new PageEditorControl();
            editor.BindPage(first, null);
            var visual = editor.AddAttachedSymbol(editor.MojiPanels[0].MojiData.ObjectId, 0, "!");
            editor.RestoreViewState(100, visual.ObjectId);
            Assert.AreEqual(visual.ObjectId, editor.SelectedAttachedSymbolId);
            session.ActivatePage(second.PageId);
            editor.BindPage(second, null);
            session.ActivatePage(first.PageId);
            editor.BindPage(first, null);
            Assert.AreEqual(visual.ObjectId, editor.SelectedAttachedSymbolId);
            editor.Dispose();
            Assert.AreEqual(0, editor.AttachedSymbolVisuals.Count);
        });
    }

    [TestMethod]
    public void MixedTextBalloonAndAttachedSymbolOrderRemainsCanonical()
    {
        var page = new PageDocument("z-order", new[] { new MojiData { FullText = "A" } });
        var balloon = new BalloonData { Bounds = new Rect(0, 0, 100, 60) };
        page.AddBalloon(balloon);
        var symbol = new AttachedSymbolData
        {
            ParentId = page.MojiDatas[0].ObjectId,
            GraphemeAnchor = 0,
            Text = "!"
        };
        page.AddAttachedSymbol(symbol);
        CollectionAssert.AreEqual(new[] { page.MojiDatas[0].ObjectId, balloon.ObjectId, symbol.ObjectId },
            page.AllObjects.Select(item => item.ObjectId).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, page.AllObjects.Select(item => item.ZIndex).ToArray());
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) Assert.Fail(failure.ToString());
    }
}
