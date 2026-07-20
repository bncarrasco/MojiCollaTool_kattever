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
            Assert.AreEqual(0, restoredBalloon.ZIndex);
            Assert.AreEqual(1, restoredPage.GetDocumentObject(text.ObjectId).ZIndex);
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

        [TestMethod]
        public void WriterUsesVersion24AndReaderAcceptsVersion20Archive()
        {
            using var scope = TemporaryDirectory.Create();
            var currentPath = Path.Combine(scope.Path, "current.mctzip");
            var legacyVersionPath = Path.Combine(scope.Path, "version20.mctzip");
            var project = new ProjectDocument(Guid.NewGuid(), "version", new[]
            {
                new PageDocument("01", Array.Empty<MojiData>(), new[] { new BalloonData() }),
            });
            DataIO.WriteVersionedProject(currentPath, project);

            using (var archive = ZipFile.OpenRead(currentPath))
            {
                var manifest = XDocument.Load(archive.GetEntry("manifest.xml")!.Open());
                Assert.AreEqual("2.4", manifest.Root!.Element("FormatVersion")!.Value);
                Assert.AreEqual("2.4", manifest.Root.Element("MinimumReaderVersion")!.Value);
            }

            RewriteArchive(currentPath, legacyVersionPath, entry =>
            {
                if (entry.FullName != "manifest.xml") return null;
                var manifest = XDocument.Load(entry.Open());
                manifest.Root!.Element("FormatVersion")!.Value = "2.0";
                manifest.Root.Element("MinimumReaderVersion")!.Value = "2.0";
                return Encoding.UTF8.GetBytes(manifest.ToString(SaveOptions.DisableFormatting));
            });

            var restored = DataIO.ReadVersionedProject(legacyVersionPath);
            Assert.AreEqual(1, restored.Pages[0].Balloons.Count);
        }

        [TestMethod]
        public void FutureMinorVersionIsRejected()
        {
            using var scope = TemporaryDirectory.Create();
            var currentPath = Path.Combine(scope.Path, "current.mctzip");
            var futurePath = Path.Combine(scope.Path, "future.mctzip");
            DataIO.WriteVersionedProject(currentPath, new ProjectDocument("future"));

            RewriteArchive(currentPath, futurePath, entry =>
            {
                if (entry.FullName != "manifest.xml") return null;
                var manifest = XDocument.Load(entry.Open());
                manifest.Root!.Element("FormatVersion")!.Value = "2.5";
                return Encoding.UTF8.GetBytes(manifest.ToString(SaveOptions.DisableFormatting));
            });

            Assert.ThrowsException<InvalidDataException>(() => DataIO.ReadVersionedProject(futurePath));
        }

        [TestMethod]
        public void MixedOrderSetUndoRedoAndPersistencePreserveOrder()
        {
            using var scope = TemporaryDirectory.Create();
            var path = Path.Combine(scope.Path, "mixed.mctzip");
            var page = new PageDocument("01");
            var firstBalloon = new BalloonData();
            var text = new MojiData { FullText = "本文" };
            var secondBalloon = new BalloonData();
            page.AddBalloon(firstBalloon);
            page.AddMojiData(text);
            page.AddBalloon(secondBalloon);
            var project = new ProjectDocument(Guid.NewGuid(), "mixed", new[] { page });
            var pageId = project.Pages[0].PageId;
            using var session = new ProjectSession(project);

            session.ExecutePage(pageId, target =>
            {
                target.SetMojiDatas(target.Objects.Select(item => item.Clone()));
                target.SetBalloons(target.Balloons.Select(item => item.Clone()));
                target.GetBalloon(firstBalloon.ObjectId).Fill = System.Windows.Media.Colors.LightBlue;
            }, "混在順回帰");
            var expectedOrder = new[] { firstBalloon.ObjectId, text.ObjectId, secondBalloon.ObjectId };
            CollectionAssert.AreEqual(expectedOrder, session.Document.Pages[0].AllObjects.Select(item => item.ObjectId).ToArray());

            Assert.IsTrue(session.Undo());
            CollectionAssert.AreEqual(expectedOrder, session.Document.Pages[0].AllObjects.Select(item => item.ObjectId).ToArray());
            Assert.IsTrue(session.Redo());
            CollectionAssert.AreEqual(expectedOrder, session.Document.Pages[0].AllObjects.Select(item => item.ObjectId).ToArray());

            DataIO.WriteVersionedProject(path, session.Document);
            var restored = DataIO.ReadVersionedProject(path);
            CollectionAssert.AreEqual(expectedOrder, restored.Pages[0].AllObjects.Select(item => item.ObjectId).ToArray());
        }

        private static void RewriteArchive(string sourcePath, string destinationPath, Func<ZipArchiveEntry, byte[]?> rewrite)
        {
            using var source = ZipFile.OpenRead(sourcePath);
            using var destination = ZipFile.Open(destinationPath, ZipArchiveMode.Create);
            foreach (var sourceEntry in source.Entries)
            {
                var destinationEntry = destination.CreateEntry(sourceEntry.FullName);
                using var output = destinationEntry.Open();
                var rewritten = rewrite(sourceEntry);
                if (rewritten != null)
                {
                    output.Write(rewritten, 0, rewritten.Length);
                }
                else
                {
                    using var input = sourceEntry.Open();
                    input.CopyTo(output);
                }
            }
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
