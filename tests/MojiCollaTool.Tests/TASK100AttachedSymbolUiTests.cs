using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class TASK100AttachedSymbolUiTests
{
    public TestContext TestContext { get; set; } = null!;

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
    public void SelectionRestoreIsMutuallyExclusiveAcrossTextSymbolBalloonNullInvalidAndPages()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("selection-exclusive");
            var first = document.Pages[0];
            var text = new MojiData { FullText = "AB" };
            first.AddMojiData(text);
            var balloon = new BalloonData { Bounds = new Rect(0, 0, 100, 60) };
            first.AddBalloon(balloon);
            var symbol = new AttachedSymbolData { ParentId = text.ObjectId, GraphemeAnchor = 0, Text = "!" };
            first.AddAttachedSymbol(symbol);
            var second = document.AddPage("second");
            second.AddMojiData(new MojiData { FullText = "C" });
            using var session = new ProjectSession(document);
            using var editor = new PageEditorControl();
            editor.BindPage(first, null);

            editor.RestoreViewState(100, symbol.ObjectId);
            Assert.AreEqual(symbol.ObjectId, editor.SelectedObjectId);
            Assert.AreEqual(symbol.ObjectId, editor.SelectedAttachedSymbolId);
            Assert.IsNull(editor.SelectedBalloonId);

            editor.RestoreViewState(100, text.ObjectId);
            Assert.AreEqual(text.ObjectId, editor.SelectedObjectId);
            Assert.IsNull(editor.SelectedAttachedSymbolId);
            Assert.IsNull(editor.SelectedBalloonId);

            editor.RestoreViewState(100, symbol.ObjectId);
            editor.RestoreViewState(100, balloon.ObjectId);
            Assert.AreEqual(balloon.ObjectId, editor.SelectedObjectId);
            Assert.IsNull(editor.SelectedAttachedSymbolId);
            Assert.AreEqual(balloon.ObjectId, editor.SelectedBalloonId);

            editor.RestoreViewState(100, null);
            Assert.IsNull(editor.SelectedObjectId);
            Assert.IsNull(editor.SelectedAttachedSymbolId);
            Assert.IsNull(editor.SelectedBalloonId);
            editor.RestoreViewState(100, Guid.NewGuid());
            Assert.IsNull(editor.SelectedObjectId);
            Assert.IsNull(editor.SelectedAttachedSymbolId);
            Assert.IsNull(editor.SelectedBalloonId);

            editor.RestoreViewState(100, balloon.ObjectId);
            session.ActivatePage(second.PageId);
            editor.BindPage(second, null);
            Assert.IsNull(editor.SelectedObjectId);
            session.ActivatePage(first.PageId);
            editor.BindPage(first, null);
            Assert.AreEqual(balloon.ObjectId, editor.SelectedObjectId);
            Assert.IsNull(editor.SelectedAttachedSymbolId);
            Assert.AreEqual(balloon.ObjectId, editor.SelectedBalloonId);
        });
    }

    [TestMethod]
    public void AttachedSymbolUiRejectsInvalidInputWithoutChangingDocumentVisualSelectionHistoryOrDirty()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("ui-validation");
            var page = document.Pages[0];
            page.AddMojiData(new MojiData { FullText = "AB" });
            using var session = new ProjectSession(document);
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            var valid = editor.AddAttachedSymbol(editor.MojiPanels[0].MojiData.ObjectId, 0, "!");
            editor.CapturePage();
            editor.RestoreViewState(100, valid.ObjectId);
            var window = editor.MojiPanels[0].MojiWindow!;
            Assert.AreEqual(256, ((TextBox)window.FindName("AttachedSymbolTextBox")).MaxLength);

            var beforeDocument = page.AttachedSymbols.Select(item => item.Clone()).ToArray();
            var beforeVisualCount = editor.AttachedSymbolVisuals.Count;
            var beforeSelected = editor.SelectedObjectId;
            var beforeUndo = session.UndoCount;
            var beforeRevision = session.CurrentRevision;
            var beforeDirty = session.IsDirty;

            Assert.IsFalse(window.TryAddAttachedSymbolFromUi(0, string.Empty, out var emptyError));
            Assert.IsTrue(emptyError.Contains("入力してください", StringComparison.Ordinal));
            Assert.AreEqual("!", valid.SymbolData.Text);
            Assert.IsFalse(window.TryAddAttachedSymbolFromUi(0, new string('x', 257), out var longError));
            Assert.IsTrue(longError.Contains("256文字以内", StringComparison.Ordinal));
            Assert.IsFalse(window.TryAddAttachedSymbolFromUi(99, "?", out var anchorError));
            Assert.IsTrue(anchorError.Contains("範囲外", StringComparison.Ordinal));

            var orphan = new MojiPanel(new MojiData { FullText = "A" }, editor);
            var orphanWindow = orphan.MojiWindow!;
            Assert.IsFalse(orphanWindow.TryAddAttachedSymbolFromUi(0, "?", out var parentError));
            Assert.IsTrue(parentError.Contains("親文字", StringComparison.Ordinal));
            orphan.Dispose();

            CollectionAssert.AreEqual(beforeDocument.Select(item => item.ObjectId).ToArray(),
                page.AttachedSymbols.Select(item => item.ObjectId).ToArray());
            Assert.AreEqual(beforeVisualCount, editor.AttachedSymbolVisuals.Count);
            Assert.AreEqual(beforeSelected, editor.SelectedObjectId);
            Assert.AreEqual(beforeUndo, session.UndoCount);
            Assert.AreEqual(beforeRevision, session.CurrentRevision);
            Assert.AreEqual(beforeDirty, session.IsDirty);
        });
    }

    [TestMethod]
    public void AttachedSymbolSettingsScrollViewerHasFiniteViewportAndScrollableContent()
    {
        RunOnSta(() =>
        {
            using var editor = new PageEditorControl();
            var page = new PageDocument("scroll", new[] { new MojiData { FullText = "A" } });
            editor.BindPage(page, null);
            var window = editor.MojiPanels[0].MojiWindow!;
            var root = (Grid)window.Content;
            root.Measure(new Size(800, 900));
            root.Arrange(new Rect(0, 0, 800, 900));
            root.UpdateLayout();
            var scrollViewer = window.AttachedSymbolSettingsViewer;
            Assert.IsTrue(scrollViewer.ViewportHeight > 0);
            Assert.IsTrue(scrollViewer.ActualHeight <= 410.1);
            Assert.IsTrue(scrollViewer.ScrollableHeight > 0,
                $"viewport={scrollViewer.ViewportHeight}; extent={scrollViewer.ExtentHeight}; actual={scrollViewer.ActualHeight}");

            var expandedHeight = root.RowDefinitions[2].ActualHeight;
            Assert.IsTrue(expandedHeight > 100);
            window.AttachedSymbolSettingsExpander.IsExpanded = false;
            root.Measure(new Size(800, 900));
            root.Arrange(new Rect(0, 0, 800, 900));
            root.UpdateLayout();
            Assert.IsTrue(root.RowDefinitions[2].ActualHeight < 100);
            Assert.IsTrue(root.RowDefinitions[1].ActualHeight > 500);
        });
    }

    [TestMethod]
    public void AttachedSymbolWindowUsesParentFilteredTopLevelVisualsAndUiCrudIsUndoable()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("ui-list");
            var page = document.Pages[0];
            var first = new MojiData { FullText = "A" };
            var second = new MojiData { FullText = "B" };
            page.AddMojiData(first);
            page.AddMojiData(second);
            using var session = new ProjectSession(document);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(session.ActivePage!.PageId, editor.ContentChangeDescription,
                    editor.ContentChangeCoalesceKey);
            };

            var firstPanel = editor.MojiPanels.Single(panel => panel.MojiData.ObjectId == first.ObjectId);
            var firstWindow = firstPanel.MojiWindow!;
            Assert.IsTrue(firstWindow.TryAddAttachedSymbolFromUi(0, "!", out var firstError), firstError);
            var firstList = (ListBox)firstWindow.FindName("AttachedSymbolListBox")!;
            Assert.AreEqual(1, firstList.Items.Count);
            var firstVisual = (AttachedSymbolVisual)firstList.SelectedItem!;
            Assert.AreEqual(firstVisual.ObjectId, editor.SelectedAttachedSymbolId);

            editor.HandleMojiListItemClickFromUi(firstPanel);
            Assert.AreEqual(first.ObjectId, editor.SelectedObjectId);
            Assert.IsNull(editor.SelectedAttachedSymbolId);
            firstList.SelectedItem = firstVisual;
            firstWindow.HandleAttachedSymbolListItemClickFromUi();
            Assert.AreEqual(firstVisual.ObjectId, editor.SelectedAttachedSymbolId);

            var offsetTextBox = (TextBox)firstWindow.FindName("AttachedSymbolOffsetXTextBox")!;
            offsetTextBox.Text = "0.75";
            Assert.AreEqual(0.75, firstVisual.SymbolData.OffsetX, 0.000001);

            var secondPanel = editor.MojiPanels.Single(panel => panel.MojiData.ObjectId == second.ObjectId);
            var secondWindow = secondPanel.MojiWindow!;
            Assert.IsTrue(secondWindow.TryAddAttachedSymbolFromUi(0, "?", out var secondError), secondError);
            var secondList = (ListBox)secondWindow.FindName("AttachedSymbolListBox")!;
            Assert.AreEqual(1, secondList.Items.Count);
            Assert.AreNotEqual(firstVisual.ObjectId, ((AttachedSymbolVisual)secondList.SelectedItem!).ObjectId);
            Assert.IsTrue(firstList.Items.Cast<AttachedSymbolVisual>().All(item => item.SymbolData.ParentId == first.ObjectId));

            firstList.SelectedItem = firstVisual;
            ((Button)firstWindow.FindName("AttachedSymbolRemoveButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(0, firstList.Items.Count);
            Assert.IsFalse(editor.AttachedSymbolVisuals.Any(item => item.ObjectId == firstVisual.ObjectId));
            Assert.IsTrue(session.ActivePage!.AttachedSymbols.All(item => item.ParentId != first.ObjectId));

            Assert.IsTrue(session.Undo());
            editor.BindPage(session.ActivePage!, null);
            firstPanel = editor.MojiPanels.Single(panel => panel.MojiData.ObjectId == first.ObjectId);
            firstList = (ListBox)firstPanel.MojiWindow!.FindName("AttachedSymbolListBox")!;
            Assert.AreEqual(1, firstList.Items.Count);
            Assert.IsTrue(session.Redo());
            editor.BindPage(session.ActivePage!, null);
            firstPanel = editor.MojiPanels.Single(panel => panel.MojiData.ObjectId == first.ObjectId);
            Assert.AreEqual(0, ((ListBox)firstPanel.MojiWindow!.FindName("AttachedSymbolListBox")!).Items.Count);
        });
    }

    [TestMethod]
    public void UiAddedSymbolGetsCanonicalZIndexBeforeRebindAndUndoRedo()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("ui-z");
            var page = document.Pages[0];
            var parent = new MojiData { FullText = "A", X = 0, Y = 0 };
            page.AddMojiData(parent);
            page.AddBalloon(new BalloonData { X = 0, Y = 0, Bounds = new Rect(0, 0, 100, 60) });
            using var session = new ProjectSession(document);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(session.ActivePage!.PageId, editor.ContentChangeDescription,
                    editor.ContentChangeCoalesceKey);
            };

            var panel = editor.MojiPanels.Single(item => item.MojiData.ObjectId == parent.ObjectId);
            Assert.IsTrue(panel.MojiWindow!.TryAddAttachedSymbolFromUi(0, "!", out var error), error);
            var symbol = editor.AttachedSymbolVisuals.Single();
            var rendered = editor.Canvas.Children.Cast<UIElement>()
                .Where(item => item is MojiPanel || item is BalloonVisual || item is AttachedSymbolVisual)
                .ToArray();
            CollectionAssert.AreEqual(page.AllObjects.Select(item => item.ObjectId).ToArray(), rendered.Select(item => item switch
            {
                MojiPanel text => text.MojiData.ObjectId,
                BalloonVisual balloon => balloon.ObjectId,
                AttachedSymbolVisual attached => attached.ObjectId,
                _ => Guid.Empty
            }).ToArray());
            Assert.AreEqual(page.AllObjects.Single(item => item.ObjectId == symbol.ObjectId).ZIndex, Canvas.GetZIndex(symbol));

            Assert.IsTrue(session.Undo());
            editor.BindPage(session.ActivePage!, null);
            Assert.AreEqual(0, editor.AttachedSymbolVisuals.Count);
            Assert.IsTrue(session.Redo());
            editor.BindPage(session.ActivePage!, null);
            symbol = editor.AttachedSymbolVisuals.Single();
            Assert.AreEqual(session.ActivePage!.AllObjects.Single(item => item.ObjectId == symbol.ObjectId).ZIndex,
                Canvas.GetZIndex(symbol));
        });
    }

    [TestMethod]
    public void ParentRotationComposesOffsetAndDragInLocalCoordinatesWithUndoRedo()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("rotation-offset");
            var page = document.Pages[0];
            var parentData = new MojiData { FullText = "A", FontSize = 40 };
            page.AddMojiData(parentData);
            using var session = new ProjectSession(document);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(session.ActivePage!.PageId, editor.ContentChangeDescription,
                    editor.ContentChangeCoalesceKey);
            };

            var parent = editor.MojiPanels.Single(item => item.MojiData.ObjectId == parentData.ObjectId);
            var visual = editor.AddAttachedSymbol(parentData.ObjectId, 0, "!");
            editor.UpdateAttachedSymbol(visual.ObjectId, data =>
            {
                data.OffsetX = 0;
                data.OffsetY = 0;
                data.Rotation = 30;
            });
            parent.MojiData.RotateAngle = 90;
            parent.UpdateMojiView(true);
            editor.NotifyContentChanged("親回転", parentData.ObjectId.ToString("D"));
            session.MarkSaved();

            var zeroLeft = Canvas.GetLeft(visual);
            var zeroTop = Canvas.GetTop(visual);
            editor.UpdateAttachedSymbol(visual.ObjectId, data => data.OffsetX = 1);
            var deltaX = Canvas.GetLeft(visual) - zeroLeft;
            var deltaY = Canvas.GetTop(visual) - zeroTop;
            Assert.AreEqual(0, deltaX, 0.0001);
            Assert.AreEqual(parentData.FontSize, deltaY, 0.0001);
            var matrix = visual.RenderTransform.Value;
            var composedAngle = Math.Atan2(matrix.M12, matrix.M11) * 180 / Math.PI;
            if (composedAngle < 0) composedAngle += 360;
            Assert.AreEqual(120, composedAngle, 0.0001);

            editor.UpdateAttachedSymbol(visual.ObjectId, data => data.OffsetX = 0);
            var em = parentData.FontSize;
            Assert.IsTrue(editor.BeginAttachedSymbolGesture(visual.ObjectId, new Point(0, 0)));
            Assert.IsTrue(editor.UpdateAttachedSymbolGesture(new Point(em, 0)));
            Assert.IsTrue(editor.CommitAttachedSymbolGesture(new Point(em, 0)));
            Assert.AreEqual(0, visual.SymbolData.OffsetX, 0.0001);
            Assert.AreEqual(-1, visual.SymbolData.OffsetY, 0.0001);

            Assert.IsTrue(session.Undo());
            editor.BindPage(session.ActivePage!, null);
            visual = editor.AttachedSymbolVisuals.Single();
            Assert.AreEqual(0, visual.SymbolData.OffsetY, 0.0001);
            Assert.IsTrue(session.Redo());
            editor.BindPage(session.ActivePage!, null);
            Assert.AreEqual(-1, editor.AttachedSymbolVisuals.Single().SymbolData.OffsetY, 0.0001);
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

    [TestMethod]
    public void MixedCanonicalZOrderIsReflectedByRenderedVisualHierarchy()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("visual-z-order");
            var page = document.Pages[0];
            var firstText = new MojiData { FullText = "A", X = 0, Y = 0 };
            page.AddMojiData(firstText);
            var firstTextId = firstText.ObjectId;
            var balloon = new BalloonData { X = 0, Y = 0, Bounds = new Rect(0, 0, 100, 60) };
            page.AddBalloon(balloon);
            var symbol = new AttachedSymbolData { ParentId = firstTextId, GraphemeAnchor = 0, Text = "!" };
            page.AddAttachedSymbol(symbol);
            var secondText = new MojiData { FullText = "B", X = 0, Y = 0 };
            page.AddMojiData(secondText);

            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            static UIElement[] RenderedObjects(PageEditorControl pageEditor) => pageEditor.Canvas.Children.Cast<UIElement>()
                .Where(child => child is MojiPanel || child is BalloonVisual || child is AttachedSymbolVisual)
                .ToArray();
            var rendered = RenderedObjects(editor);
            var renderedIds = rendered.Select(child => child switch
            {
                MojiPanel panel => panel.MojiData.ObjectId,
                BalloonVisual visual => visual.ObjectId,
                AttachedSymbolVisual visual => visual.ObjectId,
                _ => Guid.Empty
            }).ToArray();
            CollectionAssert.AreEqual(page.AllObjects.Select(item => item.ObjectId).ToArray(), renderedIds);
            CollectionAssert.AreEqual(page.AllObjects.Select(item => item.ZIndex).ToArray(),
                rendered.Select(Canvas.GetZIndex).ToArray());
            Assert.AreEqual(symbol.ObjectId, ((AttachedSymbolVisual)rendered.Single(item => item is AttachedSymbolVisual)).ObjectId);

            var path = Path.Combine(Path.GetTempPath(), $"task100-z-{Guid.NewGuid():N}.mctzip");
            try
            {
                DataIO.WriteVersionedProject(path, document);
                var restored = DataIO.ReadVersionedProject(path);
                using var reloadedEditor = new PageEditorControl();
                reloadedEditor.BindPage(restored.Pages[0], null);
                var reloaded = RenderedObjects(reloadedEditor);
                CollectionAssert.AreEqual(renderedIds, reloaded.Select(child => child switch
                {
                    MojiPanel panel => panel.MojiData.ObjectId,
                    BalloonVisual visual => visual.ObjectId,
                    AttachedSymbolVisual visual => visual.ObjectId,
                    _ => Guid.Empty
                }).ToArray());
                CollectionAssert.AreEqual(page.AllObjects.Select(item => item.ZIndex).ToArray(),
                    reloaded.Select(Canvas.GetZIndex).ToArray());
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        });
    }

    [TestMethod]
    public void TwoRecognizedFontsHaveFiniteAdjustableHorizontalAndVerticalPlacement()
    {
        RunOnSta(() =>
        {
            var fontNames = FontUtil.GetFontFamilies().Keys.Take(2).ToArray();
            Assert.AreEqual(2, fontNames.Length, "The WPF test environment must expose at least two system fonts.");
            var page = new PageDocument("fonts", new[]
            {
                new MojiData { FullText = "AB", FontFamilyName = fontNames[0] }
            });
            var symbol = new AttachedSymbolData
            {
                ParentId = page.MojiDatas[0].ObjectId,
                GraphemeAnchor = 0,
                Text = "!?",
                FontFamilyName = fontNames[1]
            };
            page.AddAttachedSymbol(symbol);
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            var parent = editor.MojiPanels[0];
            var visual = editor.AttachedSymbolVisuals.Single();
            foreach (var direction in new[] { TextDirection.Yokogaki, TextDirection.Tategaki })
            {
                parent.MojiData.TextDirection = direction;
                parent.UpdateMojiView(true);
                Assert.IsFalse(visual.IsFontFallback);
                Assert.IsTrue(IsFinite(Canvas.GetLeft(visual)) && IsFinite(Canvas.GetTop(visual)));
                Assert.IsTrue(IsFinite(visual.AnchorBounds.Left) && IsFinite(visual.AnchorBounds.Top));
                var before = Canvas.GetLeft(visual);
                editor.UpdateAttachedSymbol(visual.ObjectId, data => data.OffsetX += 0.5);
                Assert.AreNotEqual(before, Canvas.GetLeft(visual));
            }
        });
    }

    [TestMethod]
    public void UiAddRemovePropertyAndDragHaveUndoRedoSavedDirtyAndRedoBranchSemantics()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("ui-history");
            var page = document.Pages[0];
            page.AddMojiData(new MojiData { FullText = "AB" });
            using var session = new ProjectSession(document);
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };

            var parentId = editor.MojiPanels[0].MojiData.ObjectId;
            var visual = editor.AddAttachedSymbol(parentId, 0, "!");
            Assert.IsTrue(session.IsDirty);
            session.MarkSaved();
            Assert.IsFalse(session.IsDirty);
            var initialOffset = visual.SymbolData.OffsetX;

            editor.UpdateAttachedSymbol(visual.ObjectId, data => data.OffsetX = initialOffset + 0.25);
            Assert.IsTrue(session.IsDirty);
            Assert.IsTrue(session.Undo());
            editor.BindPage(session.ActivePage!, null);
            TestContext.WriteLine($"property-undo expected={initialOffset:F6}; page={session.ActivePage!.AttachedSymbols.Single().OffsetX:F6}; visual={editor.AttachedSymbolVisuals.Single().SymbolData.OffsetX:F6}");
            Assert.AreEqual(initialOffset, editor.AttachedSymbolVisuals.Single().SymbolData.OffsetX);
            Assert.IsFalse(session.IsDirty);
            Assert.IsTrue(session.Redo());
            editor.BindPage(session.ActivePage!, null);
            Assert.IsTrue(session.IsDirty);

            visual = editor.AttachedSymbolVisuals.Single();
            Assert.IsTrue(editor.BeginAttachedSymbolGesture(visual.ObjectId, new Point(0, 0)));
            editor.UpdateAttachedSymbolGesture(new Point(12, 0));
            Assert.IsTrue(editor.CommitAttachedSymbolGesture(new Point(12, 0)));
            visual = editor.AttachedSymbolVisuals.Single();
            Assert.IsTrue(session.Undo());
            editor.BindPage(session.ActivePage!, null);
            Assert.IsTrue(session.CanRedo);

            visual = editor.AttachedSymbolVisuals.Single();
            Assert.IsTrue(editor.RemoveAttachedSymbol(visual.ObjectId));
            Assert.IsFalse(session.CanRedo, "A UI remove must discard the undone drag redo branch.");
            Assert.IsTrue(session.Undo());
            editor.BindPage(session.ActivePage!, null);
            Assert.AreEqual(1, editor.AttachedSymbolVisuals.Count);
            Assert.IsTrue(session.Redo());
            editor.BindPage(session.ActivePage!, null);
            Assert.AreEqual(0, editor.AttachedSymbolVisuals.Count);
        });
    }

    [TestMethod]
    public void MultipleAttachedSymbolsRefreshAndDragPerformanceIsMeasured()
    {
        string measurement = string.Empty;
        RunOnSta(() =>
        {
            var page = new PageDocument("symbol-performance", new[] { new MojiData { FullText = "A" } });
            var parentId = page.MojiDatas[0].ObjectId;
            for (var i = 0; i < 24; i++)
            {
                page.AddAttachedSymbol(new AttachedSymbolData
                {
                    ParentId = parentId,
                    GraphemeAnchor = 0,
                    Text = i % 2 == 0 ? "!?" : "\u3099"
                });
            }
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            var panel = editor.MojiPanels[0];

            var refreshWatch = Stopwatch.StartNew();
            for (var i = 0; i < 50; i++) panel.UpdateMojiView(false);
            refreshWatch.Stop();

            var visual = editor.AttachedSymbolVisuals[0];
            var dragWatch = Stopwatch.StartNew();
            for (var i = 0; i < 25; i++)
            {
                Assert.IsTrue(editor.BeginAttachedSymbolGesture(visual.ObjectId, new Point(0, 0)));
                editor.UpdateAttachedSymbolGesture(new Point(i + 1, 0));
                editor.CommitAttachedSymbolGesture(new Point(i + 1, 0));
            }
            dragWatch.Stop();
            measurement = $"attached-symbols={editor.AttachedSymbolVisuals.Count}; refresh-50-ms={refreshWatch.Elapsed.TotalMilliseconds:F3}; drag-25-ms={dragWatch.Elapsed.TotalMilliseconds:F3}";
            Assert.IsTrue(refreshWatch.Elapsed < TimeSpan.FromSeconds(5));
            Assert.IsTrue(dragWatch.Elapsed < TimeSpan.FromSeconds(5));
        });
        TestContext.WriteLine(measurement);
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

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
