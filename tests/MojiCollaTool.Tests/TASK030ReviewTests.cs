using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class TASK030ReviewTests
{
    [TestMethod]
    public void CleanProjectAndPageSwitchDoesNotBecomeDirty()
    {
        using var workspace = new ApplicationWorkspace();
        var first = workspace.Open(new ProjectDocument("一"));
        var second = workspace.Open(new ProjectDocument("二"));
        var secondPage = second.Document.AddPage("二枚目");

        workspace.SetActiveSession(second);
        second.ActivatePage(secondPage.PageId);
        workspace.SetActiveSession(first);
        workspace.SetActiveSession(second);
        second.ActivatePage(secondPage.PageId);

        Assert.IsFalse(first.IsDirty);
        Assert.IsFalse(second.IsDirty);
        Assert.IsFalse(second.IsPageDirty(secondPage.PageId));
    }

    [TestMethod]
    public void AddingTextAndMojiWindowChangeRaiseDirtyNotification()
    {
        var result = RunOnSta(() =>
        {
            using var store = new ProjectSessionAssetStore();
            using var session = new ProjectSession(new ProjectDocument("文字"), assetStore: store);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, store, store);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(session.ActivePage!.PageId);
            };

            editor.AddNewMojiPanel();
            var afterAdd = session.IsDirty;
            session.MarkSaved();
            var panel = editor.MojiPanels.Single();
            panel.MojiWindow!.LoadMojiDataToWindow(panel.MojiData);
            var textBox = (TextBox)panel.MojiWindow!.GetType()
                .GetField("TextTextBox", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(panel.MojiWindow)!;
            textBox.Text = "変更後";

            return (afterAdd, session.IsDirty, session.IsPageDirty(session.ActivePage!.PageId));
        });

        Assert.IsTrue(result.afterAdd);
        Assert.IsTrue(result.Item2);
        Assert.IsTrue(result.Item3);
    }

    [TestMethod]
    public void CanvasEditChangeRaisesDirtyNotification()
    {
        var isDirty = RunOnSta(() =>
        {
            using var store = new ProjectSessionAssetStore();
            var project = new ProjectDocument("キャンバス");
            var page = project.Pages[0];
            page.Canvas.ImageData1 = new ImageData(20, 20);
            page.Canvas.ImageData2 = new ImageData(10, 20);
            using var session = new ProjectSession(project, assetStore: store);
            using var editor = new PageEditorControl();
            editor.BindPage(page, store, store);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(page.PageId);
            };
            session.MarkSaved();

            var canvasEditor = new CanvasEditWindow(editor.CanvasData, editor);
            var button = new Button { Tag = "Right" };
            typeof(CanvasEditWindow).GetMethod("Image2LocateButton", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(canvasEditor, new object[] { button, new RoutedEventArgs() });
            canvasEditor.Close();
            return session.IsDirty;
        });

        Assert.IsTrue(isDirty);
    }

    [TestMethod]
    public void MojiDragCompletionRaisesDirtyNotification()
    {
        var isDirty = RunOnSta(() =>
        {
            using var store = new ProjectSessionAssetStore();
            using var session = new ProjectSession(new ProjectDocument("ドラッグ"), assetStore: store);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, store, store);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(session.ActivePage!.PageId);
            };
            editor.AddNewMojiPanel();
            session.MarkSaved();
            var panel = editor.MojiPanels.Single();
            var up = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseUpEvent };
            typeof(MojiPanel).GetField("dragStart", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(panel, new Point(0, 0));
            typeof(MojiPanel).GetField("dragMoved", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(panel, true);
            typeof(MojiPanel).GetMethod("MojiPanel_MouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(panel, new object[] { panel, up });
            return session.IsDirty;
        });

        Assert.IsTrue(isDirty);
    }

    [TestMethod]
    public void RefreshTabsRestoresActiveProjectAndPageSelection()
    {
        var result = RunOnSta(() =>
        {
            var window = new MainWindow();
            var second = window.Workspace.Open(new ProjectDocument("二"));
            var thirdPage = second.Document.AddPage("03");
            second.Document.AddPage("04");
            second.ActivatePage(thirdPage.PageId);
            window.Workspace.SetActiveSession(second);

            var projectTabs = (TabControl)typeof(MainWindow).GetField("ProjectTabs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            var pageTabs = (TabControl)typeof(MainWindow).GetField("PageTabs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            var projectSelection = ((TabItem)projectTabs.SelectedItem!).Tag;
            var pageSelection = ((TabItem)pageTabs.SelectedItem!).Tag;
            window.Close();
            return (ReferenceEquals(projectSelection, second), ReferenceEquals(pageSelection, thirdPage));
        });

        Assert.IsTrue(result.Item1);
        Assert.IsTrue(result.Item2);
    }

    [TestMethod]
    public void CorruptImagePreparationLeavesOriginalAssetAndMetadata()
    {
        using var store = new ProjectSessionAssetStore();
        var project = new ProjectDocument("画像");
        var page = project.Pages[0];
        page.Canvas.ImageData1 = new ImageData(640, 480);
        page.Canvas.ImageData2 = new ImageData(320, 240);
        var original = new byte[] { 1, 2, 3, 4 };
        var originalSecond = new byte[] { 5, 6, 7, 8 };
        store.SaveImages(project, new[]
        {
            new ProjectAssetRestore(page.PageId, 1, "bin", original),
            new ProjectAssetRestore(page.PageId, 2, "bin", originalSecond),
        });
        var corruptPath = Path.Combine(Path.GetTempPath(), $"mct-corrupt-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(corruptPath, new byte[] { 0, 1, 2 });
        try
        {
            Assert.ThrowsException<InvalidDataException>(() => store.PrepareImage(corruptPath));
            using var asset = store.OpenImage(page, 1);
            using var content = new MemoryStream();
            asset!.Content.CopyTo(content);
            CollectionAssert.AreEqual(original, content.ToArray());
            Assert.AreEqual(640, page.Canvas.ImageData1.OriginalWidth);
            Assert.AreEqual(480, page.Canvas.ImageData1.OriginalHeight);
            using var secondAsset = store.OpenImage(page, 2);
            using var secondContent = new MemoryStream();
            secondAsset!.Content.CopyTo(secondContent);
            CollectionAssert.AreEqual(originalSecond, secondContent.ToArray());
            Assert.AreEqual(320, page.Canvas.ImageData2.OriginalWidth);
            Assert.AreEqual(240, page.Canvas.ImageData2.OriginalHeight);
        }
        finally
        {
            File.Delete(corruptPath);
        }
    }

    [TestMethod]
    public void TwoProjectsAndThreePagesKeepAssetsAndDirtyStateSeparated()
    {
        using var workspace = new ApplicationWorkspace();
        var first = workspace.Open(new ProjectDocument("一"));
        var second = workspace.Open(new ProjectDocument("二"));
        var firstPages = new[] { first.Document.Pages[0], first.Document.AddPage("02"), first.Document.AddPage("03") };
        var secondPages = new[] { second.Document.Pages[0], second.Document.AddPage("02"), second.Document.AddPage("03") };

        first.AssetStore.SaveImages(first.Document, new[] { new ProjectAssetRestore(firstPages[1].PageId, 1, "png", new byte[] { 1 }) });
        second.AssetStore.SaveImages(second.Document, new[] { new ProjectAssetRestore(secondPages[1].PageId, 1, "png", new byte[] { 2 }) });
        second.ActivatePage(secondPages[1].PageId);
        second.MarkChanged(secondPages[1].PageId);
        workspace.SetActiveSession(second);

        Assert.AreEqual(3, first.Document.PageCount);
        Assert.AreEqual(3, second.Document.PageCount);
        Assert.IsFalse(first.IsDirty);
        Assert.IsTrue(second.IsDirty);
        Assert.AreEqual(2, ReadAssetByte(second.AssetStore.OpenImage(secondPages[1], 1)!));
        Assert.AreEqual(1, ReadAssetByte(first.AssetStore.OpenImage(firstPages[1], 1)!));
        Assert.AreSame(secondPages[1], workspace.ActivePage);
    }

    [TestMethod]
    public void SaveSuccessClearsDirtyAndCloseRejectsDirtySession()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("保存"));
        session.MarkChanged();
        Assert.IsFalse(workspace.CloseSession(session));
        Assert.AreEqual(1, workspace.SessionCount);

        session.MarkSaved();
        Assert.IsFalse(session.IsDirty);
        Assert.IsTrue(workspace.CloseSession(session));
    }

    private static int ReadAssetByte(ProjectImageAsset asset)
    {
        using (asset)
        {
            return asset.Content.ReadByte();
        }
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null) Assert.Fail(exception.ToString());
        return result!;
    }
}
