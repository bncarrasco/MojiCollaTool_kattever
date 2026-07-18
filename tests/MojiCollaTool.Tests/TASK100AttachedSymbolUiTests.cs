using System;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class TASK100AttachedSymbolUiTests
{
    [TestMethod]
    public void PageEditorBuildsOneTextVisualPerGraphemeCluster()
    {
        RunOnSta(() =>
        {
            using var editor = new PageEditorControl();
            var page = new PageDocument("書記素", new[]
            {
                new MojiData { FullText = "A😀e\u0301👩\u200d👩" },
            });
            editor.BindPage(page, null);

            var panel = editor.MojiPanels[0];
            Assert.AreEqual(4, panel.GraphemeVisuals.Count);
            Assert.AreEqual("😀", panel.GraphemeVisuals[1].GraphemeText);
            Assert.AreEqual("e\u0301", panel.GraphemeVisuals[2].GraphemeText);
            Assert.AreEqual("👩\u200d👩", panel.GraphemeVisuals[3].GraphemeText);
        });
    }

    [TestMethod]
    public void AttachedSymbolUiCaptureReanchorsAndSupportsUndoRedo()
    {
        RunOnSta(() =>
        {
            var document = new ProjectDocument("付加記号UI");
            var page = document.Pages[0];
            page.AddMojiData(new MojiData { FullText = "A😀B" });
            using var session = new ProjectSession(document);
            using var editor = new PageEditorControl();
            editor.BindPage(session.ActivePage!, null);
            editor.ContentChanged += (_, _) =>
            {
                editor.CapturePage();
                session.MarkChanged(session.ActivePage!.PageId, editor.ContentChangeDescription,
                    editor.ContentChangeCoalesceKey);
            };

            var parentId = editor.MojiPanels[0].MojiData.ObjectId;
            var visual = editor.AddAttachedSymbol(parentId, 1, "!?");
            Assert.AreEqual("😀", visual.SymbolData.AnchorText);
            Assert.AreEqual(1, session.ActivePage!.AttachedSymbols.Count);

            editor.MojiPanels[0].MojiData.FullText = "X" + editor.MojiPanels[0].MojiData.FullText;
            editor.MojiPanels[0].UpdateMojiView(false);
            editor.NotifyContentChanged("文字入力", parentId.ToString("D"));
            Assert.AreEqual(2, session.ActivePage!.AttachedSymbols[0].GraphemeAnchor);
            Assert.AreEqual(2, editor.AttachedSymbolVisuals[0].SymbolData.GraphemeAnchor);

            Assert.IsTrue(session.Undo());
            editor.BindPage(session.ActivePage!, null);
            Assert.AreEqual(1, editor.AttachedSymbolVisuals[0].SymbolData.GraphemeAnchor);
            Assert.IsTrue(session.Redo());
            editor.BindPage(session.ActivePage!, null);
            Assert.AreEqual(2, editor.AttachedSymbolVisuals[0].SymbolData.GraphemeAnchor);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) Assert.Fail(failure.ToString());
    }
}
