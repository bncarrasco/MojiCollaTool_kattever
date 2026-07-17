using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class PageEditorLifecycleTests
{
    [TestMethod]
    public void MojiWindowSurvivesTemporaryUnloadAndCanBeRecreated()
    {
        var result = RunOnSta(() =>
        {
            using var page = new PageEditorControl();
            var panel = new MojiPanel(1, page);
            page.AddMojiPanel(panel);
            var originalWindow = panel.MojiWindow;
            var created = originalWindow != null;

            panel.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            var retainedAfterUnload = ReferenceEquals(originalWindow, panel.MojiWindow);

            originalWindow!.IsHideOnly = false;
            originalWindow.Close();
            panel.ShowMojiWindow();
            var recreatedAfterClose = panel.MojiWindow != null &&
                                      !ReferenceEquals(originalWindow, panel.MojiWindow);

            page.Dispose();
            var releasedAfterDispose = panel.MojiWindow == null;
            return (created, retainedAfterUnload, recreatedAfterClose, releasedAfterDispose);
        });

        Assert.IsTrue(result.created);
        Assert.IsTrue(result.retainedAfterUnload);
        Assert.IsTrue(result.recreatedAfterClose);
        Assert.IsTrue(result.releasedAfterDispose);
    }

    [TestMethod]
    public void CanvasEditWindowReleasesPageReferenceWhenClosed()
    {
        var referenceReleased = RunOnSta(() =>
        {
            using var page = new PageEditorControl();
            var editor = new CanvasEditWindow(page.CanvasData, page);
            var field = typeof(PageEditorControl).GetField("_canvasEditWindow", BindingFlags.Instance | BindingFlags.NonPublic);
            field!.SetValue(page, editor);

            editor.Close();
            page.Dispose();
            return field.GetValue(page) == null;
        });

        Assert.IsTrue(referenceReleased);
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null) Assert.Fail(exception.ToString());
        return result!;
    }
}
