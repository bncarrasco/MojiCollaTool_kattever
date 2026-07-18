using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class UndoRedoTests
{
    [TestMethod]
    public void UndoAndRedoRestorePageStateAndKeepProjectIdentity()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("project"));
        var page = session.ActivePage!;
        var pageId = page.PageId;
        var projectId = session.ProjectId;

        session.ExecutePage(pageId, current => current.Rename("renamed"), "名前変更");
        Assert.IsTrue(session.CanUndo);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(projectId, session.ProjectId);
        Assert.AreEqual(pageId, session.ActivePage!.PageId);
        Assert.AreEqual("01", session.ActivePage.Name);
        Assert.IsTrue(session.CanRedo);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual("renamed", session.ActivePage.Name);
    }

    [TestMethod]
    public void SavedRevisionIsCleanOnlyWhenUndoReturnsToSavedState()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("project"));
        var page = session.ActivePage!;

        session.ExecutePage(page.PageId, current => current.Rename("保存地点"), "名前変更");
        session.MarkSaved();
        session.ExecutePage(page.PageId, current => current.Rename("編集後"), "名前変更");
        Assert.IsTrue(session.IsDirty);
        Assert.IsTrue(session.Undo());
        Assert.IsFalse(session.IsDirty);
        Assert.IsFalse(session.IsPageDirty(page.PageId));
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(session.IsDirty);
        Assert.IsTrue(session.IsPageDirty(page.PageId));
    }

    [TestMethod]
    public void TransactionIsOneOperationAndNewEditDropsRedoBranch()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("project"));
        var page = session.ActivePage!;

        using (session.BeginTransaction("まとめて編集"))
        {
            session.ExecutePage(page.PageId, current => current.Rename("one"), "名前変更");
            session.ExecutePage(page.PageId, current => current.Rename("two"), "名前変更");
        }

        Assert.AreEqual(1, session.History.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("01", session.ActivePage!.Name);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual("two", session.ActivePage!.Name);
        Assert.IsTrue(session.Undo());
        session.ExecutePage(page.PageId, current => current.Rename("分岐"), "名前変更");
        Assert.IsFalse(session.CanRedo);
    }

    [TestMethod]
    public void DuplicatedPageAssetsAreRestoredAtomically()
    {
        using var workspace = new ApplicationWorkspace();
        var project = new ProjectDocument("画像");
        var source = project.Pages[0];
        var bytes = new byte[] { 1, 2, 3, 4 };
        var store = new ProjectSessionAssetStore();
        store.SaveImages(project, new[] {
            new ProjectAssetRestore(source.PageId, 1, "png", bytes),
        });
        var session = workspace.Open(project, null, store);

        session.Execute(project => {
            var clone = project.ClonePage(source.PageId);
            session.AssetStore.CopyPageAssets(source, clone);
        }, "ページ複製");

        Assert.AreEqual(2, session.Document.PageCount);
        var clonePage = session.Document.Pages.Single(page => page.PageId != source.PageId);
        Assert.AreEqual(1, session.AssetStore.GetPageAssets(clonePage.PageId).Count);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(1, session.Document.PageCount);
        Assert.AreEqual(1, session.AssetStore.GetPageAssets(source.PageId).Count);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(2, session.Document.PageCount);
        Assert.AreEqual(1, session.AssetStore.GetPageAssets(session.Document.Pages[1].PageId).Count);
    }

    [TestMethod]
    public void PageDeletionAndOrderAreUndoable()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("ページ"));
        var first = session.ActivePage!;
        var second = session.Document.AddPage("02");
        session.MarkChanged(second.PageId, "ページ追加");

        session.Execute(project => project.RemovePage(second.PageId), "ページ削除");
        Assert.AreEqual(1, session.Document.PageCount);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(2, session.Document.PageCount);
        Assert.IsTrue(session.Document.ContainsPage(second.PageId));
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(1, session.Document.PageCount);
        Assert.AreEqual(first.PageId, session.ActivePage!.PageId);
    }

    [TestMethod]
    public void HistoryTrimKeepsEditingAvailableAfterByteLimit()
    {
        var document = new ProjectDocument("履歴上限");
        var history = new UndoRedoHistory(document, maxEntries: 2, maxBytes: 10000);
        for (var index = 0; index < 5; index++)
        {
            history.Execute(document, project => project.Rename($"編集{index}"), "名前変更");
        }

        Assert.IsTrue(history.Count <= 2);
        Assert.IsTrue(history.EstimatedBytes <= history.MaxBytes);
        Assert.IsTrue(history.CanUndo);
        history.Undo(document);
        history.Execute(document, project => project.Rename("継続編集"), "名前変更");
        Assert.IsFalse(history.CanRedo);
    }

    [TestMethod]
    public void RapidSamePageEditsCoalesceIntoOneHistoryEntry()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("入力"));
        var pageId = session.ActivePage!.PageId;
        session.MarkChanged(pageId, "文字入力");
        session.ExecutePage(pageId, page => page.Rename("連続入力"), "文字入力");

        Assert.AreEqual(1, session.History.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("01", session.ActivePage!.Name);
    }
}
