using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace MojiCollaTool
{
    /// <summary>
    /// 1つのProjectSessionだけが所有するページ画像の一時保存領域です。
    /// global Working directoryは読み書きの正本として使用しません。
    /// </summary>
    public sealed class ProjectSessionAssetStore : IProjectAssetSource, IProjectAssetSink, IProjectAssetBatchSink, IDisposable
    {
        private readonly string _rootPath;
        private bool _isDisposed;

        public ProjectSessionAssetStore()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), "MojiCollaTool", "sessions", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootPath);
        }

        public string RootPath => _rootPath;

        public ProjectImageAsset? OpenImage(PageDocument page, int imageNumber)
        {
            EnsureOpen();
            if (page == null) throw new ArgumentNullException(nameof(page));
            ValidateImageNumber(imageNumber);

            var pageDirectory = GetPageDirectory(page.PageId);
            if (!Directory.Exists(pageDirectory)) return null;
            var files = Directory.GetFiles(pageDirectory, $"image{imageNumber}.*");
            if (files.Length == 0) return null;
            if (files.Length != 1) throw new InvalidDataException($"Page asset image{imageNumber} is duplicated.");

            var extension = VersionedProjectFormat.NormalizeAssetExtension(Path.GetExtension(files[0]));
            return new ProjectImageAsset(extension, new FileStream(files[0], FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        public void SaveImage(Guid pageId, int imageNumber, string extension, Stream content)
        {
            EnsureOpen();
            if (content == null) throw new ArgumentNullException(nameof(content));
            ValidateImageNumber(imageNumber);
            var bytes = ReadContent(content);
            var existing = ReadAllAssets().ToDictionary(asset => Key(asset.PageId, asset.ImageNumber));
            existing[Key(pageId, imageNumber)] = new AssetValue(pageId, imageNumber, NormalizeExtension(extension), bytes);
            ReplaceWith(existing.Values);
        }

        public void SaveImages(ProjectDocument project, IReadOnlyList<ProjectAssetRestore> assets)
        {
            EnsureOpen();
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (assets == null) throw new ArgumentNullException(nameof(assets));

            var prepared = new List<AssetValue>(assets.Count);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in assets)
            {
                if (!project.ContainsPage(asset.PageId)) throw new InvalidDataException("Asset refers to an unknown page.");
                ValidateImageNumber(asset.ImageNumber);
                var key = Key(asset.PageId, asset.ImageNumber);
                if (!keys.Add(key)) throw new InvalidDataException("Duplicate page asset.");
                prepared.Add(new AssetValue(asset.PageId, asset.ImageNumber, NormalizeExtension(asset.Extension), asset.Content.ToArray()));
            }

            // ReadProjectの復元は全件検証後、空のstageを一度だけ置換します。
            ReplaceWith(prepared);
        }

        public ProjectImageCandidate PrepareImage(string filePath)
        {
            EnsureOpen();
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Image path is required.", nameof(filePath));
            var bytes = File.ReadAllBytes(filePath);
            if (bytes.Length == 0 || bytes.Length > VersionedProjectFormat.MaxEntrySize)
            {
                throw new InvalidDataException("Image asset is empty or too large.");
            }

            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0) throw new InvalidDataException("Image contains no frame.");
                var frame = decoder.Frames[0];
                if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0)
                {
                    throw new InvalidDataException("Image dimensions are invalid.");
                }

                return new ProjectImageCandidate(
                    NormalizeExtension(Path.GetExtension(filePath)),
                    bytes,
                    frame.PixelWidth,
                    frame.PixelHeight);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Image could not be decoded.", ex);
            }
        }

        public IReadOnlyList<ProjectAssetRestore> GetPageAssets(Guid pageId)
        {
            EnsureOpen();
            return ReadAllAssets()
                .Where(asset => asset.PageId == pageId)
                .Select(asset => new ProjectAssetRestore(asset.PageId, asset.ImageNumber, asset.Extension, asset.Content.ToArray()))
                .ToArray();
        }

        public void ReplacePageAssets(Guid pageId, IReadOnlyList<ProjectAssetRestore> assets)
        {
            EnsureOpen();
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            var replacements = new List<AssetValue>(assets.Count);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in assets)
            {
                if (asset.PageId != pageId) throw new InvalidDataException("Asset refers to another page.");
                ValidateImageNumber(asset.ImageNumber);
                if (!keys.Add(Key(asset.PageId, asset.ImageNumber))) throw new InvalidDataException("Duplicate page asset.");
                replacements.Add(new AssetValue(asset.PageId, asset.ImageNumber, NormalizeExtension(asset.Extension), asset.Content.ToArray()));
            }

            ReplaceWith(ReadAllAssets().Where(asset => asset.PageId != pageId).Concat(replacements));
        }

        public void CopyPageAssets(PageDocument source, PageDocument target)
        {
            EnsureOpen();
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var copied = ReadAllAssets()
                .Where(asset => asset.PageId == source.PageId)
                .Select(asset => new AssetValue(target.PageId, asset.ImageNumber, asset.Extension, asset.Content.ToArray()))
                .ToArray();
            var all = ReadAllAssets().Where(asset => asset.PageId != target.PageId).Concat(copied);
            ReplaceWith(all);
        }

        public void RemovePage(Guid pageId)
        {
            EnsureOpen();
            ReplaceWith(ReadAllAssets().Where(asset => asset.PageId != pageId));
        }

        public void RemoveImage(Guid pageId, int imageNumber)
        {
            EnsureOpen();
            ValidateImageNumber(imageNumber);
            ReplaceWith(ReadAllAssets().Where(asset => asset.PageId != pageId || asset.ImageNumber != imageNumber));
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            TryDeleteDirectory(_rootPath);
        }

        private IEnumerable<AssetValue> ReadAllAssets()
        {
            EnsureOpen();
            if (!Directory.Exists(_rootPath)) yield break;
            foreach (var pageDirectory in Directory.GetDirectories(_rootPath))
            {
                if (!Guid.TryParse(Path.GetFileName(pageDirectory), out var pageId)) continue;
                foreach (var file in Directory.GetFiles(pageDirectory, "image*.*"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!name.StartsWith("image", StringComparison.OrdinalIgnoreCase) ||
                        !int.TryParse(name.Substring(5), out var imageNumber)) continue;
                    ValidateImageNumber(imageNumber);
                    yield return new AssetValue(pageId, imageNumber,
                        VersionedProjectFormat.NormalizeAssetExtension(Path.GetExtension(file)),
                        File.ReadAllBytes(file));
                }
            }
        }

        private void ReplaceWith(IEnumerable<AssetValue> assets)
        {
            EnsureOpen();
            var stage = _rootPath + ".stage-" + Guid.NewGuid().ToString("N");
            var backup = _rootPath + ".backup-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(stage);
                foreach (var asset in assets)
                {
                    var pageDirectory = Path.Combine(stage, asset.PageId.ToString("D"));
                    Directory.CreateDirectory(pageDirectory);
                    File.WriteAllBytes(Path.Combine(pageDirectory, $"image{asset.ImageNumber}.{NormalizeExtension(asset.Extension)}"), asset.Content);
                }

                if (Directory.Exists(_rootPath)) Directory.Move(_rootPath, backup);
                Directory.Move(stage, _rootPath);
                TryDeleteDirectory(backup);
            }
            catch
            {
                TryDeleteDirectory(stage);
                if (!Directory.Exists(_rootPath) && Directory.Exists(backup)) Directory.Move(backup, _rootPath);
                throw;
            }
        }

        private string GetPageDirectory(Guid pageId) => Path.Combine(_rootPath, pageId.ToString("D"));

        private static string NormalizeExtension(string extension)
        {
            return VersionedProjectFormat.NormalizeAssetExtension(extension);
        }

        private static byte[] ReadContent(Stream content)
        {
            if (content.CanSeek) content.Position = 0;
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            if (buffer.Length > VersionedProjectFormat.MaxEntrySize) throw new InvalidDataException("Image asset is too large.");
            return buffer.ToArray();
        }

        private static void ValidateImageNumber(int imageNumber)
        {
            if (imageNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(imageNumber));
        }

        private static string Key(Guid pageId, int imageNumber) => $"{pageId:D}:{imageNumber}";

        private void EnsureOpen()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(ProjectSessionAssetStore));
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Cleanup failure must not hide the original operation result.
            }
        }

        private sealed class AssetValue
        {
            public AssetValue(Guid pageId, int imageNumber, string extension, byte[] content)
            {
                PageId = pageId;
                ImageNumber = imageNumber;
                Extension = extension;
                Content = content ?? throw new ArgumentNullException(nameof(content));
            }

            public Guid PageId { get; }
            public int ImageNumber { get; }
            public string Extension { get; }
            public byte[] Content { get; }
        }
    }

    public sealed class ProjectImageCandidate
    {
        public ProjectImageCandidate(string extension, byte[] content, int width, int height)
        {
            Extension = extension ?? throw new ArgumentNullException(nameof(extension));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Width = width;
            Height = height;
        }

        public string Extension { get; }
        public byte[] Content { get; }
        public int Width { get; }
        public int Height { get; }

        public ProjectAssetRestore ToAsset(Guid pageId, int imageNumber)
            => new ProjectAssetRestore(pageId, imageNumber, Extension, Content.ToArray());

        public ImageData ToImageData() => new ImageData(Width, Height);
    }
}
