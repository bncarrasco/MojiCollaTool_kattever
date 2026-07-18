using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Serialization;

namespace MojiCollaTool
{
    public interface IProjectReader
    {
        ProjectDocument Read(string projectFilePath);

        ProjectDocument Read(string projectFilePath, IProjectAssetSink? assetSink);
    }

    public interface IProjectWriter
    {
        void Write(string projectFilePath, ProjectDocument project, bool createBackup = false);

        void Write(string projectFilePath, ProjectDocument project, IProjectAssetSource? assetSource, bool createBackup = false);
    }

    /// <summary>
    /// ページの背景画像1枚をarchiveへ渡すためのassetです。Streamの所有権は呼び出し側へ戻ります。
    /// </summary>
    public sealed class ProjectImageAsset : IDisposable
    {
        public ProjectImageAsset(string extension, Stream content)
        {
            Extension = extension ?? throw new ArgumentNullException(nameof(extension));
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public string Extension { get; }

        public Stream Content { get; }

        public void Dispose() => Content.Dispose();
    }

    /// <summary>
    /// ProjectDocumentと画像保存領域を分離するsourceです。pageIdと画像番号で対象を特定します。
    /// </summary>
    public interface IProjectAssetSource
    {
        ProjectImageAsset? OpenImage(PageDocument page, int imageNumber);
    }

    /// <summary>
    /// archiveから復元した画像をProjectSession固有の保存領域へ渡すsinkです。
    /// </summary>
    public interface IProjectAssetSink
    {
        void SaveImage(Guid pageId, int imageNumber, string extension, Stream content);
    }

    /// <summary>
    /// 複数画像をProjectSessionへ一括反映するsinkです。実装は全件成功時だけ反映する必要があります。
    /// </summary>
    public interface IProjectAssetBatchSink
    {
        void SaveImages(ProjectDocument project, IReadOnlyList<ProjectAssetRestore> assets);
    }

    public sealed class ProjectAssetRestore
    {
        public ProjectAssetRestore(Guid pageId, int imageNumber, string extension, byte[] content)
        {
            PageId = pageId;
            ImageNumber = imageNumber;
            Extension = extension ?? throw new ArgumentNullException(nameof(extension));
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public Guid PageId { get; }

        public int ImageNumber { get; }

        public string Extension { get; }

        public byte[] Content { get; }
    }

    [XmlRoot("Manifest")]
    public sealed class VersionedProjectManifest
    {
        public string FormatVersion { get; set; } = VersionedProjectFormat.CurrentVersion;

        public string MinimumReaderVersion { get; set; } = VersionedProjectFormat.CurrentVersion;

        public string ProjectId { get; set; } = string.Empty;

        public string Product { get; set; } = VersionedProjectFormat.ProductName;

        public string ProjectName { get; set; } = string.Empty;

        [XmlArray("Pages")]
        [XmlArrayItem("Page")]
        public List<VersionedManifestPage> Pages { get; set; } = new List<VersionedManifestPage>();
    }

    public sealed class VersionedManifestPage
    {
        public string PageId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int Order { get; set; }

        public string RelativePath { get; set; } = string.Empty;
    }

    [XmlRoot("Page")]
    public sealed class VersionedPageFile
    {
        public string PageId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int Order { get; set; }

        public CanvasData Canvas { get; set; } = new CanvasData();

        public string? Image1Path { get; set; }

        public string? Image2Path { get; set; }

        [XmlArray("Objects")]
        [XmlArrayItem("MojiData")]
        public List<MojiData> Objects { get; set; } = new List<MojiData>();

        [XmlArray("Balloons")]
        [XmlArrayItem("Balloon")]
        public List<BalloonData> Balloons { get; set; } = new List<BalloonData>();
    }

    public static class VersionedProjectFormat
    {
        public const string CurrentVersion = "2.0";
        public const string ProductName = "MojiCollaTool Katteban";
        public const string ManifestEntryName = "manifest.xml";
        public const int MaxArchiveEntries = 4096;
        public const long MaxEntrySize = 64L * 1024 * 1024;
        public const long MaxArchiveSize = 256L * 1024 * 1024;

        internal static readonly XmlSerializer ManifestSerializer = new XmlSerializer(typeof(VersionedProjectManifest));
        internal static readonly XmlSerializer PageSerializer = new XmlSerializer(typeof(VersionedPageFile));

