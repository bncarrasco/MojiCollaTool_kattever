using System;
using System.Collections.Generic;
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
public class TASK130BalloonTailLinkTests
{
    public TestContext? TestContext { get; set; }

    [TestMethod]
    public void TailGeometrySupportsShapesCardinalRootsWidthAndRotation()
    {
        var shapes = new[]
        {
            BalloonShapeKind.Ellipse,
            BalloonShapeKind.RoundedRectangle,
            BalloonShapeKind.Rectangle,
            BalloonShapeKind.Monologue,
            BalloonShapeKind.Unknown,
        };
        var parameters = new[] { 0.0, 0.25, 0.5, 0.75, 1.0 };
        foreach (var shape in shapes)
        foreach (var parameter in parameters)
        foreach (var rotation in new[] { 0.0, 90.0 })
        foreach (var width in new[] { 0.0, 24.0 })
        {
            var balloon = new BalloonData
            {
                X = 100,
                Y = 80,
                Bounds = new Rect(0, 0, 240, 140),
                ShapeKind = shape,
                Rotation = rotation,
                Tail = new BalloonTailData { TipX = 20, TipY = 310, RootParameter = parameter, Width = width },
            };
            var geometry = BalloonTailGeometry.Create(balloon);
            var placement = BalloonTailGeometry.GetRootPlacement(balloon);

            Assert.IsFalse(geometry.IsEmpty(), $"{shape}/{parameter}/{rotation}/{width}");
            AssertFinite(geometry.Bounds);
            AssertFinite(new Rect(placement.Point, new Size(0, 0)));
            Assert.AreEqual(balloon.Tail.Tip, BalloonTailGeometry.LocalToPage(
                balloon, BalloonTailGeometry.PageToLocal(balloon, balloon.Tail.Tip)));
        }

        var bounds = new Rect(0, 0, 240, 140);
        foreach (var shape in shapes)
        {
            var top = BalloonTailGeometry.GetRootPlacement(shape, bounds, 0).Point;
            var right = BalloonTailGeometry.GetRootPlacement(shape, bounds, .25).Point;
            var bottom = BalloonTailGeometry.GetRootPlacement(shape, bounds, .5).Point;
            var left = BalloonTailGeometry.GetRootPlacement(shape, bounds, .75).Point;
            Assert.IsTrue(top.Y <= bounds.Top + bounds.Height / 2, shape.ToString());
            Assert.IsTrue(right.X >= bounds.Left + bounds.Width / 2, shape.ToString());
            Assert.IsTrue(bottom.Y >= bounds.Top + bounds.Height / 2, shape.ToString());
            Assert.IsTrue(left.X <= bounds.Left + bounds.Width / 2, shape.ToString());
        }
    }

    [TestMethod]
    public void VisualHitTestUsesBodyAndTailGeometryInsteadOfBoundingBox()
    {
        RunOnSta(() =>
        {
            var balloon = new BalloonData
            {
                X = 50,
                Y = 60,
                Bounds = new Rect(0, 0, 200, 100),
                ShapeKind = BalloonShapeKind.Ellipse,
                Tail = new BalloonTailData { TipX = 150, TipY = 220, RootParameter = .5, Width = 30 },
            };
            var visual = new BalloonVisual(balloon);
            var root = BalloonTailGeometry.PageToLocal(balloon, BalloonTailGeometry.GetRootPlacement(balloon).Point);
            var tip = BalloonTailGeometry.PageToLocal(balloon, balloon.Tail.Tip);
            var tailInterior = new Point((root.X + tip.X) / 2, (root.Y + tip.Y) / 2);
            var canvas = new Canvas { Width = 400, Height = 400 };
            canvas.Children.Add(visual);
            canvas.Measure(new Size(400, 400));
            canvas.Arrange(new Rect(0, 0, 400, 400));

            Assert.IsTrue(visual.ContainsLocalPoint(new Point(100, 50)));
            Assert.IsTrue(visual.ContainsLocalPoint(tailInterior));
            Assert.IsFalse(visual.ContainsLocalPoint(new Point(1, 1)));
            var pageTailInterior = BalloonTailGeometry.LocalToPage(balloon, tailInterior);
            Assert.AreSame(visual, VisualTreeHelper.HitTest(canvas, pageTailInterior)?.VisualHit);

            balloon.Rotation = 90;
            visual.Refresh();
            Assert.AreEqual(balloon.Tail.Tip, BalloonTailGeometry.LocalToPage(
                balloon, BalloonTailGeometry.PageToLocal(balloon, balloon.Tail.Tip)));
        });
    }

