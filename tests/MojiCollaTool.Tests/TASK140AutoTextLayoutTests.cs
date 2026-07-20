using System;
using System.Linq;
using System.Threading;
using System.Windows;
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