        internal static XmlReaderSettings CreateReaderSettings()
        {
            return new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 0,
            };
        }

        internal static byte[] Serialize<T>(XmlSerializer serializer, T value)
        {
            using var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
            {
                serializer.Serialize(writer, value);
            }

            return stream.ToArray();
        }

        internal static T Deserialize<T>(XmlSerializer serializer, Stream stream)
        {
            using var reader = XmlReader.Create(stream, CreateReaderSettings());
            var value = serializer.Deserialize(reader);
            if (value is not T typedValue)
            {
                throw new InvalidDataException($"The XML document did not contain {typeof(T).Name}.");
            }

            return typedValue;
        }

        internal static void ValidateArchiveEntry(ZipArchiveEntry entry)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("/", StringComparison.Ordinal) || Path.IsPathRooted(name))
            {
                throw new InvalidDataException($"Unsafe archive entry path: {entry.FullName}");
            }

            var segments = name.Split('/');
            if (segments.Any(segment => segment.Length == 0 || segment == "." || segment == ".." || segment.Contains(':')))
            {
                throw new InvalidDataException($"Unsafe archive entry path: {entry.FullName}");
            }

            if (entry.Length > MaxEntrySize)
            {
                throw new InvalidDataException($"Archive entry is too large: {entry.FullName}");
            }
        }

        internal static Version ParseSupportedVersion(string value, string fieldName)
        {
            var version = ParseVersion(value, fieldName);
            var current = Version.Parse(CurrentVersion);
            if (version.Major != current.Major || version > current)
            {
                throw new InvalidDataException($"Unsupported project format version: {value}");
            }

            return version;
        }

        internal static Version ParseMinimumReaderVersion(string value, string fieldName)
        {
            var version = ParseVersion(value, fieldName);
            if (version > Version.Parse(CurrentVersion))
            {
                throw new InvalidDataException($"The project requires a newer reader: {value}");
            }

            return version;
        }

        private static Version ParseVersion(string value, string fieldName)
        {
            if (!Version.TryParse(value, out var version) || version.Major < 0 || version.Minor < 0)
            {
                throw new InvalidDataException($"Invalid {fieldName}: {value}");
            }

            return version;
        }

        internal static string CanonicalPagePath(Guid pageId)
        {
            return $"pages/{pageId:D}/page.xml";
        }

        internal static string CanonicalAssetPath(Guid pageId, int imageNumber, string extension)
        {
            return $"pages/{pageId:D}/image{imageNumber}.{NormalizeAssetExtension(extension)}";
        }

