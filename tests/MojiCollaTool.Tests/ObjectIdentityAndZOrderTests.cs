using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class ObjectIdentityAndZOrderTests
{
    [TestMethod]
    public void NewTextObjectHasCommonIdentityAndStateDefaults()
    {
        var first = new MojiData();
        var second = new MojiData();

        Assert.AreNotEqual(Guid.Empty, first.ObjectId);
        Assert.AreNotEqual(first.ObjectId, second.ObjectId);
        Assert.AreEqual(DocumentObjectTypes.Text, first.Type);
        Assert.IsTrue(first.IsVisible);
        Assert.IsFalse(first.IsLocked);
    }

    [TestMethod]
    public void PageNormalizesZIndexToCanonicalListOrder()
    {
        var first = new MojiData { Id = 11, ZIndex = 50 };
        var second = new MojiData { Id = 12, ZIndex = 50 };
        var page = new PageDocument("01", new[] { first, second });

        Assert.AreEqual(new[] { 0, 1 }, page.Objects.Select(item => item.ZIndex).ToArray());
        Assert.AreEqual(11, page.Objects[0].Id);
        Assert.AreEqual(12, page.Objects[1].Id);
    }

    [TestMethod]
    public void PageRejectsDuplicateObjectIds()
    {
        var objectId = Guid.NewGuid();
        var first = new MojiData { ObjectId = objectId };
        var second = new MojiData { ObjectId = objectId };

        Assert.ThrowsException<InvalidOperationException>(() => new PageDocument("01", new[] { first, second }));
    }

    [TestMethod]
    public void ProjectRejectsObjectIdsDuplicatedAcrossPages()
    {
        var objectId = Guid.NewGuid();
        var firstPage = new PageDocument("01", new[] { new MojiData { ObjectId = objectId } });
        var secondPage = new PageDocument("02", new[] { new MojiData { ObjectId = objectId } });

        Assert.ThrowsException<InvalidOperationException>(() => new ProjectDocument(Guid.NewGuid(), "project", new[] { firstPage, secondPage }));
    }

    [TestMethod]
    public void CloningPageCreatesNewObjectIdsWhilePreservingLegacyIds()
    {
        var source = new PageDocument("01", new[] { new MojiData { Id = 7 } });
        var clone = source.Clone();

        Assert.AreNotEqual(source.PageId, clone.PageId);
        Assert.AreNotEqual(source.Objects[0].ObjectId, clone.Objects[0].ObjectId);
        Assert.AreEqual(source.Objects[0].Id, clone.Objects[0].Id);
    }

    [TestMethod]
    public void VersionedRoundTripPreservesObjectIdentityStateAndOrder()
    {
        using var scope = TemporaryDirectory.Create();
        var path = Path.Combine(scope.Path, "object-model.mctzip");
        var objectId = Guid.NewGuid();
        var sourcePage = new PageDocument("01", new[]
        {
            new MojiData
            {
                Id = 3,
                ObjectId = objectId,
                Type = DocumentObjectTypes.Text,
                ZIndex = 99,
                IsLocked = true,
                IsVisible = false,
                ParentId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
            },
        });
        var source = new ProjectDocument(Guid.NewGuid(), "project", new[] { sourcePage });

        DataIO.WriteVersionedProject(path, source);
        var restored = DataIO.ReadVersionedProject(path);
        var restoredObject = restored.Pages[0].Objects[0];

        Assert.AreEqual(objectId, restoredObject.ObjectId);
        Assert.AreEqual(DocumentObjectTypes.Text, restoredObject.Type);
        Assert.AreEqual(0, restoredObject.ZIndex);
        Assert.IsTrue(restoredObject.IsLocked);
        Assert.IsFalse(restoredObject.IsVisible);
        Assert.AreEqual(sourcePage.Objects[0].ParentId, restoredObject.ParentId);
        Assert.AreEqual(sourcePage.Objects[0].GroupId, restoredObject.GroupId);
    }
}