    [TestMethod]
    public void LinkIntegrityRejectsDuplicatesAndDeletionPoliciesAreUndoable()
    {
        var text = new MojiData { FullText = "リンク対象" };
        var first = new BalloonData();
        var second = new BalloonData();
        var page = new PageDocument("01", new[] { text }, new[] { first, second });
        page.LinkBalloonText(first.ObjectId, text.ObjectId);

        CollectionAssert.AreEqual(new[] { first.ObjectId, text.ObjectId, second.ObjectId },
            page.AllObjects.Select(item => item.ObjectId).ToArray());
        Assert.ThrowsException<InvalidOperationException>(() => page.LinkBalloonText(second.ObjectId, text.ObjectId));
        Assert.ThrowsException<KeyNotFoundException>(() => page.LinkBalloonText(second.ObjectId, Guid.NewGuid()));
        Assert.AreEqual(text.ObjectId, page.GetBalloon(first.ObjectId).TextLink!.TextObjectId);
        Assert.IsNull(page.GetBalloon(second.ObjectId).TextLink);

        var project = new ProjectDocument(Guid.NewGuid(), "project", new[] { page });
        using var session = new ProjectSession(project);
        session.ExecutePage(page.PageId, current => current.RemoveMojiData(current.GetObject(text.ObjectId)), "文字削除");
        Assert.IsNull(session.Document.Pages[0].GetBalloon(first.ObjectId).TextLink);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(text.ObjectId, session.Document.Pages[0].GetBalloon(first.ObjectId).TextLink!.TextObjectId);
        session.ExecutePage(page.PageId, current => current.RemoveBalloon(first.ObjectId), "フキダシ削除");
        Assert.IsTrue(session.Document.Pages[0].ContainsObject(text.ObjectId));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(text.ObjectId, session.Document.Pages[0].GetBalloon(first.ObjectId).TextLink!.TextObjectId);
    }

