using System;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests;

[TestClass]
public class BalloonGeometryTests
{
    [TestMethod]
    public void AllSupportedShapesProduceGeometryInsideBounds()
    {
        var factory = new BalloonGeometryFactory();
        var bounds = new Rect(0, 0, 240, 140);

        foreach (var shape in new[]
        {
            BalloonShapeKind.Ellipse,
            BalloonShapeKind.RoundedRectangle,
            BalloonShapeKind.Rectangle,
            BalloonShapeKind.Monologue,
        })
        {
            var geometry = factory.Create(shape, bounds);
            Assert.IsFalse(geometry.IsEmpty(), shape.ToString());
            Assert.IsTrue(bounds.Contains(geometry.Bounds), shape.ToString());
            Assert.IsTrue(geometry.IsFrozen, shape.ToString());
        }
    }

    [TestMethod]
    public void UnknownShapeFallsBackWithoutChangingModelValue()
    {
        var balloon = new BalloonData { ShapeKindValue = "FutureShape", Bounds = new Rect(4, 8, 120, 80) };
        var factory = new BalloonGeometryFactory();

        var geometry = factory.Create(balloon);

        Assert.AreEqual(BalloonShapeKind.Unknown, balloon.ShapeKind);
        Assert.AreEqual("FutureShape", balloon.UnknownShapeKind);
        Assert.IsFalse(geometry.IsEmpty());
        Assert.AreEqual(balloon.Bounds, geometry.Bounds);
    }

    [TestMethod]
    public void CacheReusesInputsAndEvictsLeastRecentlyUsedEntry()
    {
        var factory = new BalloonGeometryFactory(cacheCapacity: 2);
        var first = new Rect(0, 0, 10, 10);
        var second = new Rect(0, 0, 20, 20);
        var third = new Rect(0, 0, 30, 30);

        var firstGeometry = factory.Create(BalloonShapeKind.Ellipse, first);
        factory.Create(BalloonShapeKind.Ellipse, second);
        factory.Create(BalloonShapeKind.Ellipse, first);
        factory.Create(BalloonShapeKind.Ellipse, third);
        Assert.AreSame(firstGeometry, factory.Create(BalloonShapeKind.Ellipse, first));
        factory.Create(BalloonShapeKind.Ellipse, second);

        Assert.AreSame(firstGeometry, factory.Create(BalloonShapeKind.Ellipse, first));
        Assert.AreEqual(2, factory.CacheCount);
        Assert.IsTrue(factory.CacheHits >= 2);
        Assert.IsTrue(factory.CacheMisses >= 4);
    }

    [TestMethod]
    public void GeometryKeyIncludesShapeAndAllBoundsParameters()
    {
        var factory = new BalloonGeometryFactory();
        var ellipse = factory.Create(BalloonShapeKind.Ellipse, new Rect(0, 0, 100, 50));
        var rounded = factory.Create(BalloonShapeKind.RoundedRectangle, new Rect(0, 0, 100, 50));
        var moved = factory.Create(BalloonShapeKind.Ellipse, new Rect(1, 0, 100, 50));

        Assert.AreNotSame(ellipse, rounded);
        Assert.AreNotSame(ellipse, moved);
        Assert.AreEqual(3, factory.CacheMisses);
    }
}
