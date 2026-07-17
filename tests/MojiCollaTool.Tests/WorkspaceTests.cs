using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class WorkspaceTests
{
    [TestMethod]
    public void SessionsKeepProjectStateAndActivePageIsIsolated()
    {
        using var workspace = new ApplicationWorkspace();
        var first = workspace.Open(new ProjectDocument("一"), Path.Combine(Path.GetTempPath(), "first.mctzip"));
        var second = workspace.Open(new ProjectDocument("二"));
        var secondPage = second.Document.AddPage("二枚目");

        second.ActivatePage(secondPage.PageId);
        second.MarkChanged();
        workspace.SetActiveSession(second);

        Assert.AreEqual(2, workspace.SessionCount);
        Assert.AreSame(second, workspace.ActiveSession);
        Assert.AreSame(secondPage, workspace.ActivePage);
        Assert.IsFalse(first.IsDirty);
        Assert.IsTrue(second.IsDirty);
        Assert.AreEqual(1, first.Document.PageCount);
        Assert.AreEqual(2, second.Document.PageCount);
    }

    [TestMethod]
    public void WorkspaceRejectsDuplicateNormalizedPathsAndAllowsReassignment()
    {
        using var workspace = new ApplicationWorkspace();
        var path = Path.Combine(Path.GetTempPath(), "nested", "project.mctzip");
        var first = workspace.Open(new ProjectDocument("一"), path);

        Assert.ThrowsException<InvalidOperationException>(() =>
            workspace.Open(new ProjectDocument("二"), Path.Combine(Path.GetTempPath(), "nested", ".", "project.mctzip")));

        var second = workspace.Open(new ProjectDocument("二"));
        workspace.SetFilePath(second, Path.Combine(Path.GetTempPath(), "other.mctzip"));
        Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "other.mctzip")), second.FilePath);
        Assert.IsTrue(workspace.IsPathOpen(first.FilePath));
    }

    [TestMethod]
    public void DirtySessionRequiresExplicitClosePolicyAndClosingReleasesIt()
    {
        using var workspace = new ApplicationWorkspace();
        var first = workspace.Open(new ProjectDocument("一"));
        var second = workspace.Open(new ProjectDocument("二"));
        first.MarkChanged();
        workspace.SetActiveSession(first);

        Assert.IsFalse(workspace.CloseSession(first));
        Assert.AreEqual(2, workspace.SessionCount);
        Assert.IsTrue(workspace.CloseSession(first, CloseSessionPolicy.DiscardChanges));
        Assert.AreEqual(1, workspace.SessionCount);
        Assert.AreSame(second, workspace.ActiveSession);
        Assert.IsTrue(first.IsClosed);
        Assert.ThrowsException<ObjectDisposedException>(() => first.MarkChanged());
    }

    [TestMethod]
    public void SavedRevisionTracksDirtyStateAndCanBeRestoredByUndoService()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("履歴"));

        session.MarkChanged();
        session.MarkChanged();
        session.MarkSaved();
        session.MarkChanged();
        session.RestoreRevision(session.SavedRevision);

        Assert.AreEqual(session.SavedRevision, session.CurrentRevision);
        Assert.IsFalse(session.IsDirty);
    }

    [TestMethod]
    public void RemovingActivePageFallsBackToRemainingPage()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("ページ"));
        var second = session.Document.AddPage("二");
        session.ActivatePage(second.PageId);

        session.Execute(project => project.RemovePage(second.PageId));

        Assert.AreEqual(session.Document.Pages[0].PageId, session.ActivePageId);
        Assert.IsNotNull(session.ActivePage);
    }

    [TestMethod]
    public void SaveUndoBranchEditRemainsDirty()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("分岐"));

        session.MarkChanged();
        session.MarkSaved();
        session.RestoreRevision(0);
        session.MarkChanged();

        Assert.AreEqual(1, session.SavedRevision);
        Assert.AreNotEqual(session.SavedRevision, session.CurrentRevision);
        Assert.IsTrue(session.IsDirty);
    }

    [TestMethod]
    public void OnlyUndoOrRedoToSavedRevisionMakesSessionClean()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("saved revision"));

        session.MarkChanged();
        session.MarkSaved();
        var savedRevision = session.SavedRevision;
        session.MarkChanged();
        var editedRevision = session.CurrentRevision;

        session.RestoreRevision(0);
        Assert.IsTrue(session.IsDirty);
        session.RestoreRevision(editedRevision);
        Assert.IsTrue(session.IsDirty);
        session.RestoreRevision(savedRevision);
        Assert.IsFalse(session.IsDirty);
    }

    [TestMethod]
    public void RevisionTokensDoNotCollideAfterRestoringAnOlderRevision()
    {
        using var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("token"));

        session.MarkChanged();
        var firstRevision = session.CurrentRevision;
        session.MarkChanged();
        var secondRevision = session.CurrentRevision;
        session.RestoreRevision(firstRevision);
        session.MarkChanged();
        var branchRevision = session.CurrentRevision;

        Assert.AreEqual(1, firstRevision);
        Assert.AreEqual(2, secondRevision);
        Assert.AreEqual(3, branchRevision);
        Assert.AreNotEqual(firstRevision, branchRevision);
        Assert.AreNotEqual(secondRevision, branchRevision);
        Assert.IsTrue(branchRevision > secondRevision);
    }
}
