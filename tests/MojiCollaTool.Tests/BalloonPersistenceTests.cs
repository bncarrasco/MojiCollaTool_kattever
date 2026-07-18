using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MojiCollaTool.Tests
{
    [TestClass]
    public class BalloonPersistenceTests
    {
        [TestMethod]
        public void VersionedRoundTripPreservesBalloonTailLinkAndOrder()
        {
            using var scope = TemporaryDirectory.Create();
            var path = Path.Combine(scope.Path, "日本語", "balloon.mctzip");
            var text = new MojiData { FullText = "横書きとemoji 😀" };
            var balloon = new BalloonData
            {
                ShapeKind = BalloonShapeKind.Monologue,
                Bounds = new System.Windows.Rect(4, 5, 300, 160),
                Fill = System.Windows.Media.Colors.Beige,
                Stroke = System.Windows.Media.Colors.Maroon,
                StrokeThickness = 3,
                Tail = new BalloonTailData { TipX = 90, TipY = 220, RootParameter = .75, Width = 20 },
                TextLink = new TextLinkData
                {
                    TextObjectId = text.ObjectId,
                    LayoutMode = BalloonTextLayoutMode.FitTextToBalloon,
                    Padding = 12,
                    MinimumFontSize = 14,
                    Alignment = BalloonTextAlignment.Start,
                },
            };
            var project = new ProjectDocument(Guid.NewGuid(), "日本語プロジェクト", new[]
            {
                new PageDocument("ページ😀", new[] { text }, new[] { balloon }),
            });

            DataIO.WriteVersionedProject(path, project);
            var restored = DataIO.ReadVersionedProject(path);
            var restoredPage = restored.Pages[0];
            var restoredBalloon = restoredPage.Balloons.Single();

            Assert.AreEqual(project.ProjectId, restored.ProjectId);
            Assert.AreEqual(text.ObjectId, restoredPage.Objects[0].ObjectId);
            Assert.AreEqual(balloon.ObjectId, restoredBalloon.ObjectId);
            Assert.AreEqual(BalloonShapeKind.Monologue, restoredBalloon.ShapeKind);
            Assert.AreEqual(balloon.Bounds, restoredBalloon.Bounds);
            Assert.AreEqual(balloon.Tail!.Tip, restoredBalloon.Tail!.Tip);
            Assert.AreEqual(balloon.Tail.RootParameter, restoredBalloon.Tail.RootParameter);
            Assert.AreEqual(text.ObjectId, restoredBalloon.TextLink!.TextObjectId);
            Assert.AreEqual(BalloonTextAlignment.Start, restoredBalloon.TextLink.Alignment);
            Assert.AreEqual(1, restoredBalloon.ZIndex);
        }

        [TestMethod]
        public void UnknownBalloonShapeIsLoadedAsUnknownWithoutRejectingArchive()
        {
            using var scope = TemporaryDirectory.Create();
            var sourcePath = Path.Combine(scope.Path, "source.mctzip");
            var futurePath = Path.Combine(scope.Path, "future.mctzip");
            var project = new ProjectDocument(Guid.NewGuid(), "future", new[]
            {
                new PageDocument("01", Array.Empty<MojiData>(), new[] { new BalloonData() }),
            });
            DataIO.WriteVersionedProject(sourcePath, project);

            using (var source = ZipFile.OpenRead(sourcePath))
            using (var destination = ZipFile.Open(futurePath, ZipArchiveMode.Create))
            {
                foreach (var entry in source.Entries)
                {
                    var output = destination.CreateEntry(entry.FullName).Open();
                    using (output)
                    using (var input = entry.Open())
                    {
                        if (entry.FullName.EndsWith("page.xml", StringComparison.Ordinal))
                        {
                            var page = XDocument.Load(input);
                            page.Descendants("ShapeKind").Single().Value = "FutureShape";
                            var bytes = Encoding.UTF8.GetBytes(page.ToString(SaveOptions.DisableFormatting));
                            output.Write(bytes, 0, bytes.Length);
                        }
                        else
                        {
                            input.CopyTo(output);
                        }
                    }
                }
            }

            var restored = DataIO.ReadVersionedProject(futurePath);
            Assert.AreEqual(BalloonShapeKind.Unknown, restored.Pages[0].Balloons[0].ShapeKind);
            Assert.AreEqual("FutureShape", restored.Pages[0].Balloons[0].UnknownShapeKind);
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            private TemporaryDirectory(string path) => Path = path;

            public string Path { get; }

            public static TemporaryDirectory Create()
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MojiCollaToolTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(path);
                return new TemporaryDirectory(path);
            }

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
            }
        }
    }
}
