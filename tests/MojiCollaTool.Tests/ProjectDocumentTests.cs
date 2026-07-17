using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class ProjectDocumentTests
{
    [TestMethod]
    public void NewProjectStartsWithOneNamedPage()
    {
        var project = new ProjectDocument("漫画");

        Assert.AreEqual("漫画", project.Name);
        Assert.AreEqual(1, project.PageCount);
        Assert.AreEqual("01", project.Pages[0].Name);
        Assert.AreEqual(0, project.Pages[0].Order);
        Assert.AreNotEqual(Guid.Empty, project.ProjectId);
    }

    [TestMethod]
    public void AddRenameMoveAndRemovePagesNormalizeOrder()
    {
        var project = new ProjectDocument();
        var first = project.Pages[0];
        var second = project.AddPage();
        var third = project.AddPage("下書き");

        project.RenamePage(third.PageId, "完成稿");
        project.MovePage(third.PageId, 0);

        CollectionAssert.AreEqual(
            new[] { "完成稿", "01", "02" },
            project.Pages.Select(page => page.Name).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, project.Pages.Select(page => page.Order).ToArray());

        var removed = project.RemovePage(second.PageId);
        Assert.AreSame(second, removed);
        Assert.AreEqual(2, project.PageCount);
        Assert.AreEqual(first.PageId, project.Pages[1].PageId);
        CollectionAssert.AreEqual(new[] { 0, 1 }, project.Pages.Select(page => page.Order).ToArray());
    }

    [TestMethod]
    public void RemoveLastPageIsRejected()
    {
        var project = new ProjectDocument();

        Assert.ThrowsException<InvalidOperationException>(() => project.RemovePage(project.Pages[0].PageId));
    }

    [TestMethod]
    public void ClonePageUsesNewIdAndDeepCopiesLegacyData()
    {
        var project = new ProjectDocument();
        var source = project.Pages[0];
        source.Canvas.Image2LocatePosition = LocatePosition.Right;
        source.Canvas.CanvasWidth = 800;
        source.AddMojiData(new MojiData(17)
        {
            FullText = "縦書き 😀",
            TextDirection = TextDirection.Tategaki,
        });

        var clone = project.ClonePage(source.PageId, "複製");

        Assert.AreNotEqual(source.PageId, clone.PageId);
        Assert.AreEqual("複製", clone.Name);
        Assert.AreEqual(LocatePosition.Right, clone.Canvas.Image2LocatePosition);
        Assert.AreEqual(source.Canvas.CanvasWidth, clone.Canvas.CanvasWidth);
        Assert.AreEqual(17, clone.MojiDatas[0].Id);
        Assert.AreEqual("縦書き 😀", clone.MojiDatas[0].FullText);

        source.Canvas.CanvasWidth = 100;
        source.MojiDatas[0].FullText = "変更";

        Assert.AreEqual(800, clone.Canvas.CanvasWidth);
        Assert.AreEqual("縦書き 😀", clone.MojiDatas[0].FullText);
    }

    [TestMethod]
    public void LegacyAdapterImportsOnePageAndDoesNotShareReferences()
    {
        var canvas = new CanvasData
        {
            CanvasWidth = 640,
            Image2LocatePosition = LocatePosition.Bottom,
        };
        var moji = new MojiData(3)
        {
            FullText = "日本語\n😀",
            TextDirection = TextDirection.Tategaki,
        };

        var project = LegacyProjectDataAdapter.Import("旧形式", canvas, new[] { moji });

        Assert.AreEqual("旧形式", project.Name);
        Assert.AreEqual(1, project.PageCount);
        Assert.AreEqual("01", project.Pages[0].Name);
        Assert.AreEqual(LocatePosition.Bottom, project.Pages[0].Canvas.Image2LocatePosition);
        Assert.AreEqual("日本語\n😀", project.Pages[0].MojiDatas[0].FullText);

        canvas.CanvasWidth = 1;
        moji.FullText = "変更";

        Assert.AreEqual(640, project.Pages[0].Canvas.CanvasWidth);
        Assert.AreEqual("日本語\n😀", project.Pages[0].MojiDatas[0].FullText);
    }

    [TestMethod]
    public void LegacyAdapterExportReturnsDeepCopy()
    {
        var project = new ProjectDocument();
        project.Pages[0].Canvas.Image2LocatePosition = LocatePosition.Left;
        project.Pages[0].AddMojiData(new MojiData(1) { FullText = "保持" });

        var legacy = LegacyProjectDataAdapter.Export(project.Pages[0]);
        legacy.Canvas.Image2LocatePosition = LocatePosition.Top;
        legacy.MojiDatas[0].FullText = "変更";

        Assert.AreEqual(LocatePosition.Left, project.Pages[0].Canvas.Image2LocatePosition);
        Assert.AreEqual("保持", project.Pages[0].MojiDatas[0].FullText);
    }
}
