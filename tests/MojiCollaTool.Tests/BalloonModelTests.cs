using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests
{
    [TestClass]
    public class BalloonModelTests
    {
        [TestMethod]
        public void BalloonPreservesShapeStyleTransformAndRelationships()
        {
            var text = new MojiData { FullText = "縦書き😀" };
            var balloon = new BalloonData
            {
                ShapeKind = BalloonShapeKind.RoundedRectangle,
                X = 12.5,
                Y = -3,
                Rotation = 15,
                Bounds = new System.Windows.Rect(2, 3, 180, 90),
                Fill = System.Windows.Media.Colors.LightYellow,
                Stroke = System.Windows.Media.Colors.DarkBlue,
                StrokeThickness = 4,
                Tail = new BalloonTailData { TipX = 20, TipY = 150, RootParameter = 0.25, Width = 18 },
                TextLink = new TextLinkData { TextObjectId = text.ObjectId, Padding = 6, MinimumFontSize = 12 },
            };

            var page = new PageDocument("01", new[] { text }, new[] { balloon });

            Assert.AreEqual(2, page.ObjectCount);
            Assert.AreEqual(0, page.GetDocumentObject(text.ObjectId).ZIndex);
            Assert.AreEqual(1, page.GetBalloon(balloon.ObjectId).ZIndex);
            Assert.AreEqual(text.ObjectId, page.GetBalloon(balloon.ObjectId).TextLink!.TextObjectId);
            Assert.AreEqual(BalloonShapeKind.RoundedRectangle, page.GetBalloon(balloon.ObjectId).ShapeKind);
        }

        [TestMethod]
        public void ClonePageRemapsBalloonAndTextRelationships()
        {
            var text = new MojiData { FullText = "本文" };
            var balloon = new BalloonData
            {
                Tail = new BalloonTailData { TipX = 10, TipY = 20 },
                TextLink = new TextLinkData { TextObjectId = text.ObjectId },
            };
            var source = new PageDocument("01", new[] { text }, new[] { balloon });

            var clone = source.Clone();

            Assert.AreNotEqual(source.PageId, clone.PageId);
            Assert.AreNotEqual(text.ObjectId, clone.Objects[0].ObjectId);
            Assert.AreNotEqual(balloon.ObjectId, clone.Balloons[0].ObjectId);
            Assert.AreEqual(clone.Objects[0].ObjectId, clone.Balloons[0].TextLink!.TextObjectId);
            Assert.AreNotEqual(balloon.Tail?.TailId, clone.Balloons[0].Tail?.TailId);
        }

        [TestMethod]
        public void RemovingLinkedTextDetachesLinkButKeepsBalloon()
        {
            var text = new MojiData();
            var balloon = new BalloonData { TextLink = new TextLinkData { TextObjectId = text.ObjectId } };
            var page = new PageDocument("01", new[] { text }, new[] { balloon });

            Assert.IsTrue(page.RemoveMojiData(text));
            Assert.AreEqual(1, page.Balloons.Count);
            Assert.IsNull(page.Balloons[0].TextLink);
        }

        [TestMethod]
        public void UnknownShapeFallsBackWithoutRejectingPage()
        {
            var balloon = new BalloonData { ShapeKindValue = "FutureShape" };
            var page = new PageDocument("01", Array.Empty<MojiData>(), new[] { balloon });

            Assert.AreEqual(BalloonShapeKind.Unknown, page.Balloons[0].ShapeKind);
            Assert.AreEqual("FutureShape", page.Balloons[0].UnknownShapeKind);
        }

        [TestMethod]
        public void InvalidBalloonNumberIsRejectedBeforeUpdate()
        {
            var balloon = new BalloonData();
            var page = new PageDocument("01", Array.Empty<MojiData>(), new[] { balloon });

            Assert.ThrowsException<InvalidDataException>(() => page.UpdateBalloon(balloon.ObjectId, item => item.StrokeThickness = double.NaN));
            Assert.AreEqual(2, page.Balloons[0].StrokeThickness);
        }

        [TestMethod]
        public void BalloonCommandsAreUndoableAndRedoable()
        {
            using var session = new ProjectSession(new ProjectDocument("履歴"));
            var pageId = session.Document.Pages[0].PageId;
            var balloon = new BalloonData();

            BalloonCommands.Add(session, pageId, balloon);
            Assert.IsTrue(session.IsDirty);
            Assert.IsTrue(session.Document.GetPage(pageId).ContainsBalloon(balloon.ObjectId));
            Assert.IsTrue(session.Undo());
            Assert.IsFalse(session.Document.GetPage(pageId).ContainsBalloon(balloon.ObjectId));
            Assert.IsTrue(session.Redo());
            Assert.IsTrue(session.Document.GetPage(pageId).ContainsBalloon(balloon.ObjectId));
        }

        [TestMethod]
        public void MixedTextAndBalloonOrderSurvivesClone()
        {
            var page = new PageDocument("01");
            var balloon = new BalloonData();
            var text = new MojiData();
            page.AddBalloon(balloon);
            page.AddMojiData(text);

            var clone = page.Clone();

            Assert.AreEqual(balloon.ObjectId, page.AllObjects[0].ObjectId);
            Assert.AreEqual(text.ObjectId, page.AllObjects[1].ObjectId);
            Assert.AreEqual(0, clone.AllObjects[0].ZIndex);
            Assert.AreEqual(1, clone.AllObjects[1].ZIndex);
            Assert.AreEqual(clone.Balloons[0].ObjectId, clone.AllObjects[0].ObjectId);
        }

        [TestMethod]
        public void SetCollectionsPreserveExistingMixedOrder()
        {
            var page = new PageDocument("01");
            var firstBalloon = new BalloonData();
            var text = new MojiData { FullText = "本文" };
            var secondBalloon = new BalloonData();
            page.AddBalloon(firstBalloon);
            page.AddMojiData(text);
            page.AddBalloon(secondBalloon);

            page.SetMojiDatas(page.Objects.Select(item => item.Clone()));
            page.SetBalloons(page.Balloons.Select(item => item.Clone()));

            CollectionAssert.AreEqual(
                new[] { firstBalloon.ObjectId, text.ObjectId, secondBalloon.ObjectId },
                page.AllObjects.Select(item => item.ObjectId).ToArray());
        }

        [TestMethod]
        public void UpdateBalloonCannotChangeStableIdAndFailureKeepsModelAndOrder()
        {
            var page = new PageDocument("01");
            var first = new BalloonData { StrokeThickness = 3 };
            var text = new MojiData();
            page.AddBalloon(first);
            page.AddMojiData(text);
            var originalOrder = page.AllObjects.Select(item => item.ObjectId).ToArray();
            var originalStroke = page.Balloons[0].StrokeThickness;

            Assert.ThrowsException<InvalidOperationException>(() => page.UpdateBalloon(first.ObjectId, item =>
            {
                item.ObjectId = Guid.NewGuid();
                item.StrokeThickness = 99;
            }));

            CollectionAssert.AreEqual(originalOrder, page.AllObjects.Select(item => item.ObjectId).ToArray());
            Assert.AreEqual(first.ObjectId, page.Balloons[0].ObjectId);
            Assert.AreEqual(originalStroke, page.Balloons[0].StrokeThickness);
        }

        [TestMethod]
        public void BalloonIsIncludedInHistoryMemoryEstimateAndTrimRemainsUsable()
        {
            var page = new PageDocument("01");
            page.AddMojiData(new MojiData());
            var project = new ProjectDocument(Guid.NewGuid(), "履歴", new[] { page });
            var pageId = project.Pages[0].PageId;
            var textOnlyEstimate = ProjectHistoryState.Capture(project, new[] { pageId }).EstimateBytes();
            project.GetPage(pageId).AddBalloon(new BalloonData());
            var mixedEstimate = ProjectHistoryState.Capture(project, new[] { pageId }).EstimateBytes();

            Assert.IsTrue(mixedEstimate > textOnlyEstimate);

            var history = new UndoRedoHistory(project, maxEntries: 2, maxBytes: 120000);
            for (var index = 0; index < 6; index++)
            {
                history.Execute(project, document => document.GetPage(pageId).AddBalloon(new BalloonData()), "フキダシ追加");
            }

            Assert.IsTrue(history.UndoCount <= history.MaxEntries);
            Assert.IsTrue(history.EstimatedBytes <= history.MaxBytes);
            Assert.IsTrue(history.CanUndo);
            history.Undo(project);
            Assert.IsTrue(history.CanRedo);
            history.Redo(project);
        }
    }
}
