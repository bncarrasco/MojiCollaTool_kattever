using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class TASK140AutoTextLayoutTests
{
    [TestMethod]
    public void BasicWrapKeepsFullTextAndGraphemeClustersIntact()
    {
        var fullText = "A😀e\u0301👩\u200d💻\r\n日本語";
        var request = new TextLayoutRequest
        {
            FullText = fullText,
            Direction = TextDirection.Yokogaki,
            FrameWidth = 55,
            FrameHeight = 240,
            FontSize = 28,
            MinimumFontSize = 8,
            Padding = 2,
        };

        var result = new TextLayoutService().FitTextToBalloon(request);
        var renderedText = string.Concat(result.Lines.Select(line => line.Text));
        var sourceWithoutBreaks = fullText.Replace("\r\n", string.Empty, StringComparison.Ordinal);

        Assert.AreEqual(sourceWithoutBreaks, renderedText);
        Assert.IsTrue(result.Lines.Any(line => line.IsExplicitBreak));
        CollectionAssert.AreEqual(
            GraphemeService.Segment(fullText).Where(cluster => cluster.Text != "\r\n").Select(cluster => cluster.Text).ToArray(),
            result.Lines.SelectMany(line => line.Clusters).Select(cluster => cluster.Text).ToArray());
        Assert.AreEqual(fullText, request.FullText);
    }

    [TestMethod]
    public void VerticalWrapAndMinimumFontSizeProduceFiniteDeterministicResult()
    {
        var request = new TextLayoutRequest
        {
            FullText = "縦書き 😀😀😀😀😀",
            Direction = TextDirection.Tategaki,
            FrameWidth = 60,
            FrameHeight = 34,
            FontSize = 30,
            MinimumFontSize = 10,
            Padding = 4,
        };
        var service = new TextLayoutService(cacheCapacity: 32);

        var first = service.FitTextToBalloon(request);
        var second = service.FitTextToBalloon(request);

        Assert.IsTrue(first.EffectiveFontSize >= request.MinimumFontSize);
        Assert.IsTrue(first.EffectiveFontSize <= request.FontSize);
        Assert.IsTrue(first.ContentWidth >= 0 && first.ContentHeight >= 0);
        Assert.AreEqual(first.EffectiveFontSize, second.EffectiveFontSize);
        Assert.AreEqual(first.ContentWidth, second.ContentWidth);
        Assert.IsTrue(service.MeasureCacheCount <= 32);
    }

    [TestMethod]
    public void ExplicitFitIsOneHistoryEntryAndUndoRedoRestoresBothSides()
    {
        RunOnSta(() =>
        {
            var text = new MojiData { Id = 1, FullText = "これは長い本文です。😀", FontSize = 32 };
            var balloon = new BalloonData
            {
                X = 40,
                Y = 50,
                Bounds = new Rect(0, 0, 40, 30),
                TextLink = new TextLinkData
                {
                    TextObjectId = text.ObjectId,
                    LayoutMode = BalloonTextLayoutMode.FitTextToBalloon,
                    MinimumFontSize = 8,
                    Padding = 2,
                },
            };
            var page = new PageDocument("01", new[] { text }, new[] { balloon });
            using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "layout", new[] { page }));
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.RestoreViewState(100, balloon.ObjectId);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };

            var liveText = editor.MojiPanels.Single().MojiData;
            Assert.IsTrue(editor.FitTextToBalloon());
            Assert.AreEqual(1, session.UndoCount);
            Assert.AreEqual(text.FullText, liveText.FullText);
            Assert.IsTrue(liveText.FontSize <= text.FontSize);
            Assert.IsTrue(editor.MojiPanels.Single().ComputedLayout != null);

            Assert.IsTrue(session.Undo());
            Assert.AreEqual(text.FontSize, session.ActivePage!.MojiDatas.Single().FontSize);
            Assert.AreEqual(balloon.Bounds, session.ActivePage.Balloons.Single().Bounds);
            Assert.IsTrue(session.Redo());
            Assert.AreEqual(liveText.FontSize, session.ActivePage!.MojiDatas.Single().FontSize);
        });
    }

    [TestMethod]
    public void LifecycleRebuildsPlanAcrossSaveReloadPageSwitchUndoRedoAndPreservesText()
    {
        RunOnSta(() =>
        {
            var text = new MojiData
            {
                FullText = "保存\r\n再読込と折返しを確認する長い本文",
                X = 12,
                Y = 18,
                FontSize = 32,
                FontFamilyName = "Segoe UI",
            };
            var balloon = new BalloonData
            {
                X = 40,
                Y = 50,
                Bounds = new Rect(0, 0, 78, 44),
                TextLink = new TextLinkData
                {
                    TextObjectId = text.ObjectId,
                    LayoutMode = BalloonTextLayoutMode.FitTextToBalloon,
                    Padding = 4,
                    MinimumFontSize = 10,
                    Alignment = BalloonTextAlignment.End,
                },
            };
            var first = new PageDocument("01", new[] { text }, new[] { balloon });
            var second = new PageDocument("02", new[] { new MojiData { FullText = "別ページ" } });
            var document = new ProjectDocument(Guid.NewGuid(), "lifecycle", new[] { first, second });
            using var session = new ProjectSession(document);
            using var editor = new PageEditorControl();
            editor.BindPage(first, null);
            editor.RestoreViewState(100, balloon.ObjectId);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(first.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };

            Assert.IsTrue(editor.FitTextToBalloon());
            var appliedText = editor.MojiPanels.Single().MojiData.Clone();
            var appliedLink = editor.BalloonVisuals.Single().BalloonData.TextLink!.Clone();
            Assert.IsNotNull(editor.MojiPanels.Single().ComputedLayout);
            session.MarkSaved();

            editor.ReloadBoundPage();
            Assert.IsNotNull(editor.MojiPanels.Single().ComputedLayout);
            Assert.AreEqual(appliedText.FullText, editor.MojiPanels.Single().MojiData.FullText);
            Assert.AreEqual(appliedLink.Alignment, editor.BalloonVisuals.Single().BalloonData.TextLink!.Alignment);

            session.ActivatePage(second.PageId);
            editor.BindPage(second, null);
            session.ActivatePage(first.PageId);
            editor.BindPage(first, null);
            Assert.IsNotNull(editor.MojiPanels.Single().ComputedLayout);

            Assert.IsTrue(session.Undo());
            editor.ReloadBoundPage();
            Assert.IsNotNull(editor.MojiPanels.Single().ComputedLayout);
            Assert.IsTrue(session.IsDirty);
            Assert.IsTrue(session.Redo());
            editor.ReloadBoundPage();
            Assert.IsNotNull(editor.MojiPanels.Single().ComputedLayout);
            Assert.IsFalse(session.IsDirty);

            using var scope = TemporaryDirectory.Create();
            var path = Path.Combine(scope.Path, "layout.mctzip");
            DataIO.WriteVersionedProject(path, session.Document);
            var restored = DataIO.ReadVersionedProject(path);
            editor.BindPage(restored.Pages.Single(page => page.PageId == first.PageId), null);
            Assert.IsNotNull(editor.MojiPanels.Single().ComputedLayout);
            Assert.AreEqual(appliedText.FullText, editor.MojiPanels.Single().MojiData.FullText);
        });
    }

    [TestMethod]
    public void FitBalloonToTextChangesOnlyBalloonAndIsNoOpOnSecondApply()
    {
        RunOnSta(() =>
        {
            var text = new MojiData
            {
                FullText = "固定側の本文と\n付加記号",
                X = 15,
                Y = 21,
                FontSize = 26,
                IsLocked = true,
                IsVisible = false,
            };
            var symbol = new AttachedSymbolData
            {
                ParentId = text.ObjectId,
                GraphemeAnchor = 1,
                AnchorText = "定",
                Text = "!",
                OffsetX = 0.25,
                OffsetY = -0.5,
                Scale = 1.2,
            };
            var balloon = new BalloonData
            {
                X = 80,
                Y = 90,
                Bounds = new Rect(0, 0, 42, 32),
                Rotation = 13,
                Tail = new BalloonTailData { TipX = 180, TipY = 210, RootParameter = 0.3, Width = 18 },
                TextLink = new TextLinkData
                {
                    TextObjectId = text.ObjectId,
                    LayoutMode = BalloonTextLayoutMode.FitBalloonToText,
                    Padding = 3,
                    MinimumFontSize = 8,
                },
            };
            var page = new PageDocument("01", new[] { text }, new[] { balloon });
            page.AddAttachedSymbol(symbol);
            using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "balloon-fit", new[] { page }));
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.RestoreViewState(100, balloon.ObjectId);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };

            var liveText = editor.MojiPanels.Single().MojiData;
            var liveSymbol = editor.AttachedSymbolVisuals.Single().SymbolData.Clone();
            var beforeText = liveText.Clone();
            var beforeTail = editor.BalloonVisuals.Single().BalloonData.Tail!.Clone();
            Assert.IsTrue(editor.FitBalloonToText());
            Assert.AreEqual(1, session.UndoCount);
            Assert.AreEqual(beforeText.FullText, liveText.FullText);
            Assert.AreEqual(beforeText.FontSize, liveText.FontSize);
            Assert.AreEqual(beforeText.X, liveText.X);
            Assert.AreEqual(beforeText.Y, liveText.Y);
            Assert.AreEqual(beforeTail.TipX, balloon.Tail!.TipX);
            Assert.AreEqual(beforeTail.TipY, balloon.Tail.TipY);
            var afterFirst = editor.BalloonVisuals.Single().BalloonData.Clone();
            var afterSymbol = editor.AttachedSymbolVisuals.Single().SymbolData;
            Assert.AreEqual(liveSymbol.OffsetX, afterSymbol.OffsetX);
            Assert.AreEqual(liveSymbol.OffsetY, afterSymbol.OffsetY);
            Assert.AreEqual(liveSymbol.Scale, afterSymbol.Scale);
            Assert.IsNotNull(editor.MojiPanels.Single().ComputedLayout);

            Assert.IsTrue(editor.FitBalloonToText());
            Assert.AreEqual(1, session.UndoCount);
            Assert.AreEqual(afterFirst.Bounds, editor.BalloonVisuals.Single().BalloonData.Bounds);
            Assert.IsTrue(session.Undo());
            Assert.IsTrue(session.Redo());
            editor.ReloadBoundPage();
            Assert.IsNotNull(editor.MojiPanels.Single().ComputedLayout);
        });
    }

    [TestMethod]
    public void LayoutUiRejectsBadNumbersWithoutMutationAndCommitsSettingsWithOneHistoryEntry()
    {
        RunOnSta(() =>
        {
            var text = new MojiData { FullText = "UI入力値を検証", FontSize = 30 };
            var balloon = new BalloonData
            {
                Bounds = new Rect(0, 0, 100, 60),
                TextLink = new TextLinkData { TextObjectId = text.ObjectId, Padding = 2, MinimumFontSize = 8 },
            };
            var page = new PageDocument("01", new[] { text }, new[] { balloon });
            using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "ui", new[] { page }));
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.RestoreViewState(100, balloon.ObjectId);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };

            var padding = PrivateTextBox(editor, "TextLayoutPaddingTextBox");
            var minimum = PrivateTextBox(editor, "TextLayoutMinimumFontSizeTextBox");
            var beforeText = editor.MojiPanels.Single().MojiData.Clone();
            var beforeBalloon = editor.BalloonVisuals.Single().BalloonData.Clone();
            var beforeRevision = session.CurrentRevision;
            var beforeUndo = session.UndoCount;
            var beforeDirty = session.IsDirty;

            padding.Text = "NaN";
            Assert.IsFalse(editor.FitTextToBalloon());
            AssertStateUnchanged(editor, beforeText, beforeBalloon, session, beforeRevision, beforeUndo, beforeDirty);
            var nanStatus = PrivateStatus(editor).Text;
            padding.Text = "2";
            minimum.Text = "0";
            Assert.IsFalse(editor.FitTextToBalloon());
            Assert.AreNotEqual(nanStatus, PrivateStatus(editor).Text);
            minimum.Text = "8";
            padding.Text = "";
            Assert.IsFalse(editor.FitTextToBalloon());
            AssertStateUnchanged(editor, beforeText, beforeBalloon, session, beforeRevision, beforeUndo, beforeDirty);

            padding.Text = "4.5";
            minimum.Text = "12";
            Assert.IsTrue(editor.FitTextToBalloon());
            Assert.AreEqual(1, session.UndoCount);
            Assert.AreEqual(4.5, page.Balloons.Single().TextLink!.Padding, 0.0001);
            Assert.AreEqual(12, page.Balloons.Single().TextLink!.MinimumFontSize, 0.0001);
            Assert.IsTrue(session.IsDirty);
        });
    }

    [TestMethod]
    public void SubscriberFailurePropagatesAfterCommitWithoutRollingBackHistoryOrVisualPlan()
    {
        RunOnSta(() =>
        {
            var text = new MojiData { FullText = "subscriber exception", FontSize = 28 };
            var balloon = new BalloonData
            {
                Bounds = new Rect(0, 0, 80, 45),
                TextLink = new TextLinkData { TextObjectId = text.ObjectId, Padding = 3, MinimumFontSize = 8 },
            };
            var page = new PageDocument("01", new[] { text }, new[] { balloon });
            using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "subscriber", new[] { page }));
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.RestoreViewState(100, balloon.ObjectId);
            EventHandler handler = (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
                throw new InvalidOperationException("subscriber failure");
            };
            editor.ContentChanged += handler;
            try
            {
                Assert.ThrowsException<InvalidOperationException>(() => editor.FitTextToBalloon());
            }
            finally
            {
                editor.ContentChanged -= handler;
            }

            Assert.AreEqual(1, session.UndoCount);
            Assert.IsTrue(session.IsDirty);
            Assert.IsNotNull(editor.MojiPanels.Single().ComputedLayout);
            Assert.AreEqual(page.Balloons.Single().TextLink!.LayoutMode,
                editor.BalloonVisuals.Single().BalloonData.TextLink!.LayoutMode);
        });
    }

    [TestMethod]
    public void LockAndVisibilityApplyOnlyToSelectedLayoutTarget()
    {
        RunOnSta(() =>
        {
            var firstText = new MojiData { FullText = "text target text target text target text target" };
            var firstBalloon = new BalloonData
            {
                Bounds = new Rect(0, 0, 90, 50),
                IsLocked = true,
                IsVisible = false,
                TextLink = new TextLinkData { TextObjectId = firstText.ObjectId, Padding = 2, MinimumFontSize = 8 },
            };
            var page = new PageDocument("01", new[] { firstText }, new[] { firstBalloon });
            using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "target-lock", new[] { page }));
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.RestoreViewState(100, firstBalloon.ObjectId);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };
            Assert.IsTrue(editor.FitTextToBalloon());
            Assert.AreEqual(1, session.UndoCount);

            editor.ReloadBoundPage();
            var liveText = editor.MojiPanels.Single().MojiData;
            var liveBalloon = editor.BalloonVisuals.Single().BalloonData;
            liveText.IsLocked = true;
            liveText.IsVisible = false;
            liveBalloon.IsLocked = false;
            liveBalloon.IsVisible = true;
            liveBalloon.Bounds = new Rect(0, 0, 24, 24);
            Assert.IsTrue(editor.FitBalloonToText());
            Assert.IsTrue(editor.BalloonVisuals.Single().BalloonData.Bounds.Width > 24,
                $"target bounds={editor.BalloonVisuals.Single().BalloonData.Bounds}");
            Assert.AreEqual(2, session.UndoCount);
        });
    }

    [TestMethod]
    public void ServiceOwnsDirectionAlignmentTargetsAndHandlesBreaksInvalidGeometryAndTwoFonts()
    {
        var service = new TextLayoutService(cacheCapacity: 64);
        foreach (var direction in new[] { TextDirection.Yokogaki, TextDirection.Tategaki })
        {
            foreach (var alignment in new[] { BalloonTextAlignment.Start, BalloonTextAlignment.Center, BalloonTextAlignment.End })
            {
                var request = new TextLayoutRequest
                {
                    FullText = "A\rB\nC\r\nD",
                    Direction = direction,
                    FrameX = 10,
                    FrameY = 20,
                    FrameWidth = 120,
                    FrameHeight = 80,
                    TextX = 2,
                    TextY = 3,
                    Padding = 5,
                    FontSize = 20,
                    MinimumFontSize = 8,
                    Alignment = alignment,
                    FontFamilyName = direction == TextDirection.Yokogaki ? "Segoe UI" : "Arial",
                };
                var result = service.LayoutWithinFrame(request);
                Assert.AreEqual(3, result.Lines.Count(line => line.IsExplicitBreak));
                Assert.IsTrue(result.Lines.SelectMany(line => line.Clusters).Any(cluster => cluster.Text == "D"));
                Assert.IsTrue(IsFinite(result.TargetTextPosition.X) && IsFinite(result.TargetTextPosition.Y));
                if (direction == TextDirection.Yokogaki && alignment == BalloonTextAlignment.Start)
                    Assert.AreEqual(15, result.TargetTextPosition.X, 0.001);
                if (direction == TextDirection.Tategaki && alignment == BalloonTextAlignment.End)
                    Assert.AreEqual(20 + 80 - 5 - result.ContentHeight, result.TargetTextPosition.Y, 0.001);
            }
        }

        var invalidGeometry = new TextLayoutRequest
        {
            FullText = "safe",
            FrameWidth = double.NaN,
            FrameHeight = double.PositiveInfinity,
            Padding = double.PositiveInfinity,
            FontSize = double.NaN,
            MinimumFontSize = double.NegativeInfinity,
        };
        var safe = service.FitTextToBalloon(invalidGeometry);
        Assert.IsTrue(IsFinite(safe.EffectiveFontSize));
        Assert.IsTrue(IsFinite(safe.TargetBalloonBounds.Width) && IsFinite(safe.TargetBalloonBounds.Height));
    }

    [TestMethod]
    public void LayoutServiceMeetsSingleAndCompositionBatchSafetyBudgets()
    {
        RunOnSta(() =>
        {
            var service = new TextLayoutService();
            var requests = Enumerable.Range(0, 24).Select(index => new TextLayoutRequest
            {
                FullText = $"性能測定{index}の本文と\r\n明示改行",
                Direction = index % 2 == 0 ? TextDirection.Yokogaki : TextDirection.Tategaki,
                FrameWidth = 180,
                FrameHeight = 120,
                Padding = 5,
                FontSize = 24,
                MinimumFontSize = 8,
                FontFamilyName = index % 2 == 0 ? "Segoe UI" : "Arial",
            }).ToArray();
            foreach (var request in requests) service.FitTextToBalloon(request);
            var samples = Enumerable.Range(0, 20).Select(_ =>
            {
                var watch = Stopwatch.StartNew();
                service.FitTextToBalloon(requests[0]);
                watch.Stop();
                return watch.Elapsed.TotalMilliseconds;
            }).OrderBy(value => value).ToArray();
            var p95 = samples[(int)Math.Ceiling(samples.Length * 0.95) - 1];
            var batchWatch = Stopwatch.StartNew();
            foreach (var request in requests) service.FitTextToBalloon(request);
            batchWatch.Stop();
#if DEBUG
            const double singleBudget = 3000;
            const double batchBudget = 3000;
#else
            const double singleBudget = 100;
            const double batchBudget = 1000;
#endif
            Console.WriteLine($"TASK-140 performance: p95={p95:0.###}ms; batch24={batchWatch.Elapsed.TotalMilliseconds:0.###}ms");
            Assert.IsTrue(p95 < singleBudget, $"p95={p95:0.###}ms budget={singleBudget}ms");
            Assert.IsTrue(batchWatch.Elapsed.TotalMilliseconds < batchBudget,
                $"batch={batchWatch.Elapsed.TotalMilliseconds:0.###}ms budget={batchBudget}ms");
        });
    }

    private static void AssertStateUnchanged(PageEditorControl editor, MojiData beforeText,
        BalloonData beforeBalloon, ProjectSession session, long beforeRevision, int beforeUndo, bool beforeDirty)
    {
        var text = editor.MojiPanels.Single().MojiData;
        var balloon = editor.BalloonVisuals.Single().BalloonData;
        Assert.AreEqual(beforeText.FullText, text.FullText);
        Assert.AreEqual(beforeText.FontSize, text.FontSize);
        Assert.AreEqual(beforeText.X, text.X);
        Assert.AreEqual(beforeText.Y, text.Y);
        Assert.AreEqual(beforeBalloon.Bounds, balloon.Bounds);
        Assert.AreEqual(beforeBalloon.TextLink!.Padding, balloon.TextLink!.Padding);
        Assert.AreEqual(beforeRevision, session.CurrentRevision);
        Assert.AreEqual(beforeUndo, session.UndoCount);
        Assert.AreEqual(beforeDirty, session.IsDirty);
    }

    private static TextBox PrivateTextBox(PageEditorControl editor, string name) =>
        (TextBox)(typeof(PageEditorControl).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(editor)
            ?? throw new AssertFailedException($"Missing private UI field: {name}"));

    private static TextBlock PrivateStatus(PageEditorControl editor) =>
        (TextBlock)(typeof(PageEditorControl).GetField("BalloonStatusTextBlock", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(editor)
            ?? throw new AssertFailedException("Missing status UI field."));

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MojiCollaTask140-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new AssertFailedException(failure.ToString());
    }
}
