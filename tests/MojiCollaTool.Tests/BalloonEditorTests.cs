using System;
using System.Threading;
using MojiCollaTool;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class BalloonEditorTests
{
    [TestMethod]
    public void AddAndCaptureBalloonRoundTripsThroughPageEditor()
    {
        RunOnSta(() =>
        {
            var page = new PageDocument("01");
            using var editor = new PageEditorControl();
            editor.BindPage(page, assetSource: null);

            var visual = editor.AddNewBalloon(BalloonShapeKind.Monologue);
            visual.BalloonData.X = 75;
            visual.Refresh();
            editor.CapturePage();

            Assert.AreEqual(1, page.Balloons.Count);
            Assert.AreEqual(BalloonShapeKind.Monologue, page.Balloons[0].ShapeKind);
            Assert.AreEqual(75, page.Balloons[0].X);
        });
    }

    [TestMethod]
    public void DisposeReleasesBalloonVisualsAndSelection()
    {
        RunOnSta(() =>
        {
            using var editor = new PageEditorControl();
            editor.AddNewBalloon();
            Assert.AreEqual(1, editor.BalloonVisuals.Count);
            Assert.IsNotNull(editor.SelectedBalloonId);

            editor.Dispose();

            Assert.AreEqual(0, editor.BalloonVisuals.Count);
            Assert.IsNull(editor.SelectedBalloonId);
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
