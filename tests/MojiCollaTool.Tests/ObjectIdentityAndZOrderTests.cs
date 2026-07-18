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

        CollectionAssert.AreEqual(new[] { 0, 1 }, page.Objects.Select(item => item.ZIndex).ToArray());
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
    public void CloningMultipleObjectsPreservesLegacyIdsAndRemapsRelationships()
    {
        var root = new MojiData { Id = 7 };
        var child = new MojiData { Id = 8, ParentId = root.ObjectId, GroupId = root.ObjectId };
        var source = new PageDocument("01", new[] { root, child });

        var clone = source.Clone();

        CollectionAssert.AreEqual(new[] { 7, 8 }, clone.Objects.Select(item => item.Id).ToArray());
        Assert.IsTrue(clone.Objects.All(item => !source.Objects.Any(sourceItem => sourceItem.ObjectId == item.ObjectId)));
        Assert.AreEqual(clone.Objects[0].ObjectId, clone.Objects[1].ParentId);
        Assert.AreEqual(clone.Objects[0].ObjectId, clone.Objects[1].GroupId);
    }

    [TestMethod]
    public void LegacyXmlWithoutCommonFieldsGeneratesIdentityAndKeepsVisibleDefault()
    {
        using var scope = TemporaryDirectory.Create();
        var path = Path.Combine(scope.Path, "MojiData17.xml");
        File.WriteAllText(path, "<MojiData><Id>17</Id><FullText>legacy</FullText></MojiData>");

        var restored = DataIO.ReadMojiData(path);

        Assert.AreEqual(17, restored.Id);
        Assert.AreNotEqual(Guid.Empty, restored.ObjectId);
        Assert.AreEqual(DocumentObjectTypes.Text, restored.Type);
        Assert.IsTrue(restored.IsVisible);
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

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MojiCollaTool.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
