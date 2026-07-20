using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public sealed class TASK150BalloonMergeTests
{
    public TestContext? TestContext { get; set; }

    [TestMethod]
    public void ModelValidatesFlatGroupsCloneIdentityAndMemberDeletion()
    {
        var first = CreateBalloon(10, 20, BalloonShapeKind.Ellipse);
        var second = CreateBalloon(120, 20, BalloonShapeKind.Rectangle);
        var third = CreateBalloon(260, 20, BalloonShapeKind.Monologue);
        var fourth = CreateBalloon(400, 20, BalloonShapeKind.RoundedRectangle);
        var page = new PageDocument("モデル", new[] { first, second, third, fourth });

        Assert.IsTrue(page.MergeBalloons(first.ObjectId, second.ObjectId));
        var firstGroup = page.FindBalloonMergeByMember(first.ObjectId)!;
        var stableMergeId = firstGroup.MergeId;
        CollectionAssert.AreEqual(new[] { first.ObjectId, second.ObjectId }, firstGroup.MemberIds);
        Assert.AreEqual(first.ObjectId, firstGroup.PrimaryBalloonId);

        Assert.IsTrue(page.MergeBalloons(third.ObjectId, fourth.ObjectId));
        Assert.IsTrue(page.MergeBalloons(second.ObjectId, third.ObjectId));
        var flattened = page.FindBalloonMergeByMember(fourth.ObjectId)!;
        Assert.AreEqual(stableMergeId, flattened.MergeId);
        Assert.AreEqual(first.ObjectId, flattened.PrimaryBalloonId);
        CollectionAssert.AreEqual(
            new[] { first.ObjectId, second.ObjectId, third.ObjectId, fourth.ObjectId }, flattened.MemberIds);
        Assert.AreEqual(1, page.BalloonMerges.Count);
        Assert.IsFalse(page.MergeBalloons(first.ObjectId, fourth.ObjectId));

        var preserved = page.Clone(preserveObjectIds: true);
        CollectionAssert.AreEqual(flattened.MemberIds, preserved.BalloonMerges.Single().MemberIds);
        Assert.AreEqual(flattened.MergeId, preserved.BalloonMerges.Single().MergeId);
        var remapped = page.Clone();
        Assert.AreNotEqual(flattened.MergeId, remapped.BalloonMerges.Single().MergeId);
        Assert.IsTrue(remapped.BalloonMerges.Single().MemberIds.All(id => remapped.ContainsBalloon(id)));
        Assert.IsFalse(remapped.BalloonMerges.Single().MemberIds.Intersect(flattened.MemberIds).Any());

        Assert.IsTrue(page.RemoveBalloon(first.ObjectId));
        var afterPrimaryDelete = page.BalloonMerges.Single();
        Assert.AreEqual(second.ObjectId, afterPrimaryDelete.PrimaryBalloonId);
        CollectionAssert.AreEqual(new[] { second.ObjectId, third.ObjectId, fourth.ObjectId }, afterPrimaryDelete.MemberIds);
        Assert.IsTrue(page.RemoveBalloon(third.ObjectId));
        Assert.IsTrue(page.RemoveBalloon(fourth.ObjectId));
        Assert.AreEqual(0, page.BalloonMerges.Count);

        var ids = new[] { second.ObjectId, Guid.NewGuid() };
        Assert.ThrowsException<InvalidDataException>(() => new BalloonMergeData
        {
            PrimaryBalloonId = second.ObjectId,
            MemberIds = new List<Guid> { second.ObjectId },
        }.Validate(ids));
        Assert.ThrowsException<InvalidDataException>(() => new BalloonMergeData
        {
            PrimaryBalloonId = Guid.NewGuid(),
            MemberIds = ids.ToList(),
        }.Validate(ids));
        Assert.ThrowsException<InvalidDataException>(() => new BalloonMergeData
        {
            PrimaryBalloonId = second.ObjectId,
            MemberIds = new List<Guid> { second.ObjectId, second.ObjectId },
        }.Validate(ids));
        Assert.ThrowsException<InvalidDataException>(() => new BalloonMergeData
        {
            PrimaryBalloonId = second.ObjectId,
            MemberIds = new List<Guid> { second.ObjectId, Guid.NewGuid() },
        }.Validate(ids));

        var validationPage = new PageDocument("重複所属", new[] { second, third, fourth });
        var duplicateMergeId = Guid.NewGuid();
        Assert.ThrowsException<InvalidDataException>(() => validationPage.SetBalloonMerges(new[]
        {
            new BalloonMergeData
            {
                MergeId = duplicateMergeId,
                PrimaryBalloonId = second.ObjectId,
                MemberIds = new List<Guid> { second.ObjectId, third.ObjectId },
            },
            new BalloonMergeData
            {
                MergeId = duplicateMergeId,
                PrimaryBalloonId = third.ObjectId,
                MemberIds = new List<Guid> { third.ObjectId, fourth.ObjectId },
            },
        }));
        Assert.AreEqual(0, validationPage.BalloonMerges.Count, "Cross-group validation must be atomic.");
    }

    [TestMethod]
    public void MergeAndUnmergeAreNonDestructiveAndTypedCompositionIsAtomic()
    {
        var firstText = new MojiData
        {
            FullText = "横書き本文", X = 34, Y = 45, TextDirection = TextDirection.Yokogaki, FontSize = 31,
        };
        var secondText = new MojiData
        {
            FullText = "縦書き本文", X = 246, Y = 52, TextDirection = TextDirection.Tategaki, FontSize = 27,
        };
        var first = CreateBalloon(20, 30, BalloonShapeKind.Ellipse);
        first.Rotation = 12;
        first.Fill = Colors.LightYellow;
        first.Stroke = Colors.DarkBlue;
        first.StrokeThickness = 5;
        first.Tail = new BalloonTailData { TipX = 90, TipY = 230, RootParameter = .54, Width = 34 };
        first.TextLink = new TextLinkData
        {
            TextObjectId = firstText.ObjectId, LayoutMode = BalloonTextLayoutMode.FitTextToBalloon,
            Padding = 11, MinimumFontSize = 9, Alignment = BalloonTextAlignment.End,
        };
        var second = CreateBalloon(210, 40, BalloonShapeKind.Rectangle);
        second.Rotation = -7;
        second.Fill = Colors.Pink;
        second.TextLink = new TextLinkData
        {
            TextObjectId = secondText.ObjectId, LayoutMode = BalloonTextLayoutMode.FitBalloonToText,
            Padding = 6, MinimumFontSize = 8, Alignment = BalloonTextAlignment.Start,
        };
        var symbol = new AttachedSymbolData
        {
            ParentId = secondText.ObjectId, Text = "※", GraphemeAnchor = 0, OffsetX = .4, OffsetY = -.2,
        };
        var page = new PageDocument(Guid.NewGuid(), "非破壊", new CanvasData(),
            new[] { firstText, secondText }, new[] { first, second }, new[] { symbol });
        var before = page.Clone(page.PageId, preserveObjectIds: true);

        Assert.IsTrue(page.MergeBalloons(first.ObjectId, second.ObjectId));
        AssertBalloonEqual(before.GetBalloon(first.ObjectId), page.GetBalloon(first.ObjectId));
        AssertBalloonEqual(before.GetBalloon(second.ObjectId), page.GetBalloon(second.ObjectId));
        Assert.AreEqual(firstText.FullText, page.GetObject(firstText.ObjectId).FullText);
        Assert.AreEqual(secondText.TextDirection, page.GetObject(secondText.ObjectId).TextDirection);
        CollectionAssert.AreEqual(
            new[] { first.ObjectId, firstText.ObjectId, second.ObjectId, secondText.ObjectId, symbol.ObjectId },
            page.GetObjectOrderBlock(symbol.ObjectId).ToArray());

        page.GetDocumentObject(symbol.ObjectId).IsLocked = true;
        var lockedOrder = page.AllObjects.Select(item => item.ObjectId).ToArray();
        Assert.IsFalse(page.MoveObjectOrder(second.ObjectId, ObjectOrderOperation.BringToFront));
        Assert.IsFalse(page.UnmergeBalloons(first.ObjectId));
        CollectionAssert.AreEqual(lockedOrder, page.AllObjects.Select(item => item.ObjectId).ToArray());
        Assert.AreEqual(1, page.BalloonMerges.Count);

        page.GetDocumentObject(symbol.ObjectId).IsLocked = false;
        Assert.IsTrue(page.UnmergeBalloons(second.ObjectId));
        AssertBalloonEqual(before.GetBalloon(first.ObjectId), page.GetBalloon(first.ObjectId));
        AssertBalloonEqual(before.GetBalloon(second.ObjectId), page.GetBalloon(second.ObjectId));
        Assert.AreEqual(BalloonTextLayoutMode.FitTextToBalloon,
            page.GetBalloon(first.ObjectId).TextLink!.LayoutMode);
        Assert.AreEqual(BalloonTextLayoutMode.FitBalloonToText,
            page.GetBalloon(second.ObjectId).TextLink!.LayoutMode);
    }

    [TestMethod]
    public void SemanticCommandsCreateOneHistoryEntryPreserveDirtyAndPropagateSubscriberFailure()
    {
        var first = CreateBalloon(10, 10, BalloonShapeKind.Ellipse);
        var second = CreateBalloon(150, 10, BalloonShapeKind.Rectangle);
        var page = new PageDocument("履歴", new[] { first, second });
        using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "履歴", new[] { page }));

        Assert.IsTrue(BalloonMergeCommands.Merge(session, page.PageId, first.ObjectId, second.ObjectId));
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsTrue(session.IsDirty);
        session.MarkSaved();
        Assert.IsFalse(session.IsDirty);
        Assert.IsFalse(BalloonMergeCommands.Merge(session, page.PageId, first.ObjectId, second.ObjectId));
        Assert.AreEqual(1, session.UndoCount);
        Assert.IsFalse(session.IsDirty);

        Assert.IsTrue(BalloonMergeCommands.Unmerge(session, page.PageId, second.ObjectId));
        Assert.AreEqual(2, session.UndoCount);
        Assert.IsTrue(session.IsDirty);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(1, session.Document.GetPage(page.PageId).BalloonMerges.Count);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(0, session.Document.GetPage(page.PageId).BalloonMerges.Count);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(BalloonMergeCommands.Unmerge(session, page.PageId, first.ObjectId));
        Assert.AreEqual(0, session.RedoCount);

        Assert.IsTrue(BalloonMergeCommands.Merge(session, page.PageId, first.ObjectId, second.ObjectId));
        session.Document.GetPage(page.PageId).GetBalloon(second.ObjectId).IsLocked = true;
        var undoCount = session.UndoCount;
        Assert.IsFalse(BalloonMergeCommands.Unmerge(session, page.PageId, first.ObjectId));
        Assert.AreEqual(undoCount, session.UndoCount);
        session.Document.GetPage(page.PageId).GetBalloon(second.ObjectId).IsLocked = false;

        var delivered = false;
        session.PropertyChanged += (_, e) =>
        {
            if (!delivered && e.PropertyName == nameof(ProjectSession.CurrentRevision))
            {
                delivered = true;
                throw new InvalidOperationException("subscriber");
            }
        };
        Assert.ThrowsException<InvalidOperationException>(() =>
            BalloonMergeCommands.Unmerge(session, page.PageId, first.ObjectId));
        Assert.IsTrue(delivered);
        Assert.AreEqual(0, session.Document.GetPage(page.PageId).BalloonMerges.Count,
            "Semantic commit must remain after a subscriber exception.");
    }

    [TestMethod]
    public void GeometryUnionSupportsShapesRotationTailsExactHitAndBoundedCache()
    {
        RunOnSta(() =>
        {
            var shapes = new[]
            {
                BalloonShapeKind.Ellipse, BalloonShapeKind.RoundedRectangle, BalloonShapeKind.Rectangle,
                BalloonShapeKind.Monologue, BalloonShapeKind.Unknown,
            };
            foreach (var shape in shapes)
            {
                var first = CreateBalloon(10, 20, shape);
                first.Bounds = new Rect(0, 0, 150, 100);
                first.Rotation = 13;
                first.Tail = new BalloonTailData { TipX = 45, TipY = 190, RootParameter = .5, Width = 25 };
                var second = CreateBalloon(100, 35, BalloonShapeKind.Rectangle);
                second.Bounds = new Rect(0, 0, 140, 90);
                second.Rotation = -8;
                second.Fill = Colors.Red;
                var merge = Merge(first, second);
                var factory = new BalloonMergeGeometryFactory(cacheCapacity: 8);

                var geometry = factory.Create(merge, new[] { first, second });
                AssertFinite(geometry.Bounds);
                Assert.IsFalse(geometry.Body.IsEmpty());
                Assert.AreEqual(1, geometry.Tails.Count);
                Assert.IsTrue(geometry.Contains(new Point(125, 75), first.StrokeThickness));
                Assert.IsFalse(geometry.Contains(new Point(800, 800), first.StrokeThickness));
                Assert.IsTrue(geometry.Body.IsFrozen);
                Assert.IsTrue(geometry.Tails.All(tail => tail.IsFrozen));
                Assert.AreSame(geometry, factory.Create(merge, new[] { first, second }));
                Assert.AreEqual(1L, factory.CacheHits);
            }

            var left = CreateBalloon(0, 0, BalloonShapeKind.Rectangle);
            left.Bounds = new Rect(0, 0, 100, 100);
            var right = CreateBalloon(300, 0, BalloonShapeKind.Rectangle);
            right.Bounds = new Rect(0, 0, 100, 100);
            var disconnected = new BalloonMergeGeometryFactory(4).Create(Merge(left, right), new[] { left, right });
            Assert.IsTrue(disconnected.Contains(new Point(50, 50), 2));
            Assert.IsTrue(disconnected.Contains(new Point(350, 50), 2));
            Assert.IsFalse(disconnected.Contains(new Point(200, 50), 2), "Transparent bounding-box gap must not hit.");

            right.X = 100;
            var touching = new BalloonMergeGeometryFactory(4).Create(Merge(left, right), new[] { left, right });
            Assert.IsTrue(touching.Body.FillContains(new Point(100, 50)));
            var internalPen = new Pen(Brushes.Black, 1);
            Assert.IsFalse(touching.Body.StrokeContains(internalPen, new Point(100, 50)),
                "The original internal seam must be suppressed by the union.");

            var containing = CreateBalloon(20, 20, BalloonShapeKind.Ellipse);
            containing.Bounds = new Rect(0, 0, 300, 200);
            var inside = CreateBalloon(100, 70, BalloonShapeKind.Rectangle);
            inside.Bounds = new Rect(0, 0, 40, 30);
            var contained = new BalloonMergeGeometryFactory(4).Create(Merge(containing, inside), new[] { containing, inside });
            Assert.IsTrue(contained.Contains(new Point(120, 85), 2));

            var bounded = new BalloonMergeGeometryFactory(3);
            var pair = new[] { left, right };
            var group = Merge(left, right);
            for (var i = 0; i < 12; i++)
            {
                pair[1].X = 120 + i;
                bounded.Create(group, pair);
            }
            Assert.AreEqual(3, bounded.CacheCount);
        });
    }

    [TestMethod]
    public void JapaneseUiUsesOneMergeVisualAndDisablesIndividualHandles()
    {
        RunOnSta(() =>
        {
            var first = CreateBalloon(30, 40, BalloonShapeKind.Ellipse);
            first.Tail = new BalloonTailData { TipX = 100, TipY = 220, RootParameter = .5, Width = 30 };
            var second = CreateBalloon(140, 40, BalloonShapeKind.Rectangle);
            var page = new PageDocument("画面", new[] { first, second });
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.RestoreViewState(100, first.ObjectId);
            var candidate = (ComboBox)editor.FindName("BalloonMergeCandidateComboBox")!;
            var mergeButton = (Button)editor.FindName("MergeBalloonButton")!;
            var unmergeButton = (Button)editor.FindName("UnmergeBalloonButton")!;
            var status = (TextBlock)editor.FindName("BalloonStatusTextBlock")!;

            Assert.AreEqual("フキダシ合体", mergeButton.Content);
            Assert.AreEqual("合体解除", unmergeButton.Content);
            Assert.AreEqual(1, candidate.Items.Count);
            candidate.SelectedIndex = 0;
            Assert.IsTrue(mergeButton.IsEnabled);
            mergeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual(first.ObjectId, editor.SelectedBalloonId);
            Assert.AreEqual(1, page.BalloonMerges.Count);
            Assert.AreEqual(1, editor.BalloonMergeVisuals.Count);
            Assert.IsTrue(editor.BalloonVisuals.All(visual => visual.IsMergeRenderingSuppressed));
            Assert.IsTrue(editor.Canvas.Children.Contains(editor.BalloonMergeVisuals.Single()));
            Assert.IsTrue(editor.BalloonVisuals.All(visual => !editor.Canvas.Children.Contains(visual)));
            Assert.AreEqual(0, editor.ResizeHandleVisuals.Count, "Resize handles must be absent while merged.");
            Assert.AreEqual(0, editor.TailHandleVisuals.Count, "Tail handles must be absent while merged.");
            StringAssert.Contains(status.Text, "合体中");
            StringAssert.Contains(status.Text, "合体解除後");
            Assert.IsTrue(unmergeButton.IsEnabled);

            var visual = editor.BalloonMergeVisuals.Single();
            editor.Canvas.Measure(new Size(700, 500));
            editor.Canvas.Arrange(new Rect(0, 0, 700, 500));
            Assert.IsTrue(visual.ContainsLocalPoint(new Point(visual.Width / 2, visual.Height / 2)));
            Assert.IsTrue(unmergeButton.IsEnabled, "Unmerge button changed availability before click.");
            unmergeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(0, page.BalloonMerges.Count,
                $"Unmerge button must remove the relationship. Status: {status.Text}");
            Assert.AreEqual(0, editor.BalloonMergeVisuals.Count, "Unmerge must remove the merge visual.");
            Assert.IsTrue(editor.BalloonVisuals.All(item => !item.IsMergeRenderingSuppressed));
        });
    }

    [TestMethod]
    public void GroupGestureMovesLinkedTextAndSymbolsAndCancelOrMidLockIsAtomic()
    {
        RunOnSta(() =>
        {
            var horizontal = new MojiData { FullText = "横書き", X = 40, Y = 50, TextDirection = TextDirection.Yokogaki };
            var vertical = new MojiData { FullText = "縦書き", X = 230, Y = 60, TextDirection = TextDirection.Tategaki };
            var first = CreateBalloon(20, 30, BalloonShapeKind.Ellipse);
            first.TextLink = new TextLinkData
            {
                TextObjectId = horizontal.ObjectId, LayoutMode = BalloonTextLayoutMode.FitTextToBalloon,
            };
            first.Tail = new BalloonTailData { TipX = 80, TipY = 220, RootParameter = .5, Width = 25 };
            var second = CreateBalloon(210, 35, BalloonShapeKind.Rectangle);
            second.TextLink = new TextLinkData
            {
                TextObjectId = vertical.ObjectId, LayoutMode = BalloonTextLayoutMode.FitBalloonToText,
            };
            var unlinked = CreateBalloon(390, 45, BalloonShapeKind.RoundedRectangle);
            var attached = new AttachedSymbolData
            {
                ParentId = horizontal.ObjectId, Text = "※", GraphemeAnchor = 0, OffsetX = .5, OffsetY = -.25,
            };
            var page = new PageDocument(Guid.NewGuid(), "移動", new CanvasData(),
                new[] { horizontal, vertical }, new[] { first, second, unlinked }, new[] { attached });
            page.MergeBalloons(first.ObjectId, second.ObjectId);
            page.MergeBalloons(first.ObjectId, unlinked.ObjectId);
            using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "移動", new[] { page }));
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.RestoreViewState(100, first.ObjectId);
            var notifications = 0;
            editor.ContentChanged += (_, _) =>
            {
                notifications++;
                editor.CapturePage();
                session.MarkChanged(page.PageId, editor.ContentChangeDescription, editor.ContentChangeCoalesceKey);
            };

            Assert.IsTrue(editor.BeginBalloonGesture(first.ObjectId, new Point(0, 0)));
            Assert.IsTrue(editor.UpdateBalloonGesture(new Point(35, -15)));
            Assert.AreEqual(55, editor.BalloonVisuals.Single(v => v.ObjectId == first.ObjectId).BalloonData.X);
            Assert.AreEqual(245, editor.BalloonVisuals.Single(v => v.ObjectId == second.ObjectId).BalloonData.X);
            Assert.AreEqual(425, editor.BalloonVisuals.Single(v => v.ObjectId == unlinked.ObjectId).BalloonData.X);
            Assert.AreEqual(75, editor.MojiPanels.Single(p => p.MojiData.ObjectId == horizontal.ObjectId).MojiData.X);
            editor.CancelBalloonGesture();
            Assert.AreEqual(20, editor.BalloonVisuals.Single(v => v.ObjectId == first.ObjectId).BalloonData.X);
            Assert.AreEqual(390, editor.BalloonVisuals.Single(v => v.ObjectId == unlinked.ObjectId).BalloonData.X);
            Assert.AreEqual(40, editor.MojiPanels.Single(p => p.MojiData.ObjectId == horizontal.ObjectId).MojiData.X);
            Assert.AreEqual(0, notifications);

            Assert.IsTrue(editor.BeginBalloonGesture(second.ObjectId, new Point(0, 0)));
            Assert.IsTrue(editor.CommitBalloonGesture(new Point(25, 20)));
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(1, session.UndoCount);
            Assert.AreEqual(45, session.ActivePage!.GetBalloon(first.ObjectId).X);
            Assert.AreEqual(235, session.ActivePage.GetBalloon(second.ObjectId).X);
            Assert.AreEqual(415, session.ActivePage.GetBalloon(unlinked.ObjectId).X);
            Assert.AreEqual(65, session.ActivePage.GetObject(horizontal.ObjectId).X);
            Assert.AreEqual(255, session.ActivePage.GetObject(vertical.ObjectId).X);
            Assert.AreEqual(BalloonTextLayoutMode.FitTextToBalloon,
                session.ActivePage.GetBalloon(first.ObjectId).TextLink!.LayoutMode);
            Assert.AreEqual(BalloonTextLayoutMode.FitBalloonToText,
                session.ActivePage.GetBalloon(second.ObjectId).TextLink!.LayoutMode);
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(20, session.ActivePage!.GetBalloon(first.ObjectId).X);
            Assert.IsTrue(session.Redo());
            Assert.AreEqual(45, session.ActivePage!.GetBalloon(first.ObjectId).X);

            editor.BindPage(session.ActivePage, null);
            editor.RestoreViewState(100, first.ObjectId);
            Assert.IsTrue(editor.BeginBalloonGesture(first.ObjectId, new Point(0, 0)));
            Assert.IsTrue(editor.UpdateBalloonGesture(new Point(50, 0)));
            editor.BalloonVisuals.Single(v => v.ObjectId == second.ObjectId).BalloonData.IsLocked = true;
            Assert.IsFalse(editor.UpdateBalloonGesture(new Point(70, 0)));
            Assert.IsFalse(editor.IsBalloonGestureActive);
            Assert.AreEqual(45, editor.BalloonVisuals.Single(v => v.ObjectId == first.ObjectId).BalloonData.X);
            Assert.AreEqual(1, notifications);
            StringAssert.Contains(((TextBlock)editor.FindName("BalloonStatusTextBlock")!).Text, "取り消しました");
        });
    }

    [TestMethod]
    public void Version24RoundTripsOrderedGroupsAndRejectsInvalidOrFutureArchives()
    {
        var first = CreateBalloon(10, 20, BalloonShapeKind.Ellipse);
        var second = CreateBalloon(130, 20, BalloonShapeKind.Rectangle);
        var page = new PageDocument("日本語ページ", new[] { first, second });
        page.MergeBalloons(first.ObjectId, second.ObjectId);
        var merge = page.BalloonMerges.Single().Clone();
        var project = new ProjectDocument(Guid.NewGuid(), "日本語プロジェクト", new[] { page });
        using var scope = TemporaryDirectory.Create();
        var path = Path.Combine(scope.Path, "日本語 合体.mctzip");
        DataIO.WriteVersionedProject(path, project);

        using (var archive = ZipFile.OpenRead(path))
        using (var stream = archive.GetEntry(VersionedProjectFormat.ManifestEntryName)!.Open())
        {
            var manifest = XDocument.Load(stream);
            Assert.AreEqual("2.4", (string?)manifest.Root!.Element("FormatVersion"));
            Assert.AreEqual("2.4", (string?)manifest.Root.Element("MinimumReaderVersion"));
        }
        var restored = DataIO.ReadVersionedProject(path);
        Assert.AreEqual("日本語プロジェクト", restored.Name);
        Assert.AreEqual("日本語ページ", restored.Pages.Single().Name);
        Assert.AreEqual(merge.MergeId, restored.Pages.Single().BalloonMerges.Single().MergeId);
        Assert.AreEqual(merge.PrimaryBalloonId, restored.Pages.Single().BalloonMerges.Single().PrimaryBalloonId);
        CollectionAssert.AreEqual(merge.MemberIds, restored.Pages.Single().BalloonMerges.Single().MemberIds);

        var oldPath = Path.Combine(scope.Path, "旧2.3.mctzip");
        File.Copy(path, oldPath);
        RewriteManifestVersion(oldPath, "2.3", "2.3");
        RemoveMergeCollection(oldPath, page.PageId);
        Assert.AreEqual(0, DataIO.ReadVersionedProject(oldPath).Pages.Single().BalloonMerges.Count);

        var futurePath = Path.Combine(scope.Path, "future.mctzip");
        File.Copy(path, futurePath);
        RewriteManifestVersion(futurePath, "2.5", "2.5");
        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(futurePath));

        var invalidPath = Path.Combine(scope.Path, "invalid.mctzip");
        File.Copy(path, invalidPath);
        CorruptMergeWithMissingMember(invalidPath, page.PageId);
        var bytesBefore = File.ReadAllBytes(invalidPath);
        Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(invalidPath));
        CollectionAssert.AreEqual(bytesBefore, File.ReadAllBytes(invalidPath));
        Assert.AreEqual(1, page.BalloonMerges.Count, "Failed load must not mutate existing working data.");
    }

    [TestMethod]
    public void MergedLayoutApplyButtonAndDirectCommandAreRejectedWithoutMutation()
    {
        RunOnSta(() =>
        {
            var text = new MojiData { FullText = "合体中レイアウト", X = 60, Y = 70, FontSize = 32 };
            var first = CreateBalloon(30, 40, BalloonShapeKind.Ellipse);
            first.TextLink = new TextLinkData
            {
                TextObjectId = text.ObjectId,
                LayoutMode = BalloonTextLayoutMode.FitTextToBalloon,
                Padding = 8,
                MinimumFontSize = 8,
            };
            var second = CreateBalloon(150, 40, BalloonShapeKind.Rectangle);
            var page = new PageDocument("合体レイアウト拒否", new[] { text }, new[] { first, second });
            page.MergeBalloons(first.ObjectId, second.ObjectId);
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.RestoreViewState(100, first.ObjectId);
            var apply = (Button)editor.FindName("ApplyTextLayoutButton")!;
            var mode = (ComboBox)editor.FindName("TextLayoutModeComboBox")!;
            mode.SelectedItem = mode.Items.Cast<ComboBoxItem>()
                .Single(item => (string)item.Tag == "FitBalloonToText");
            var before = page.Clone(page.PageId, preserveObjectIds: true);
            var notifications = 0;
            editor.ContentChanged += (_, _) => notifications++;

            Assert.IsFalse(apply.IsEnabled);
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            AssertBalloonEqual(before.GetBalloon(first.ObjectId), page.GetBalloon(first.ObjectId));
            Assert.AreEqual(0, notifications);
            Assert.IsFalse(editor.ApplySelectedTextLayout());
            StringAssert.Contains(((TextBlock)editor.FindName("BalloonStatusTextBlock")!).Text, "合体解除後");
            AssertBalloonEqual(before.GetBalloon(first.ObjectId), page.GetBalloon(first.ObjectId));
            Assert.AreEqual(0, notifications);
        });
    }

    [TestMethod]
    public void PublicModelRemovalPreflightsEveryMergedTypedCompositionMember()
    {
        var text = new MojiData { FullText = "削除ロック" };
        var first = CreateBalloon(20, 20, BalloonShapeKind.Ellipse);
        first.TextLink = new TextLinkData { TextObjectId = text.ObjectId };
        var second = CreateBalloon(150, 20, BalloonShapeKind.Rectangle);
        var symbol = new AttachedSymbolData
        {
            ParentId = text.ObjectId, Text = "※", GraphemeAnchor = 0,
        };
        var page = new PageDocument(Guid.NewGuid(), "削除", new CanvasData(),
            new[] { text }, new[] { first, second }, new[] { symbol });
        page.MergeBalloons(first.ObjectId, second.ObjectId);
        var order = page.AllObjects.Select(item => item.ObjectId).ToArray();

        page.GetBalloon(second.ObjectId).IsLocked = true;
        Assert.IsFalse(page.RemoveBalloon(page.GetBalloon(first.ObjectId)));
        Assert.IsTrue(page.ContainsBalloon(first.ObjectId));
        Assert.AreEqual(1, page.BalloonMerges.Count);
        CollectionAssert.AreEqual(order, page.AllObjects.Select(item => item.ObjectId).ToArray());

        page.GetBalloon(second.ObjectId).IsLocked = false;
        page.GetObject(text.ObjectId).IsLocked = true;
        Assert.IsFalse(page.RemoveBalloon(first.ObjectId));
        page.GetObject(text.ObjectId).IsLocked = false;
        page.AttachedSymbols.Single().IsLocked = true;
        Assert.IsFalse(page.RemoveBalloon(first.ObjectId));
        Assert.IsTrue(page.ContainsBalloon(first.ObjectId));
        Assert.AreEqual(1, page.BalloonMerges.Count);

        page.AttachedSymbols.Single().IsLocked = false;
        Assert.IsTrue(page.RemoveBalloon(first.ObjectId));
        Assert.IsFalse(page.ContainsBalloon(first.ObjectId));
        Assert.AreEqual(0, page.BalloonMerges.Count);
        Assert.IsTrue(page.ContainsObject(text.ObjectId));
        Assert.IsTrue(page.ContainsObject(symbol.ObjectId));
    }

    [TestMethod]
    public void PrimaryVisibilityControlsBothRenderingAndExactHit()
    {
        RunOnSta(() =>
        {
            var primary = CreateBalloon(0, 0, BalloonShapeKind.Rectangle);
            primary.Bounds = new Rect(0, 0, 100, 80);
            var secondary = CreateBalloon(180, 0, BalloonShapeKind.Rectangle);
            secondary.Bounds = new Rect(0, 0, 100, 80);
            var merge = Merge(primary, secondary);
            var visual = new BalloonMergeVisual(merge, new[] { primary, secondary });
            visual.Measure(new Size(400, 200));
            visual.Arrange(new Rect(0, 0, visual.DesiredSize.Width, visual.DesiredSize.Height));
            var secondaryLocal = new Point(230 - Canvas.GetLeft(visual), 40 - Canvas.GetTop(visual));

            primary.IsVisible = false;
            secondary.IsVisible = true;
            visual.ApplyData(merge, new[] { primary, secondary });
            Assert.IsFalse(visual.IsHitTestVisible);
            Assert.IsFalse(visual.ContainsLocalPoint(secondaryLocal), "Hidden primary must not leave a ghost hit.");

            primary.IsVisible = true;
            secondary.IsVisible = false;
            visual.ApplyData(merge, new[] { primary, secondary });
            Assert.IsTrue(visual.IsHitTestVisible);
            Assert.IsTrue(visual.ContainsLocalPoint(secondaryLocal),
                "Primary visibility governs the complete primary-style composition.");
            visual.Measure(new Size(400, 200));
            visual.Arrange(new Rect(0, 0, visual.DesiredSize.Width, visual.DesiredSize.Height));
            Assert.IsTrue(FlattenGeometryDrawings(VisualTreeHelper.GetDrawing(visual)).Any());
        });
    }

    [TestMethod]
    public void ModelAndLiveGroupMoveRejectOverflowAndLateSymbolFailureAtomically()
    {
        var text = new MojiData { FullText = "原子移動", X = 60, Y = 70 };
        var first = CreateBalloon(20, 30, BalloonShapeKind.Ellipse);
        first.Tail = new BalloonTailData { TipX = 80, TipY = 220, RootParameter = .5, Width = 24 };
        first.TextLink = new TextLinkData { TextObjectId = text.ObjectId };
        var second = CreateBalloon(double.MaxValue, 40, BalloonShapeKind.Rectangle);
        var symbol = new AttachedSymbolData { ParentId = text.ObjectId, Text = "※", GraphemeAnchor = 0, OffsetX = .5 };
        var page = new PageDocument(Guid.NewGuid(), "overflow", new CanvasData(),
            new[] { text }, new[] { first, second }, new[] { symbol });
        page.MergeBalloons(first.ObjectId, second.ObjectId);
        var beforeFirst = page.GetBalloon(first.ObjectId).Clone();
        var beforeSecond = page.GetBalloon(second.ObjectId).Clone();
        var beforeText = page.GetObject(text.ObjectId).Clone();
        var beforeSymbol = page.AttachedSymbols.Single().Clone();

        Assert.ThrowsException<InvalidDataException>(() =>
            page.MoveBalloonMerge(first.ObjectId, double.MaxValue, 1));
        AssertBalloonEqual(beforeFirst, page.GetBalloon(first.ObjectId));
        AssertBalloonEqual(beforeSecond, page.GetBalloon(second.ObjectId));
        Assert.AreEqual(beforeText.X, page.GetObject(text.ObjectId).X);
        Assert.AreEqual(beforeText.Y, page.GetObject(text.ObjectId).Y);
        Assert.AreEqual(beforeSymbol.OffsetX, page.AttachedSymbols.Single().OffsetX);

        page.GetBalloon(second.ObjectId).X = 200;
        page.AttachedSymbols.Single().OffsetX = double.PositiveInfinity;
        beforeFirst = page.GetBalloon(first.ObjectId).Clone();
        beforeSecond = page.GetBalloon(second.ObjectId).Clone();
        beforeText = page.GetObject(text.ObjectId).Clone();
        Assert.ThrowsException<InvalidDataException>(() => page.MoveBalloonMerge(second.ObjectId, 25, -10));
        AssertBalloonEqual(beforeFirst, page.GetBalloon(first.ObjectId));
        AssertBalloonEqual(beforeSecond, page.GetBalloon(second.ObjectId));
        Assert.AreEqual(beforeText.X, page.GetObject(text.ObjectId).X);
        Assert.AreEqual(beforeText.Y, page.GetObject(text.ObjectId).Y);

        page.AttachedSymbols.Single().OffsetX = .5;
        page.GetBalloon(second.ObjectId).X = double.MaxValue;
        RunOnSta(() =>
        {
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.RestoreViewState(100, first.ObjectId);
            var notifications = 0;
            editor.ContentChanged += (_, _) => notifications++;
            Assert.IsTrue(editor.BeginBalloonGesture(first.ObjectId, new Point(0, 0)));
            Assert.IsFalse(editor.UpdateBalloonGesture(new Point(double.MaxValue, 0)));
            Assert.IsFalse(editor.IsBalloonGestureActive);
            Assert.AreEqual(beforeFirst.X,
                editor.BalloonVisuals.Single(item => item.ObjectId == first.ObjectId).BalloonData.X);
            Assert.AreEqual(double.MaxValue,
                editor.BalloonVisuals.Single(item => item.ObjectId == second.ObjectId).BalloonData.X);
            Assert.AreEqual(0, notifications);
            StringAssert.Contains(((TextBlock)editor.FindName("BalloonStatusTextBlock")!).Text, "取り消しました");
        });
    }

    [TestMethod]
    public void EveryMemberCanInitiateAllFourRealZButtonsAndCanvasFollowsDocument()
    {
        RunOnSta(() =>
        {
            var operations = new[]
            {
                ("BringToFrontButton", ObjectOrderOperation.BringToFront, true),
                ("BringForwardButton", ObjectOrderOperation.BringForward, true),
                ("SendBackwardButton", ObjectOrderOperation.SendBackward, false),
                ("SendToBackButton", ObjectOrderOperation.SendToBack, false),
            };
            foreach (var memberIndex in new[] { 0, 1 })
            foreach (var (buttonName, _, movesToFront) in operations)
            {
                var back = new MojiData { FullText = "背面", ZIndex = 0 };
                var first = CreateBalloon(40, 40, BalloonShapeKind.Ellipse);
                first.ZIndex = 1;
                var second = CreateBalloon(150, 40, BalloonShapeKind.Rectangle);
                second.ZIndex = 2;
                var front = new MojiData { FullText = "前面", ZIndex = 3 };
                var page = new PageDocument("Z", new[] { back, front }, new[] { first, second });
                page.MergeBalloons(first.ObjectId, second.ObjectId);
                using var editor = new PageEditorControl();
                editor.BindPage(page, null);
                var selected = memberIndex == 0 ? first.ObjectId : second.ObjectId;
                editor.RestoreViewState(100, selected);
                var button = (Button)editor.FindName(buttonName)!;
                Assert.IsTrue(button.IsEnabled, $"{buttonName}/{memberIndex}");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                var block = page.GetObjectOrderBlock(selected);
                CollectionAssert.AreEqual(new[] { first.ObjectId, second.ObjectId }, block.ToArray());
                var groupMinimumZ = Math.Min(page.GetBalloon(first.ObjectId).ZIndex, page.GetBalloon(second.ObjectId).ZIndex);
                var mergeVisual = editor.BalloonMergeVisuals.Single();
                Assert.AreEqual(groupMinimumZ, Canvas.GetZIndex(mergeVisual));
                var canvasObjects = editor.Canvas.Children.Cast<UIElement>()
                    .Where(item => item is MojiPanel or BalloonMergeVisual)
                    .ToArray();
                var mergeIndex = Array.IndexOf(canvasObjects, mergeVisual);
                var backIndex = Array.FindIndex(canvasObjects,
                    item => item is MojiPanel panel && panel.MojiData.ObjectId == back.ObjectId);
                var frontIndex = Array.FindIndex(canvasObjects,
                    item => item is MojiPanel panel && panel.MojiData.ObjectId == front.ObjectId);
                if (movesToFront)
                {
                    Assert.IsTrue(mergeIndex > backIndex && mergeIndex > frontIndex, $"{buttonName}/{memberIndex}");
                    Assert.AreEqual(2, groupMinimumZ);
                }
                else
                {
                    Assert.IsTrue(mergeIndex < backIndex && mergeIndex < frontIndex, $"{buttonName}/{memberIndex}");
                    Assert.AreEqual(0, groupMinimumZ);
                }
            }
        });
    }

    [TestMethod]
    public void MergeVisualRendersEveryMemberTailWithPrimaryStyle()
    {
        RunOnSta(() =>
        {
            var members = new[]
            {
                CreateBalloon(20, 20, BalloonShapeKind.Ellipse),
                CreateBalloon(130, 25, BalloonShapeKind.Rectangle),
                CreateBalloon(250, 30, BalloonShapeKind.Monologue),
            };
            members[0].Fill = Colors.Gold;
            members[0].Stroke = Colors.DarkViolet;
            members[0].StrokeThickness = 7;
            members[1].Fill = Colors.Red;
            members[2].Stroke = Colors.Green;
            for (var i = 0; i < members.Length; i++)
            {
                members[i].Bounds = new Rect(0, 0, 140, 90);
                members[i].Tail = new BalloonTailData
                {
                    TipX = members[i].X + 45,
                    TipY = 190 + i * 20,
                    RootParameter = .5,
                    Width = 24 + i,
                };
            }
            var merge = Merge(members);
            var visual = new BalloonMergeVisual(merge, members);
            visual.Measure(new Size(600, 400));
            visual.Arrange(new Rect(0, 0, visual.DesiredSize.Width, visual.DesiredSize.Height));
            var drawings = FlattenGeometryDrawings(VisualTreeHelper.GetDrawing(visual)).ToArray();

            Assert.AreEqual(3, visual.Geometry.Tails.Count);
            Assert.AreEqual(4, drawings.Length, "Three tails and one union body must be rendered together.");
            Assert.IsTrue(drawings.All(item => item.Brush is SolidColorBrush brush && brush.Color == Colors.Gold));
            Assert.IsTrue(drawings.All(item => item.Pen?.Brush is SolidColorBrush brush &&
                brush.Color == Colors.DarkViolet && item.Pen.Thickness == 7));
            foreach (var tail in visual.Geometry.Tails)
            {
                var point = new Point(tail.Bounds.X + tail.Bounds.Width / 2, tail.Bounds.Y + tail.Bounds.Height / 2);
                Assert.IsTrue(visual.Geometry.Contains(point, members[0].StrokeThickness));
            }
        });
    }

    [TestMethod]
    public void MergeUiValidationFailureRestoresDocumentLiveCanvasSelectionAndNotification()
    {
        RunOnSta(() =>
        {
            var first = CreateBalloon(20, 20, BalloonShapeKind.Ellipse);
            var second = CreateBalloon(150, 20, BalloonShapeKind.Rectangle);
            var invalid = CreateBalloon(300, 20, BalloonShapeKind.Monologue);
            invalid.Tail = new BalloonTailData { TipX = 350, TipY = 200, RootParameter = .5, Width = 24 };
            var page = new PageDocument("rollback", new[] { first, second, invalid });
            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            editor.RestoreViewState(100, first.ObjectId);
            editor.BalloonVisuals.Single(item => item.ObjectId == invalid.ObjectId)
                .BalloonData.Tail!.RootParameter = 2;
            var beforeOrder = page.AllObjects.Select(item => item.ObjectId).ToArray();
            var beforeCanvas = editor.Canvas.Children.Cast<UIElement>()
                .Where(item => item is BalloonVisual or BalloonMergeVisual or MojiPanel or AttachedSymbolVisual)
                .ToArray();
            var notifications = 0;
            editor.ContentChanged += (_, _) => notifications++;
            var combo = (ComboBox)editor.FindName("BalloonMergeCandidateComboBox")!;
            combo.SelectedIndex = 0;
            ((Button)editor.FindName("MergeBalloonButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual(0, page.BalloonMerges.Count);
            CollectionAssert.AreEqual(beforeOrder, page.AllObjects.Select(item => item.ObjectId).ToArray());
            Assert.AreEqual(first.ObjectId, editor.SelectedBalloonId);
            Assert.AreEqual(2d, editor.BalloonVisuals.Single(item => item.ObjectId == invalid.ObjectId)
                .BalloonData.Tail!.RootParameter);
            CollectionAssert.AreEqual(beforeCanvas, editor.Canvas.Children.Cast<UIElement>()
                .Where(item => item is BalloonVisual or BalloonMergeVisual or MojiPanel or AttachedSymbolVisual)
                .ToArray());
            Assert.AreEqual(0, notifications);
            Assert.AreEqual("フキダシ合体に失敗しました。",
                ((TextBlock)editor.FindName("BalloonStatusTextBlock")!).Text);
        });
    }

    [TestMethod]
    public void UndoRedoRebindRemoveAndDisposeDoNotLeakMergeVisualsOrHandlers()
    {
        RunOnSta(() =>
        {
            var first = CreateBalloon(20, 20, BalloonShapeKind.Ellipse);
            var second = CreateBalloon(150, 20, BalloonShapeKind.Rectangle);
            var page = new PageDocument("lifecycle", new[] { first, second });
            using var session = new ProjectSession(new ProjectDocument(Guid.NewGuid(), "lifecycle", new[] { page }));
            Assert.IsTrue(BalloonMergeCommands.Merge(session, page.PageId, first.ObjectId, second.ObjectId));
            var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            Assert.AreEqual(1, editor.BalloonMergeVisuals.Count);
            Assert.IsTrue(session.Undo());
            editor.BindPage(session.ActivePage!, null);
            Assert.AreEqual(0, editor.BalloonMergeVisuals.Count);
            Assert.IsTrue(session.Redo());
            editor.BindPage(session.ActivePage!, null);
            Assert.AreEqual(1, editor.BalloonMergeVisuals.Count);
            editor.RestoreViewState(100, first.ObjectId);
            Assert.IsTrue(editor.RemoveBalloon(first.ObjectId));
            Assert.AreEqual(0, session.ActivePage!.BalloonMerges.Count);
            Assert.AreEqual(0, editor.BalloonMergeVisuals.Count);

            var replacementFirst = CreateBalloon(30, 30, BalloonShapeKind.Ellipse);
            var replacementSecond = CreateBalloon(160, 30, BalloonShapeKind.Rectangle);
            var replacement = new PageDocument("dispose", new[] { replacementFirst, replacementSecond });
            replacement.MergeBalloons(replacementFirst.ObjectId, replacementSecond.ObjectId);
            editor.BindPage(replacement, null);
            var detachedVisual = editor.BalloonMergeVisuals.Single();
            editor.Dispose();
            Assert.AreEqual(0, editor.BalloonMergeVisuals.Count);
            Assert.AreEqual(0, editor.Canvas.Children.OfType<BalloonMergeVisual>().Count());
            var mouseDown = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseDownEvent,
            };
            detachedVisual.RaiseEvent(mouseDown);
            Assert.IsFalse(editor.IsBalloonGestureActive, "Disposed editor handler must be detached.");
        });
    }

    [TestMethod]
    public void LifecycleAndPerformanceKeepOneVisualPerGroupAndBoundCaches()
    {
        RunOnSta(() =>
        {
            var balloons = Enumerable.Range(0, 48)
                .Select(i => CreateBalloon((i % 8) * 110, (i / 8) * 90,
                    (BalloonShapeKind)(i % 4)))
                .ToArray();
            var page = new PageDocument("性能", balloons);
            for (var i = 0; i < balloons.Length; i += 2)
                page.MergeBalloons(balloons[i].ObjectId, balloons[i + 1].ObjectId);
            var factory = new BalloonMergeGeometryFactory(64);
            var singleSamples = new List<double>();
            var batch = Stopwatch.StartNew();
            for (var pass = 0; pass < 100; pass++)
            {
                foreach (var merge in page.BalloonMerges)
                {
                    var timer = Stopwatch.StartNew();
                    var geometry = factory.Create(merge, merge.MemberIds.Select(page.GetBalloon));
                    geometry.Contains(geometry.Bounds.Location + new Vector(1, 1), 2);
                    timer.Stop();
                    singleSamples.Add(timer.Elapsed.TotalMilliseconds);
                }
            }
            batch.Stop();
            var ordered = singleSamples.OrderBy(value => value).ToArray();
            var p95 = ordered[(int)Math.Floor((ordered.Length - 1) * .95)];
            TestContext?.WriteLine($"TASK-150 geometry refresh/hit: p95={p95:F3}ms, 24 groups x 100={batch.Elapsed.TotalMilliseconds:F3}ms");
            Assert.IsTrue(p95 < 50, $"p95 was {p95:F3}ms");
            Assert.IsTrue(batch.Elapsed.TotalMilliseconds < 3000,
                $"Debug safety batch was {batch.Elapsed.TotalMilliseconds:F3}ms");
            Assert.IsTrue(factory.CacheCount <= factory.CacheCapacity);

            using var editor = new PageEditorControl();
            editor.BindPage(page, null);
            Assert.AreEqual(24, editor.BalloonMergeVisuals.Count);
            Assert.AreEqual(24, editor.Canvas.Children.OfType<BalloonMergeVisual>().Count());
            var secondPage = new PageDocument("切替", new[]
            {
                CreateBalloon(0, 0, BalloonShapeKind.Ellipse),
                CreateBalloon(80, 0, BalloonShapeKind.Rectangle),
            });
            secondPage.MergeBalloons(secondPage.Balloons[0].ObjectId, secondPage.Balloons[1].ObjectId);
            editor.BindPage(secondPage, null);
            Assert.AreEqual(1, editor.BalloonMergeVisuals.Count);
            Assert.AreEqual(1, editor.Canvas.Children.OfType<BalloonMergeVisual>().Count());
            editor.UnbindPage();
            Assert.AreEqual(0, editor.BalloonMergeVisuals.Count);
            Assert.AreEqual(0, editor.Canvas.Children.OfType<BalloonMergeVisual>().Count());
        });
    }

    private static BalloonData CreateBalloon(double x, double y, BalloonShapeKind shape)
        => new()
        {
            X = x,
            Y = y,
            Bounds = new Rect(0, 0, 180, 110),
            ShapeKind = shape,
            Fill = Colors.White,
            Stroke = Colors.Black,
            StrokeThickness = 2,
        };

    private static BalloonMergeData Merge(params BalloonData[] balloons)
        => new()
        {
            PrimaryBalloonId = balloons[0].ObjectId,
            MemberIds = balloons.Select(item => item.ObjectId).ToList(),
        };

    private static IEnumerable<GeometryDrawing> FlattenGeometryDrawings(Drawing? drawing)
    {
        if (drawing is GeometryDrawing geometry)
        {
            yield return geometry;
            yield break;
        }
        if (drawing is not DrawingGroup group) yield break;
        foreach (var child in group.Children)
        foreach (var nested in FlattenGeometryDrawings(child))
            yield return nested;
    }

    private static void AssertBalloonEqual(BalloonData expected, BalloonData actual)
    {
        Assert.AreEqual(expected.ObjectId, actual.ObjectId);
        Assert.AreEqual(expected.ShapeKind, actual.ShapeKind);
        Assert.AreEqual(expected.X, actual.X);
        Assert.AreEqual(expected.Y, actual.Y);
        Assert.AreEqual(expected.Bounds, actual.Bounds);
        Assert.AreEqual(expected.Rotation, actual.Rotation);
        Assert.AreEqual(expected.Fill, actual.Fill);
        Assert.AreEqual(expected.Stroke, actual.Stroke);
        Assert.AreEqual(expected.StrokeThickness, actual.StrokeThickness);
        Assert.AreEqual(expected.Tail?.TailId, actual.Tail?.TailId);
        Assert.AreEqual(expected.Tail?.Tip, actual.Tail?.Tip);
        Assert.AreEqual(expected.Tail?.RootParameter, actual.Tail?.RootParameter);
        Assert.AreEqual(expected.Tail?.Width, actual.Tail?.Width);
        Assert.AreEqual(expected.TextLink?.TextObjectId, actual.TextLink?.TextObjectId);
        Assert.AreEqual(expected.TextLink?.LayoutMode, actual.TextLink?.LayoutMode);
        Assert.AreEqual(expected.TextLink?.Padding, actual.TextLink?.Padding);
        Assert.AreEqual(expected.TextLink?.MinimumFontSize, actual.TextLink?.MinimumFontSize);
        Assert.AreEqual(expected.TextLink?.Alignment, actual.TextLink?.Alignment);
    }

    private static void AssertFinite(Rect value)
    {
        Assert.IsFalse(value.IsEmpty);
        Assert.IsFalse(double.IsNaN(value.X) || double.IsInfinity(value.X));
        Assert.IsFalse(double.IsNaN(value.Y) || double.IsInfinity(value.Y));
        Assert.IsFalse(double.IsNaN(value.Width) || double.IsInfinity(value.Width));
        Assert.IsFalse(double.IsNaN(value.Height) || double.IsInfinity(value.Height));
    }

    private static void RewriteManifestVersion(string path, string version, string minimum)
    {
        RewriteEntry(path, VersionedProjectFormat.ManifestEntryName, document =>
        {
            document.Root!.Element("FormatVersion")!.Value = version;
            document.Root.Element("MinimumReaderVersion")!.Value = minimum;
        });
    }

    private static void RemoveMergeCollection(string path, Guid pageId)
        => RewriteEntry(path, VersionedProjectFormat.CanonicalPagePath(pageId),
            document => document.Root!.Element("BalloonMerges")?.Remove());

    private static void CorruptMergeWithMissingMember(string path, Guid pageId)
        => RewriteEntry(path, VersionedProjectFormat.CanonicalPagePath(pageId), document =>
        {
            var member = document.Descendants("BalloonId").Last();
            member.Value = Guid.NewGuid().ToString("D");
        });

    private static void RewriteEntry(string path, string entryName, Action<XDocument> mutation)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        var entry = archive.GetEntry(entryName)!;
        XDocument document;
        using (var stream = entry.Open()) document = XDocument.Load(stream);
        mutation(document);
        entry.Delete();
        var replacement = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(replacement.Open());
        document.Save(writer);
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
        if (failure != null) throw failure;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;
        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"task150-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
