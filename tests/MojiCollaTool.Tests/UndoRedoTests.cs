using System;
using System.Linq;
using System.Reflection;
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

    [TestMethod]
    public void CoalescedRevisionRemainsDirtyAfterSaveThenNextEdit()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("revision"));
        var pageId = session.ActivePage!.PageId;

        session.ExecutePage(pageId, page => page.Rename("入力1"), "文字入力", "object-1");
        session.ExecutePage(pageId, page => page.Rename("入力2"), "文字入力", "object-1");
        session.MarkSaved();
        var savedRevision = session.SavedRevision;

        session.ExecutePage(pageId, page => page.Rename("入力3"), "文字入力", "object-1");
        session.ExecutePage(pageId, page => page.Rename("入力4"), "文字入力", "object-1");

        Assert.IsTrue(session.IsDirty);
        Assert.AreNotEqual(savedRevision, session.CurrentRevision);
        Assert.AreEqual(savedRevision + 1, session.CurrentRevision);
        Assert.AreEqual(session.CurrentRevision, session.History.CurrentRevision);
    }

    [TestMethod]
    public void PageDirtyDoesNotIncludeSavedCommonNode()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("dirty pages"));
        var first = session.ActivePage!;
        var second = session.Document.AddPage("02");
        session.MarkChanged(second.PageId, "ページ追加");
        session.ExecutePage(first.PageId, page => page.Rename("保存済み"), "名前変更", "first");
        session.MarkSaved();

        session.ExecutePage(second.PageId, page => page.Rename("未保存"), "名前変更", "second");

        Assert.IsFalse(session.IsPageDirty(first.PageId));
        Assert.IsTrue(session.IsPageDirty(second.PageId));
    }

    [TestMethod]
    public void FailedAssetRestoreRollsBackDocumentAndHistory()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("atomic"));
        var pageId = session.ActivePage!.PageId;
        session.ExecutePage(pageId, page => page.Rename("変更後"), "名前変更");
        var revision = session.History.CurrentRevision;

        Assert.ThrowsException<InvalidOperationException>(() =>
            session.History.Undo(session.Document, _ => throw new InvalidOperationException("asset failure")));

        Assert.AreEqual("変更後", session.ActivePage!.Name);
        Assert.IsTrue(session.History.CanUndo);
        Assert.AreEqual(revision, session.History.CurrentRevision);
    }

    [TestMethod]
    public void UndoRestoresActivePageAfterDeletingIt()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("active page"));
        var first = session.ActivePage!;
        var second = session.Document.AddPage("02");
        session.ActivatePage(second.PageId);
        session.MarkChanged(second.PageId, "ページ追加");

        session.Execute(project => project.RemovePage(second.PageId), "ページ削除");
        Assert.AreEqual(first.PageId, session.ActivePageId);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(second.PageId, session.ActivePageId);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(first.PageId, session.ActivePageId);
    }

    [TestMethod]
    public void TrimmingCutsHistoryParentChain()
    {
        var document = new ProjectDocument("trim chain");
        var history = new UndoRedoHistory(document, maxEntries: 2, maxBytes: 10000);
        for (var index = 0; index < 20; index++)
        {
            history.Execute(document, project => project.Rename($"編集{index}"), "名前変更");
        }

        var currentField = typeof(UndoRedoHistory).GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var parentProperty = currentField.FieldType.GetProperty("Parent", BindingFlags.Instance | BindingFlags.Public)!;
        var node = currentField.GetValue(history);
        var depth = 0;
        while (node != null && depth < 10)
        {
            node = parentProperty.GetValue(node);
            depth++;
        }

        Assert.IsTrue(depth <= 2, $"trimmed history chain depth was {depth}");
    }

    [TestMethod]
    public void RepeatedEditSaveCyclesReleaseTrimmedHistoryReferences()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("save trim cycles"));
        var pageId = session.ActivePage!.PageId;

        for (var cycle = 0; cycle < 6; cycle++)
        {
            for (var index = 0; index < 35; index++)
            {
                var value = cycle * 35 + index;
                session.ExecutePage(pageId, page => page.Rename($"cycle-{value}"),
                    $"保存cycle編集-{value}", $"save-cycle-{value}");
            }
            session.MarkSaved();
        }

        Assert.IsTrue(session.History.RetainedEntryCount <= session.History.Count);
        Assert.IsTrue(session.History.RetainedNodeCount <= session.History.MaxEntries + 2,
            $"retained nodes: {session.History.RetainedNodeCount}");
        Assert.IsTrue(session.History.EstimatedBytes <= session.History.MaxBytes);
    }

    [TestMethod]
    public void DifferentCoalesceKeysDoNotMergeUnrelatedEdits()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("coalesce keys"));
        var pageId = session.ActivePage!.PageId;

        session.ExecutePage(pageId, page => page.Rename("文字"), "ページ編集", "text");
        session.ExecutePage(pageId, page => page.Canvas.CanvasWidth = 100, "ページ編集", "canvas");

        Assert.AreEqual(2, session.History.UndoCount);
    }

    [TestMethod]
    public void NormalEditUndoKeepsSecondPageSelected()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("normal active page"));
        session.Execute(project =>
        {
            var page = project.AddPage("02");
            session.ActivatePage(page.PageId);
        }, "ページ追加");
        var second = session.ActivePage!;

        second.Rename("edited");
        session.MarkChanged(second.PageId, "ページ編集", "second-page");

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(second.PageId, session.ActivePageId);
        Assert.AreEqual("02", session.ActivePage!.Name);
    }

    [TestMethod]
    public void AddAndDuplicateUndoRedoRestoreSelectedPage()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("page selection"));
        var first = session.ActivePage!;

        Guid addedPageId = Guid.Empty;
        session.Execute(project =>
        {
            var page = project.AddPage("02");
            addedPageId = page.PageId;
            session.ActivatePage(page.PageId);
        }, "ページ追加");
        Assert.AreEqual(addedPageId, session.ActivePageId);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(first.PageId, session.ActivePageId);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(addedPageId, session.ActivePageId);

        var source = session.ActivePage!;
        Guid clonePageId = Guid.Empty;
        session.Execute(project =>
        {
            var clone = project.ClonePage(source.PageId);
            clonePageId = clone.PageId;
            session.ActivatePage(clone.PageId);
        }, "ページ複製");
        Assert.AreEqual(clonePageId, session.ActivePageId);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(source.PageId, session.ActivePageId);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(clonePageId, session.ActivePageId);
    }

    [TestMethod]
    public void BranchAfterUndoDoesNotDirtySavedCommonPage()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("saved branch"));
        var first = session.ActivePage!;
        var second = session.Document.AddPage("02");
        session.MarkChanged(second.PageId, "ページ追加", "add-second");
        session.ExecutePage(first.PageId, page => page.Rename("saved"), "名前変更", "first");
        session.MarkSaved();

        session.ExecutePage(first.PageId, page => page.Rename("temporary"), "名前変更", "first");
        Assert.IsTrue(session.Undo());
        session.ExecutePage(second.PageId, page => page.Rename("branch"), "名前変更", "second");

        Assert.IsFalse(session.IsPageDirty(first.PageId));
        Assert.IsTrue(session.IsPageDirty(second.PageId));
    }

    [TestMethod]
    public void AbandonedSavedRedoBranchKeepsCommonPageClean()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("saved redo branch"));
        var pageA = session.ActivePage!;
        PageDocument? pageB = null;
        PageDocument? pageC = null;
        session.Execute(project =>
        {
            pageB = project.AddPage("B");
            pageC = project.AddPage("C");
        }, "ページ追加");
        session.MarkSaved();

        session.ExecutePage(pageC!.PageId, page => page.Rename("C changed"), "C編集", "page-c");
        session.ExecutePage(pageA.PageId, page => page.Rename("A saved"), "A編集", "page-a");
        session.MarkSaved();

        // Undo moves the saved A operation to redo. Editing B abandons that redo branch.
        Assert.IsTrue(session.Undo());
        session.ExecutePage(pageB!.PageId, page => page.Rename("B branch"), "B編集", "page-b");

        Assert.IsTrue(session.IsPageDirty(pageA.PageId));
        Assert.IsTrue(session.IsPageDirty(pageB.PageId));
        Assert.IsFalse(session.IsPageDirty(pageC.PageId));
    }

    [TestMethod]
    public void TrimmedSavedNodeStillLeavesUnchangedPageClean()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("saved trim"));
        var first = session.ActivePage!;
        var second = session.Document.AddPage("02");
        session.MarkChanged(second.PageId, "ページ追加", "add-second");
        session.ExecutePage(first.PageId, page => page.Rename("saved"), "名前変更", "first");
        session.MarkSaved();

        for (var index = 0; index < 110; index++)
        {
            var value = index;
            session.ExecutePage(second.PageId, page => page.Rename($"edit-{value}"),
                $"編集-{value}", $"second-{value}");
        }

        Assert.IsFalse(session.IsPageDirty(first.PageId));
        Assert.IsTrue(session.IsPageDirty(second.PageId));
    }

    [TestMethod]
    public void SameIdPageRestoreRaisesActivePageNotification()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("same page id"));
        var pageId = session.ActivePage!.PageId;
        session.ExecutePage(pageId, page => page.Rename("changed"), "名前変更", "page");
        var activePageNotifications = 0;
        session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProjectSession.ActivePage)) activePageNotifications++;
        };

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(pageId, session.ActivePageId);
        Assert.IsTrue(activePageNotifications > 0);
    }
}