        internal static string NormalizeAssetExtension(string extension)
        {
            var normalized = (extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
            if (normalized.Length == 0 || normalized.Length > 10 || normalized.Any(character => !char.IsLetterOrDigit(character)))
            {
                throw new InvalidDataException($"Invalid image extension: {extension}");
            }

            return normalized;
        }

        internal static string? ValidateAssetPath(string? path, Guid pageId, int imageNumber)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var normalizedPath = path.Replace('\\', '/');
            var prefix = $"pages/{pageId:D}/image{imageNumber}.";
            if (!normalizedPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Invalid image path: {path}");
            }

            var extension = NormalizeAssetExtension(normalizedPath.Substring(prefix.Length));
            var canonicalPath = CanonicalAssetPath(pageId, imageNumber, extension);
            if (!string.Equals(normalizedPath, canonicalPath, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Invalid image path: {path}");
            }

            return canonicalPath;
        }

        internal static bool HasImageMetadata(ImageData? imageData)
        {
            return imageData != null && (imageData.OriginalWidth != 0 || imageData.OriginalHeight != 0 || imageData.ModifiedWidth != 0 || imageData.ModifiedHeight != 0);
        }

        internal static void ValidateImageAsset(int imageNumber, ImageData? imageData, byte[]? content)
        {
            var hasMetadata = HasImageMetadata(imageData);
            var hasAsset = content != null;
            if (hasMetadata != hasAsset)
            {
                throw new InvalidDataException($"Image{imageNumber} metadata and archive asset are inconsistent.");
            }

            if (!hasMetadata) return;
            if (imageData!.OriginalWidth <= 0 || imageData.OriginalHeight <= 0)
            {
                throw new InvalidDataException($"Image{imageNumber} metadata dimensions are invalid.");
            }

            ValidateDecodedImage(imageNumber, imageData, content!);
        }

        private static void ValidateDecodedImage(int imageNumber, ImageData imageData, byte[] content)
        {
            try
            {
                using var stream = new MemoryStream(content, writable: false);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0 || decoder.Frames[0].PixelWidth != imageData.OriginalWidth || decoder.Frames[0].PixelHeight != imageData.OriginalHeight)
                {
                    throw new InvalidDataException($"Image{imageNumber} dimensions do not match CanvasData.");
                }
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Image{imageNumber} could not be decoded.", ex);
            }
        }
    }

    public sealed class VersionedProjectWriter : IProjectWriter
    {
        public void Write(string projectFilePath, ProjectDocument project, bool createBackup = false)
        {
            Write(projectFilePath, project, assetSource: null, createBackup);
        }

        public void Write(string projectFilePath, ProjectDocument project, IProjectAssetSource? assetSource, bool createBackup = false)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath)) throw new ArgumentException("Project path is required.", nameof(projectFilePath));
            if (project == null) throw new ArgumentNullException(nameof(project));

            var targetPath = Path.GetFullPath(projectFilePath);
            var parentPath = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrEmpty(parentPath)) throw new InvalidOperationException("Project directory could not be determined.");
            Directory.CreateDirectory(parentPath);

            var temporaryPath = Path.Combine(parentPath, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                WriteArchive(temporaryPath, project, assetSource);

                // Read the closed archive through the writer validation path before replacing the user's file.
                // This validates image bytes without discarding them through the public sinkless reader API.
                new VersionedProjectReader().ValidateForWriter(temporaryPath);
                ReplaceFile(temporaryPath, targetPath, createBackup);
                temporaryPath = string.Empty;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Versioned project save failed.", ex);
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporaryPath))
                {
                    TryDeleteFile(temporaryPath);
                }
            }
        }

        private static void WriteArchive(string archivePath, ProjectDocument project, IProjectAssetSource? assetSource)
        {
            var pages = project.Pages.OrderBy(page => page.Order).ToArray();
            if (pages.Length == 0) throw new InvalidOperationException("A project must contain at least one page.");
            if (pages.Select(page => page.PageId).Distinct().Count() != pages.Length)
            {
                throw new InvalidOperationException("A project contains duplicate page IDs.");
            }

            var manifest = new VersionedProjectManifest
            {
                FormatVersion = VersionedProjectFormat.CurrentVersion,
                MinimumReaderVersion = VersionedProjectFormat.CurrentVersion,
                ProjectId = project.ProjectId.ToString("D"),
                Product = VersionedProjectFormat.ProductName,
                ProjectName = project.Name,
                Pages = pages.Select(page => new VersionedManifestPage
                {
                    PageId = page.PageId.ToString("D"),
                    Name = page.Name,
                    Order = page.Order,
                    RelativePath = VersionedProjectFormat.CanonicalPagePath(page.PageId),
                }).ToList(),
            };

            using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
            WriteEntry(archive, VersionedProjectFormat.ManifestEntryName, VersionedProjectFormat.Serialize(VersionedProjectFormat.ManifestSerializer, manifest));
            foreach (var page in pages)
            {
                using var image1 = assetSource?.OpenImage(page, 1);
                using var image2 = assetSource?.OpenImage(page, 2);
                var canvas = page.Canvas.ToLegacyData();
                var preparedImage1 = PrepareImageAsset(page.PageId, 1, canvas.ImageData1, image1);
                var preparedImage2 = PrepareImageAsset(page.PageId, 2, canvas.ImageData2, image2);
                var pageFile = new VersionedPageFile
                {
                    PageId = page.PageId.ToString("D"),
                    Name = page.Name,
                    Order = page.Order,
                    Canvas = canvas,
                    Image1Path = preparedImage1?.Path,
                    Image2Path = preparedImage2?.Path,
                    // Persist canonical list order even when a caller has edited
                    // individual ZIndex values directly.
                    Objects = page.CreateObjectSnapshot().ToList(),
                    Balloons = page.Balloons.Select(PageDocument.CloneBalloonData).ToList(),
                };
                WriteEntry(archive, VersionedProjectFormat.CanonicalPagePath(page.PageId), VersionedProjectFormat.Serialize(VersionedProjectFormat.PageSerializer, pageFile));
                WriteAssetEntry(archive, preparedImage1);
                WriteAssetEntry(archive, preparedImage2);
            }
        }

        private static PreparedImageAsset? PrepareImageAsset(Guid pageId, int imageNumber, ImageData? imageData, ProjectImageAsset? asset)
        {
            if (asset == null)
            {
                VersionedProjectFormat.ValidateImageAsset(imageNumber, imageData, content: null);
                return null;
            }

            if (!VersionedProjectFormat.HasImageMetadata(imageData))
            {
                throw new InvalidDataException($"Image{imageNumber} archive asset has no matching CanvasData metadata.");
            }

            var content = ReadStream(asset.Content);
            VersionedProjectFormat.ValidateImageAsset(imageNumber, imageData, content);
            var extension = VersionedProjectFormat.NormalizeAssetExtension(asset.Extension);
            return new PreparedImageAsset(VersionedProjectFormat.CanonicalAssetPath(pageId, imageNumber, extension), content);
        }

        private static byte[] ReadStream(Stream source)
        {
            if (source.CanSeek) source.Position = 0;
            using var content = new MemoryStream();
            source.CopyTo(content);
            if (content.Length > VersionedProjectFormat.MaxEntrySize) throw new InvalidDataException("Image asset is too large.");
            return content.ToArray();
        }

        private static void WriteEntry(ZipArchive archive, string entryName, byte[] content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(content, 0, content.Length);
        }

        private static void WriteAssetEntry(ZipArchive archive, PreparedImageAsset? asset)
        {
            if (asset == null) return;

            var entry = archive.CreateEntry(asset.Path, CompressionLevel.Optimal);
            using var destination = entry.Open();
            destination.Write(asset.Content, 0, asset.Content.Length);
        }

        private static void ReplaceFile(string temporaryPath, string targetPath, bool createBackup)
        {
            if (!File.Exists(targetPath))
            {
                File.Move(temporaryPath, targetPath);
                return;
            }

            var backupPath = $"{targetPath}.backup";
            if (createBackup)
            {
                File.Copy(targetPath, backupPath, overwrite: true);
            }

            try
            {
                File.Replace(temporaryPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Move(temporaryPath, targetPath, overwrite: true);
            }
            catch (IOException) when (!createBackup)
            {
                File.Move(temporaryPath, targetPath, overwrite: true);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Do not hide the original save failure with cleanup failure.
            }
        }

        private sealed class PreparedImageAsset
        {
            public PreparedImageAsset(string path, byte[] content)
            {
                Path = path;
                Content = content;
            }

            public string Path { get; }

            public byte[] Content { get; }
        }
    }

    public sealed class VersionedProjectReader : IProjectReader
    {
        public ProjectDocument Read(string projectFilePath)
        {
            return ReadCore(projectFilePath, assetSink: null, requireAssetSink: true);
        }

        public ProjectDocument Read(string projectFilePath, IProjectAssetSink? assetSink)
        {
            return ReadCore(projectFilePath, assetSink, requireAssetSink: true);
        }

        internal void ValidateForWriter(string projectFilePath)
        {
            _ = ReadCore(projectFilePath, assetSink: null, requireAssetSink: false);
        }

        private ProjectDocument ReadCore(string projectFilePath, IProjectAssetSink? assetSink, bool requireAssetSink)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath)) throw new ArgumentException("Project path is required.", nameof(projectFilePath));
            if (!File.Exists(projectFilePath)) throw new FileNotFoundException("Project file was not found.", projectFilePath);

            try
            {
                using var archive = ZipFile.OpenRead(projectFilePath);
                ValidateArchive(archive);
                var manifestEntry = FindUniqueEntry(archive, VersionedProjectFormat.ManifestEntryName);
                VersionedProjectManifest manifest;
                using (var stream = manifestEntry.Open())
                {
                    manifest = VersionedProjectFormat.Deserialize<VersionedProjectManifest>(VersionedProjectFormat.ManifestSerializer, stream);
                }

                var result = ReadProject(archive, manifest);
                if (result.Assets.Count > 0 && assetSink == null && requireAssetSink)
                {
                    throw new InvalidDataException("The project contains image assets and requires an asset sink.");
                }

                ApplyAssets(result, assetSink);

                return result.Project;
            }
            catch (ProjectAssetRestoreException ex)
            {
                throw new InvalidOperationException("Project image asset restoration failed.", ex.InnerException ?? ex);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Versioned project archive could not be read.", ex);
            }
        }

        private static void ApplyAssets(VersionedProjectReadResult result, IProjectAssetSink? assetSink)
        {
            if (assetSink == null || result.Assets.Count == 0) return;
            try
            {
                if (assetSink is IProjectAssetBatchSink batchSink)
                {
                    batchSink.SaveImages(result.Project, result.Assets.Select(asset => new ProjectAssetRestore(asset.PageId, asset.ImageNumber, asset.Extension, asset.Content)).ToArray());
                    return;
                }

                if (result.Assets.Count > 1)
                {
                    throw new InvalidDataException("Multiple image assets require a batch asset sink to prevent partial restoration.");
                }

                var asset = result.Assets[0];
                using var content = new MemoryStream(asset.Content, writable: false);
                assetSink.SaveImage(asset.PageId, asset.ImageNumber, asset.Extension, content);
            }
            catch (ProjectAssetRestoreException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ProjectAssetRestoreException("Project image asset restoration failed.", ex);
            }
        }

        private static void ValidateArchive(ZipArchive archive)
        {
            if (archive.Entries.Count > VersionedProjectFormat.MaxArchiveEntries)
            {
                throw new InvalidDataException("The project archive contains too many entries.");
            }

            long totalSize = 0;
            foreach (var entry in archive.Entries)
            {
                VersionedProjectFormat.ValidateArchiveEntry(entry);
                totalSize = checked(totalSize + entry.Length);
                if (totalSize > VersionedProjectFormat.MaxArchiveSize)
                {
                    throw new InvalidDataException("The project archive is too large.");
                }
            }
        }

        private static VersionedProjectReadResult ReadProject(ZipArchive archive, VersionedProjectManifest manifest)
        {
            VersionedProjectFormat.ParseSupportedVersion(manifest.FormatVersion, nameof(manifest.FormatVersion));
            VersionedProjectFormat.ParseMinimumReaderVersion(manifest.MinimumReaderVersion, nameof(manifest.MinimumReaderVersion));

            if (!Guid.TryParse(manifest.ProjectId, out var projectId) || projectId == Guid.Empty)
            {
                throw new InvalidDataException("Manifest contains an invalid project ID.");
            }

            if (manifest.Pages == null || manifest.Pages.Count == 0)
            {
                throw new InvalidDataException("Manifest does not contain any pages.");
            }

            var pages = manifest.Pages.OrderBy(page => page.Order).ToArray();
            if (pages.Select(page => page.Order).Distinct().Count() != pages.Length || pages.Any(page => page.Order < 0) || pages.Select((page, index) => page.Order != index).Any(isInvalid => isInvalid))
            {
                throw new InvalidDataException("Manifest page order is invalid.");
            }

            var pageDocuments = new List<PageDocument>(pages.Length);
            var assets = new List<VersionedProjectAssetContent>();
            var pageIds = new HashSet<Guid>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var manifestPage in pages)
            {
                if (!Guid.TryParse(manifestPage.PageId, out var pageId) || pageId == Guid.Empty || !pageIds.Add(pageId))
                {
                    throw new InvalidDataException("Manifest contains a duplicate or invalid page ID.");
                }

                var expectedPath = VersionedProjectFormat.CanonicalPagePath(pageId);
                if (!string.Equals(manifestPage.RelativePath?.Replace('\\', '/'), expectedPath, StringComparison.Ordinal) || !paths.Add(expectedPath))
                {
                    throw new InvalidDataException("Manifest contains an invalid page path.");
                }

                var pageEntry = FindUniqueEntry(archive, expectedPath);
                VersionedPageFile pageFile;
                using (var stream = pageEntry.Open())
                {
                    pageFile = VersionedProjectFormat.Deserialize<VersionedPageFile>(VersionedProjectFormat.PageSerializer, stream);
                }

                if (!string.Equals(pageFile.PageId, pageId.ToString("D"), StringComparison.OrdinalIgnoreCase) || pageFile.Order != manifestPage.Order || !string.Equals(pageFile.Name, manifestPage.Name, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Page metadata does not match the manifest.");
                }

                var canvas = pageFile.Canvas ?? throw new InvalidDataException("Page is missing CanvasData.");
                var image1 = ReadAsset(archive, pageFile.Image1Path, pageId, 1, canvas.ImageData1);
                var image2 = ReadAsset(archive, pageFile.Image2Path, pageId, 2, canvas.ImageData2);
                if (image1 != null) assets.Add(image1);
                if (image2 != null) assets.Add(image2);
                pageDocuments.Add(new PageDocument(
                    pageId,
                    pageFile.Name ?? manifestPage.Name,
                    canvas,
                    pageFile.Objects ?? Enumerable.Empty<MojiData>(),
                    pageFile.Balloons ?? Enumerable.Empty<BalloonData>()));
            }

            if (string.IsNullOrWhiteSpace(manifest.ProjectName))
            {
                throw new InvalidDataException("Manifest does not contain a project name.");
            }

            return new VersionedProjectReadResult(new ProjectDocument(projectId, manifest.ProjectName, pageDocuments), assets);
        }

        private static VersionedProjectAssetContent? ReadAsset(ZipArchive archive, string? path, Guid pageId, int imageNumber, ImageData? imageData)
        {
            var canonicalPath = VersionedProjectFormat.ValidateAssetPath(path, pageId, imageNumber);
            if (canonicalPath == null)
            {
                VersionedProjectFormat.ValidateImageAsset(imageNumber, imageData, content: null);
                return null;
            }

            var entry = FindUniqueEntry(archive, canonicalPath);
            var extension = VersionedProjectFormat.NormalizeAssetExtension(Path.GetExtension(canonicalPath));
            using var stream = entry.Open();
            using var content = new MemoryStream();
            stream.CopyTo(content);
            var bytes = content.ToArray();
            VersionedProjectFormat.ValidateImageAsset(imageNumber, imageData, bytes);
            return new VersionedProjectAssetContent(pageId, imageNumber, extension, bytes);
        }

        private static ZipArchiveEntry FindUniqueEntry(ZipArchive archive, string entryName)
        {
            var entries = archive.Entries.Where(entry => string.Equals(entry.FullName.Replace('\\', '/'), entryName, StringComparison.Ordinal)).ToArray();
            if (entries.Length != 1) throw new InvalidDataException($"Required archive entry is missing or duplicated: {entryName}");
            return entries[0];
        }
    }

    internal sealed class VersionedProjectReadResult
    {
        public VersionedProjectReadResult(ProjectDocument project, IReadOnlyList<VersionedProjectAssetContent> assets)
        {
            Project = project;
            Assets = assets;
        }

        public ProjectDocument Project { get; }

        public IReadOnlyList<VersionedProjectAssetContent> Assets { get; }
    }

    internal sealed class VersionedProjectAssetContent
    {
        public VersionedProjectAssetContent(Guid pageId, int imageNumber, string extension, byte[] content)
        {
            PageId = pageId;
            ImageNumber = imageNumber;
            Extension = extension;
            Content = content;
        }

        public Guid PageId { get; }

        public int ImageNumber { get; }

        public string Extension { get; }

        public byte[] Content { get; }
    }

    internal sealed class ProjectAssetRestoreException : Exception
    {
        public ProjectAssetRestoreException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class VersionedProjectPersistence : IProjectReader, IProjectWriter
    {
        private readonly VersionedProjectReader _reader = new VersionedProjectReader();
        private readonly VersionedProjectWriter _writer = new VersionedProjectWriter();

        public ProjectDocument Read(string projectFilePath) => _reader.Read(projectFilePath);

        public ProjectDocument Read(string projectFilePath, IProjectAssetSink? assetSink) => _reader.Read(projectFilePath, assetSink);

        public void Write(string projectFilePath, ProjectDocument project, bool createBackup = false) => _writer.Write(projectFilePath, project, createBackup);

        public void Write(string projectFilePath, ProjectDocument project, IProjectAssetSource? assetSource, bool createBackup = false) => _writer.Write(projectFilePath, project, assetSource, createBackup);
    }
}
