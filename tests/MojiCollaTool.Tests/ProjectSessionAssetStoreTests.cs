using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class ProjectSessionAssetStoreTests
{
    [TestMethod]
    public void SessionAssetStoresAreIsolatedByStoreAndPageId()
    {
        var page = new PageDocument("01");
        using var first = new ProjectSessionAssetStore();
        using var second = new ProjectSessionAssetStore();
        var project = new ProjectDocument("画像");
        var projectPage = project.Pages[0];

        first.SaveImages(project, new[] { new ProjectAssetRestore(projectPage.PageId, 1, ".png", new byte[] { 1, 2, 3 }) });

        Assert.AreNotEqual(first.RootPath, second.RootPath);
        using var firstAsset = first.OpenImage(projectPage, 1);
        using var secondAsset = second.OpenImage(projectPage, 1);
        using var unrelatedAsset = first.OpenImage(page, 1);
        Assert.IsNotNull(firstAsset);
        Assert.IsNull(secondAsset);
        Assert.IsNull(unrelatedAsset);
    }

    [TestMethod]
    public void DuplicatingPageAssetsUsesTheNewPageId()
    {
        var project = new ProjectDocument("画像複製");
        var source = project.Pages[0];
        var target = project.ClonePage(source.PageId);
        using var store = new ProjectSessionAssetStore();
        store.SaveImages(project, new[] { new ProjectAssetRestore(source.PageId, 2, "jpg", new byte[] { 4, 5 }) });

        store.CopyPageAssets(source, target);

        using var sourceAsset = store.OpenImage(source, 2);
        using var targetAsset = store.OpenImage(target, 2);
        Assert.IsNotNull(sourceAsset);
        Assert.IsNotNull(targetAsset);
        using var sourceBytes = new MemoryStream();
        using var targetBytes = new MemoryStream();
        sourceAsset!.Content.CopyTo(sourceBytes);
        targetAsset!.Content.CopyTo(targetBytes);
        CollectionAssert.AreEqual(sourceBytes.ToArray(), targetBytes.ToArray());
        Assert.AreNotEqual(source.PageId, target.PageId);
    }

    [TestMethod]
    public void ClosingSessionReleasesItsAssetStore()
    {
        var workspace = new ApplicationWorkspace();
        var session = workspace.Open(new ProjectDocument("破棄"));
        var root = session.AssetStore.RootPath;

        Assert.IsTrue(workspace.CloseSession(session, CloseSessionPolicy.DiscardChanges));
        Assert.IsFalse(Directory.Exists(root));
        workspace.Dispose();
    }
}
