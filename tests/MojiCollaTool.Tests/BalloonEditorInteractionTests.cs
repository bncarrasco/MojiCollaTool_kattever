using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class BalloonEditorInteractionTests
{
    [TestMethod]
    public void BalloonSelectionClearsOnBackgroundButNotOnResizeHandle()
    {
        RunOnSta(() =>
        {
            using var editor = CreateEditorWithBalloon(out var balloon);
            editor.RestoreViewState(100, balloon.ObjectId);
            Assert.AreEqual(balloon.ObjectId, editor.SelectedObjectId);

            editor.HandleCanvasClickSource(editor.CanvasBackgroundVisual);
            Assert.IsNull(editor.SelectedObjectId);

            editor.RestoreViewState(100, balloon.ObjectId);
            editor.HandleCanvasClickSource(editor.ResizeHandleVisuals.First());
            Assert.AreEqual(balloon.ObjectId, editor.SelectedObjectId);
        });
    }

    [TestMethod]
    public void MoveGestureChangesPositionAndCommitsOnce()
    {
        RunOnSta(() =>
        {
            using var editor = CreateEditorWithBalloon(out var balloon);
            var notifications = 0;
            editor.ContentChanged += (_, _) => notifications++;
            var beforeX = balloon.BalloonData.X;
            var beforeY = balloon.BalloonData.Y;

            Assert.IsTrue(editor.BeginBalloonGesture(balloon.ObjectId, new Point(10, 20)));
            editor.UpdateBalloonGesture(new Point(55, 65));
            Assert.IsTrue(editor.IsBalloonGestureActive);
            Assert.IsTrue(editor.CommitBalloonGesture(new Point(55, 65)));

            Assert.AreEqual(beforeX + 45, editor.BalloonVisuals[0].BalloonData.X);
            Assert.AreEqual(beforeY + 45, editor.BalloonVisuals[0].BalloonData.Y);
            Assert.AreEqual(1, notifications);
            Assert.IsFalse(editor.IsBalloonGestureActive);
        });
    }

    [TestMethod]
    public void EightResizeDirectionsAreSupported()
    {
        RunOnSta(() =>
        {
            using var editor = CreateEditorWithBalloon(out _);
            var directions = new[]
            {
                BalloonResizeHandle.Left, BalloonResizeHandle.Right,
                BalloonResizeHandle.Top, BalloonResizeHandle.Bottom,
                BalloonResizeHandle.TopLeft, BalloonResizeHandle.TopRight,
                BalloonResizeHandle.BottomLeft, BalloonResizeHandle.BottomRight,
            };

            foreach (var direction in directions)
            {
                var balloon = editor.AddNewBalloon(BalloonShapeKind.Rectangle);
                var before = balloon.BalloonData.Bounds;
                Assert.IsTrue(editor.BeginBalloonGesture(balloon.ObjectId, new Point(100, 100), direction));
                editor.UpdateBalloonGesture(new Point(80, 70));
                Assert.IsTrue(editor.CommitBalloonGesture(new Point(80, 70)));
                var after = balloon.BalloonData.Bounds;
                Assert.IsTrue(after.Width >= 24 && after.Height >= 24);
                Assert.IsTrue(after.Width != before.Width || after.Height != before.Height ||
                              balloon.BalloonData.X != 40 || balloon.BalloonData.Y != 40);
            }
        });
    }

    [TestMethod]
    public void ResizeNeverGoesBelowMinimumSize()
    {
        RunOnSta(() =>
        {
            using var editor = CreateEditorWithBalloon(out var balloon);
            Assert.IsTrue(editor.BeginBalloonGesture(balloon.ObjectId, new Point(100, 100), BalloonResizeHandle.Right));
            editor.UpdateBalloonGesture(new Point(-10000, 100));
            editor.CommitBalloonGesture(new Point(-10000, 100));

            Assert.AreEqual(24, balloon.BalloonData.Bounds.Width);
            Assert.IsTrue(balloon.BalloonData.Bounds.Height >= 24);
        });
    }

    [TestMethod]
    public void ZoomKeepsHandleSizeAndBackgroundHitBoundaryConsistent()
    {
        RunOnSta(() =>
        {
            using var editor = CreateEditorWithBalloon(out var balloon);
            editor.RestoreViewState(200, balloon.ObjectId);
            var handle = editor.ResizeHandleVisuals.First();

            Assert.AreEqual(4, handle.Width, 0.001);
            editor.HandleCanvasClickSource(handle);
            Assert.AreEqual(balloon.ObjectId, editor.SelectedObjectId);
            editor.HandleCanvasClickSource(new Rectangle());
            Assert.IsNull(editor.SelectedObjectId);
        });
    }

    [TestMethod]
    public void CaptureLossCancelsGestureWithoutNotification()
    {
        RunOnSta(() =>
        {
            using var editor = CreateEditorWithBalloon(out var balloon);
            var before = balloon.BalloonData.Clone();
            var notifications = 0;
            editor.ContentChanged += (_, _) => notifications++;

            Assert.IsTrue(editor.BeginBalloonGesture(balloon.ObjectId, new Point(0, 0)));
            editor.UpdateBalloonGesture(new Point(100, 120));
            editor.CancelBalloonGesture();

            Assert.AreEqual(before.X, balloon.BalloonData.X);
            Assert.AreEqual(before.Y, balloon.BalloonData.Y);
            Assert.AreEqual(before.Bounds, balloon.BalloonData.Bounds);
            Assert.AreEqual(0, notifications);
            Assert.IsFalse(editor.IsBalloonGestureActive);
        });
    }

    [TestMethod]
    public void TwoRapidGesturesCreateTwoUndoEntriesAndRedoBranchIsDiscarded()
    {
        RunOnSta(() =>
        {
            var page = new PageDocument("01", new[] { new BalloonData { X = 10, Y = 20 } });
            var project = new ProjectDocument(Guid.NewGuid(), "project", new[] { page });
            using var session = new ProjectSession(project);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };
            var balloon = editor.BalloonVisuals[0];

            Assert.IsTrue(editor.BeginBalloonGesture(balloon.ObjectId, new Point(0, 0)));
            editor.UpdateBalloonGesture(new Point(10, 0));
            editor.CommitBalloonGesture(new Point(10, 0));
            Assert.IsTrue(editor.BeginBalloonGesture(balloon.ObjectId, new Point(0, 0)));
            editor.UpdateBalloonGesture(new Point(20, 0));
            editor.CommitBalloonGesture(new Point(20, 0));

            Assert.AreEqual(2, session.UndoCount);
            Assert.IsTrue(session.IsDirty);
            Assert.IsTrue(session.Undo());
            Assert.IsTrue(session.CanRedo);
            Assert.IsTrue(session.Undo());
            Assert.IsFalse(session.CanUndo);
            Assert.IsTrue(session.Redo());
            session.ExecutePage(page.PageId, current => current.GetBalloon(balloon.ObjectId).Y = 9, "new gesture");
            Assert.IsFalse(session.CanRedo);
        });
    }

    [TestMethod]
    public void SavedDirtyStateTracksUndoRedoAroundBalloonGesture()
    {
        RunOnSta(() =>
        {
            var page = new PageDocument("01", new[] { new BalloonData() });
            var project = new ProjectDocument(Guid.NewGuid(), "project", new[] { page });
            using var session = new ProjectSession(project);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };
            session.MarkSaved();
            var balloon = editor.BalloonVisuals[0];

            editor.BeginBalloonGesture(balloon.ObjectId, new Point(0, 0));
            editor.UpdateBalloonGesture(new Point(25, 0));
            editor.CommitBalloonGesture(new Point(25, 0));
            Assert.IsTrue(session.IsDirty);
            Assert.IsTrue(session.Undo());
            Assert.IsFalse(session.IsDirty);
            Assert.IsTrue(session.Redo());
            Assert.IsTrue(session.IsDirty);
        });
    }

    [TestMethod]
    public void MixedZOrderIsReflectedByEditorVisualOrder()
    {
        RunOnSta(() =>
        {
            var firstBalloon = new BalloonData();
            var text = new MojiData { FullText = "text" };
            var secondBalloon = new BalloonData();
            var page = new PageDocument("01");
            page.AddBalloon(firstBalloon);
            page.AddMojiData(text);
            page.AddBalloon(secondBalloon);
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);

            var ids = editor.Canvas.Children
                .OfType<UIElement>()
                .Where(item => item is BalloonVisual || item is MojiPanel)
                .Select(item => item is BalloonVisual balloon ? balloon.ObjectId : ((MojiPanel)item).MojiData.ObjectId)
                .ToArray();
            CollectionAssert.AreEqual(new[] { firstBalloon.ObjectId, text.ObjectId, secondBalloon.ObjectId }, ids);
        });
    }

    [TestMethod]
    public void SelectionRestoresForTextAndBalloonAcrossPageAndProjectSwitch()
    {
        RunOnSta(() =>
        {
            var text = new MojiData { FullText = "text" };
            var firstBalloon = new BalloonData();
            var firstPage = new PageDocument("01", new[] { text }, new[] { firstBalloon });
            var secondBalloon = new BalloonData();
            var secondPage = new PageDocument("02", new[] { secondBalloon });
            var firstProject = new ProjectDocument(Guid.NewGuid(), "first", new[] { firstPage });
            var secondProject = new ProjectDocument(Guid.NewGuid(), "second", new[] { secondPage });
            using var editor = new PageEditorControl();

            editor.BindPage(firstProject.Pages[0], null);
            editor.RestoreViewState(100, text.ObjectId);
            Assert.AreEqual(text.ObjectId, editor.SelectedObjectId);
            editor.BindPage(secondProject.Pages[0], null);
            editor.BindPage(firstProject.Pages[0], null);
            Assert.AreEqual(text.ObjectId, editor.SelectedObjectId);
            editor.RestoreViewState(100, firstBalloon.ObjectId);
            Assert.AreEqual(firstBalloon.ObjectId, editor.SelectedObjectId);
            editor.BindPage(secondProject.Pages[0], null);
            editor.RestoreViewState(100, secondBalloon.ObjectId);
            editor.BindPage(firstProject.Pages[0], null);

            Assert.AreEqual(firstBalloon.ObjectId, editor.SelectedObjectId);
            editor.BindPage(secondProject.Pages[0], null);
            Assert.AreEqual(secondBalloon.ObjectId, editor.SelectedObjectId);
        });
    }

    [TestMethod]
    public void BalloonSaveAndReloadPreservesSelectedObjectData()
    {
        using var scope = TemporaryDirectory.Create();
        var path = System.IO.Path.Combine(scope.Path, "balloon.mctzip");
        var balloon = new BalloonData
        {
            ShapeKind = BalloonShapeKind.RoundedRectangle,
            X = 31,
            Y = 42,
            Bounds = new Rect(0, 0, 333, 177),
        };
        var source = new ProjectDocument(Guid.NewGuid(), "save", new[] { new PageDocument("01", new[] { balloon }) });
        DataIO.WriteVersionedProject(path, source);

        var restored = DataIO.ReadVersionedProject(path);
        var actual = restored.Pages[0].Balloons.Single();
        Assert.AreEqual(balloon.ObjectId, actual.ObjectId);
        Assert.AreEqual(balloon.ShapeKind, actual.ShapeKind);
        Assert.AreEqual(balloon.X, actual.X);
        Assert.AreEqual(balloon.Bounds, actual.Bounds);
    }

    private static PageEditorControl CreateEditorWithBalloon(out BalloonVisual visual)
    {
        var page = new PageDocument("01", new[] { new BalloonData { X = 40, Y = 40 } });
        var editor = new PageEditorControl();
        editor.BindPage(page, null);
        visual = editor.BalloonVisuals[0];
        return editor;
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

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;
        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MojiCollaToolTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}

[TestClass]
public class BalloonGeometryPerformanceTests
{
    public TestContext? TestContext { get; set; }

    [TestMethod]
    public void WarmGeometryCacheMeetsPerformanceBaseline()
    {
        var factory = new BalloonGeometryFactory();
        var bounds = new Rect(0, 0, 240, 140);
        for (var index = 0; index < 1000; index++) factory.Create(BalloonShapeKind.Ellipse, bounds);

        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < 10000; index++) factory.Create(BalloonShapeKind.Ellipse, bounds);
        stopwatch.Stop();

        TestContext?.WriteLine($"warm-cache 10000 calls: {stopwatch.Elapsed.TotalMilliseconds:F3} ms; hits={factory.CacheHits}; misses={factory.CacheMisses}");
        Assert.AreEqual(10999, factory.CacheHits);
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500), $"warm cache took {stopwatch.Elapsed.TotalMilliseconds:F3} ms");
    }
}