    [TestMethod]
    public void JapaneseUiAddsRemovesLinksAndReportsSafeErrors()
    {
        RunOnSta(() =>
        {
            var text = new MojiData { Id = 7, FullText = "縦横リンク", TextDirection = TextDirection.Tategaki, FontSize = 43 };
            var balloon = new BalloonData();
            var page = new PageDocument("01", new[] { text }, new[] { balloon });
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.RestoreViewState(100, balloon.ObjectId);
            var notifications = 0;
            editor.ContentChanged += (_, _) => notifications++;

            Assert.AreEqual("しっぽ追加", ((Button)editor.FindName("AddTailButton")!).Content);
            ((Button)editor.FindName("AddTailButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(3, editor.TailHandleVisuals.Count);
            Assert.IsFalse(editor.AddTailToSelectedBalloon());
            Assert.IsTrue(editor.RemoveTailFromSelectedBalloon());
            Assert.IsFalse(editor.RemoveTailFromSelectedBalloon());

            Assert.IsTrue(editor.TryLinkSelectedBalloon(text.ObjectId));
            Assert.AreEqual(text.ObjectId, editor.BalloonVisuals.Single().BalloonData.TextLink!.TextObjectId);
            Assert.AreEqual(TextDirection.Tategaki, editor.MojiPanels.Single().MojiData.TextDirection);
            Assert.AreEqual(43, editor.MojiPanels.Single().MojiData.FontSize);
            Assert.IsFalse(editor.TryLinkSelectedBalloon(Guid.NewGuid()));
            Assert.IsTrue(((TextBlock)editor.FindName("BalloonStatusTextBlock")!).Text.Contains("同じページ"));
            Assert.IsTrue(editor.UnlinkSelectedBalloon());
            Assert.IsFalse(editor.UnlinkSelectedBalloon());
            Assert.AreEqual(4, notifications);

            editor.BalloonVisuals.Single().BalloonData.IsLocked = true;
            editor.RestoreViewState(100, balloon.ObjectId);
            Assert.AreEqual(0, editor.ResizeHandleVisuals.Count);
            Assert.AreEqual(0, editor.TailHandleVisuals.Count);
        });
    }

    [TestMethod]
    public void TailHandlesChangeOnlyTheirFieldKeepScreenSizeAndCancelCleanly()
    {
        RunOnSta(() =>
        {
            var balloon = new BalloonData
            {
                X = 40,
                Y = 40,
                Bounds = new Rect(0, 0, 240, 140),
                Tail = new BalloonTailData { TipX = 160, TipY = 250, RootParameter = .5, Width = 24 },
            };
            using var editor = new PageEditorControl();
            editor.BindPage(new PageDocument("01", new[] { balloon }), null);
            editor.RestoreViewState(200, balloon.ObjectId);
            Assert.IsTrue(editor.TailHandleVisuals.All(handle => Math.Abs(handle.Width - 5) < .001));
            var live = editor.BalloonVisuals.Single().BalloonData;

            var before = live.Clone();
            editor.BeginBalloonGesture(balloon.ObjectId, live.Tail!.Tip, BalloonResizeHandle.TailTip);
            editor.CommitBalloonGesture(new Point(live.Tail.TipX + 30, live.Tail.TipY - 20));
            Assert.AreEqual(before.Tail!.RootParameter, live.Tail.RootParameter);
            Assert.AreEqual(before.Tail.Width, live.Tail.Width);
            Assert.AreNotEqual(before.Tail.Tip, live.Tail.Tip);

            before = live.Clone();
            var root = BalloonTailGeometry.GetRootPlacement(live).Point;
            editor.BeginBalloonGesture(balloon.ObjectId, root, BalloonResizeHandle.TailRoot);
            editor.CommitBalloonGesture(new Point(live.X + live.Bounds.Width, live.Y + live.Bounds.Height / 2));
            Assert.AreNotEqual(before.Tail!.RootParameter, live.Tail.RootParameter);
            Assert.AreEqual(before.Tail.Tip, live.Tail.Tip);
            Assert.AreEqual(before.Tail.Width, live.Tail.Width);

            before = live.Clone();
            var widthHandle = BalloonTailGeometry.GetWidthHandlePoint(live);
            var tangent = BalloonTailGeometry.GetRootPlacement(live).Tangent;
            editor.BeginBalloonGesture(balloon.ObjectId, widthHandle, BalloonResizeHandle.TailWidth);
            editor.CommitBalloonGesture(widthHandle + tangent * 25);
            Assert.AreEqual(before.Tail!.RootParameter, live.Tail.RootParameter);
            Assert.AreEqual(before.Tail.Tip, live.Tail.Tip);
            Assert.AreNotEqual(before.Tail.Width, live.Tail.Width);

            before = live.Clone();
            editor.BeginBalloonGesture(balloon.ObjectId, live.Tail.Tip, BalloonResizeHandle.TailTip);
            editor.UpdateBalloonGesture(live.Tail.Tip + new Vector(100, 100));
            editor.CancelBalloonGesture();
            Assert.AreEqual(before.Tail!.Tip, editor.BalloonVisuals.Single().BalloonData.Tail!.Tip);
        });
    }

    [TestMethod]
    public void TailGesturesCreateDistinctUndoEntriesAndCaptureLossIsClean()
    {
        RunOnSta(() =>
        {
            var balloon = new BalloonData
            {
                Tail = new BalloonTailData { TipX = 100, TipY = 220, RootParameter = .5, Width = 24 },
            };
            var page = new PageDocument("01", new[] { balloon });
            using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "p", new[] { page }));
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };
            var live = editor.BalloonVisuals.Single();
            for (var index = 0; index < 2; index++)
            {
                var start = live.BalloonData.Tail!.Tip;
                editor.BeginBalloonGesture(live.ObjectId, start, BalloonResizeHandle.TailTip);
                editor.CommitBalloonGesture(start + new Vector(10 + index, 0));
            }
            Assert.AreEqual(2, session.UndoCount);
            Assert.IsTrue(session.Undo());
            Assert.IsTrue(session.Undo());
            Assert.IsFalse(session.CanUndo);

            editor.ReloadBoundPage();
            live = editor.BalloonVisuals.Single();
            var before = live.BalloonData.Tail!.Tip;
            editor.BeginBalloonGesture(live.ObjectId, before, BalloonResizeHandle.TailTip);
            editor.UpdateBalloonGesture(before + new Vector(90, 80));
            editor.CancelBalloonGesture();
            Assert.AreEqual(before, live.BalloonData.Tail.Tip);
            Assert.AreEqual(0, session.UndoCount);
        });
    }

    [TestMethod]
    public void CompositionMovePreviewsAllMembersResizeDoesNotAutoLayoutAndCancelRestores()
    {
        RunOnSta(() =>
        {
            var text = new MojiData { Id = 1, FullText = "本文", X = 120, Y = 90, FontSize = 37 };
            var balloon = new BalloonData
            {
                X = 80,
                Y = 60,
                Bounds = new Rect(0, 0, 220, 130),
                Tail = new BalloonTailData { TipX = 190, TipY = 260, RootParameter = .5, Width = 22 },
                TextLink = new TextLinkData { TextObjectId = text.ObjectId },
            };
            using var editor = new PageEditorControl();
            editor.BindPage(new PageDocument("01", new[] { text }, new[] { balloon }), null);
            var liveBalloon = editor.BalloonVisuals.Single();
            var liveText = editor.MojiPanels.Single();
            var beforeBalloon = liveBalloon.BalloonData.Clone();
            var beforeText = PageDocument.CloneMojiData(liveText.MojiData);

            editor.BeginBalloonGesture(balloon.ObjectId, new Point(0, 0));
            editor.UpdateBalloonGesture(new Point(35, -15));
            Assert.AreEqual(beforeBalloon.X + 35, liveBalloon.BalloonData.X);
            Assert.AreEqual(beforeBalloon.Tail!.TipX + 35, liveBalloon.BalloonData.Tail!.TipX);
            Assert.AreEqual(beforeText.X + 35, liveText.MojiData.X);
            editor.CancelBalloonGesture();
            Assert.AreEqual(beforeBalloon.X, liveBalloon.BalloonData.X);
            Assert.AreEqual(beforeBalloon.Tail.Tip, liveBalloon.BalloonData.Tail!.Tip);
            Assert.AreEqual(beforeText.X, liveText.MojiData.X);

            editor.BeginBalloonGesture(balloon.ObjectId, new Point(0, 0), BalloonResizeHandle.Right);
            editor.CommitBalloonGesture(new Point(50, 0));
            Assert.AreEqual(beforeText.X, liveText.MojiData.X);
            Assert.AreEqual(beforeText.Y, liveText.MojiData.Y);
            Assert.AreEqual(beforeText.FontSize, liveText.MojiData.FontSize);
        });
    }

    [TestMethod]
    public void CompositionZOrderMovesTypedBlockAndUndoRedoPreservesUnrelatedOrder()
    {
        var unrelatedBack = new MojiData { Id = 1, FullText = "back" };
        var balloon = new BalloonData();
        var linkedText = new MojiData { Id = 2, FullText = "linked" };
        var symbol = new AttachedSymbolData { ParentId = linkedText.ObjectId, GraphemeAnchor = 0, Text = "!" };
        var unrelatedFront = new BalloonData();
        var page = new PageDocument("01");
        page.AddMojiData(unrelatedBack);
        page.AddBalloon(balloon);
        page.AddMojiData(linkedText);
        page.AddAttachedSymbol(symbol);
        page.AddBalloon(unrelatedFront);
        page.LinkBalloonText(balloon.ObjectId, linkedText.ObjectId);
        var project = new ProjectDocument(Guid.NewGuid(), "project", new[] { page });
        using var session = new ProjectSession(project);

        Assert.IsTrue(BalloonCommands.MoveComposition(session, page.PageId, balloon.ObjectId, BalloonCompositionOrder.BringToFront));
        CollectionAssert.AreEqual(
            new[] { unrelatedBack.ObjectId, unrelatedFront.ObjectId, balloon.ObjectId, linkedText.ObjectId, symbol.ObjectId },
            session.Document.Pages[0].AllObjects.Select(item => item.ObjectId).ToArray());
        Assert.IsFalse(BalloonCommands.MoveComposition(session, page.PageId, balloon.ObjectId, BalloonCompositionOrder.BringToFront));
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsTrue(session.Undo());
        CollectionAssert.AreEqual(
            new[] { unrelatedBack.ObjectId, balloon.ObjectId, linkedText.ObjectId, symbol.ObjectId, unrelatedFront.ObjectId },
            session.Document.Pages[0].AllObjects.Select(item => item.ObjectId).ToArray());
        Assert.IsTrue(session.Redo());

        var path = Path.Combine(Path.GetTempPath(), $"task130-{Guid.NewGuid():N}.mctzip");
        try
        {
            DataIO.WriteVersionedProject(path, session.Document);
            var restored = DataIO.ReadVersionedProject(path);
            CollectionAssert.AreEqual(
                session.Document.Pages[0].AllObjects.Select(item => item.ObjectId).ToArray(),
                restored.Pages[0].AllObjects.Select(item => item.ObjectId).ToArray());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void TailAndCompositionOperationsMeetMeasuredPerformanceBudget()
    {
        var factory = new BalloonGeometryFactory();
        var balloons = Enumerable.Range(0, 24).Select(index => new BalloonData
        {
            X = index * 4,
            Y = index * 3,
            Bounds = new Rect(0, 0, 240, 140),
            ShapeKind = (BalloonShapeKind)(index % 4),
            Tail = new BalloonTailData { TipX = 120 + index, TipY = 230 + index, RootParameter = (index % 10) / 10.0, Width = 24 },
        }).ToArray();
        var stopwatch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < 100; iteration++)
        foreach (var balloon in balloons)
        {
            var tail = BalloonTailGeometry.Create(balloon, factory);
            tail.FillContains(BalloonTailGeometry.PageToLocal(balloon, balloon.Tail!.Tip));
        }
        stopwatch.Stop();
        var refreshMs = stopwatch.Elapsed.TotalMilliseconds;

        stopwatch.Restart();
        for (var iteration = 0; iteration < 40; iteration++)
            BalloonTailGeometry.FindNearestRootParameter(BalloonShapeKind.Monologue,
                new Rect(0, 0, 240, 140), new Point(120 + iteration, 150), factory);
        stopwatch.Stop();
        var rootDragMs = stopwatch.Elapsed.TotalMilliseconds;
        TestContext?.WriteLine($"balloons=24; tail-refresh-hit-2400-ms={refreshMs:F3}; root-drag-40-ms={rootDragMs:F3}; cache={factory.CacheCount}");

        Assert.IsTrue(refreshMs < 3000, $"tail refresh/hit took {refreshMs:F3} ms");
        Assert.IsTrue(rootDragMs < 3000, $"root drag took {rootDragMs:F3} ms");
        Assert.IsTrue(factory.CacheCount <= factory.CacheCapacity);
    }

    private static void AssertFinite(Rect bounds)
    {
        Assert.IsFalse(double.IsNaN(bounds.X) || double.IsInfinity(bounds.X));
        Assert.IsFalse(double.IsNaN(bounds.Y) || double.IsInfinity(bounds.Y));
        Assert.IsFalse(double.IsNaN(bounds.Width) || double.IsInfinity(bounds.Width));
        Assert.IsFalse(double.IsNaN(bounds.Height) || double.IsInfinity(bounds.Height));
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
