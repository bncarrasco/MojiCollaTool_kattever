using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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

                // Read the closed archive before replacing the user's file. This catches malformed XML and
                // incomplete page manifests while the previous file is still untouched.
                _ = new VersionedProjectReader().Read(temporaryPath);
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
                var pageFile = new VersionedPageFile
                {
                    PageId = page.PageId.ToString("D"),
                    Name = page.Name,
                    Order = page.Order,
                    Canvas = page.Canvas.ToLegacyData(),
                    Image1Path = CreateAssetPath(page.PageId, 1, image1),
                    Image2Path = CreateAssetPath(page.PageId, 2, image2),
                    Objects = page.MojiDatas.Select(PageDocument.CloneMojiData).ToList(),
                };
                WriteEntry(archive, VersionedProjectFormat.CanonicalPagePath(page.PageId), VersionedProjectFormat.Serialize(VersionedProjectFormat.PageSerializer, pageFile));
                WriteAssetEntry(archive, pageFile.Image1Path, image1);
                WriteAssetEntry(archive, pageFile.Image2Path, image2);
            }
        }

        private static string? CreateAssetPath(Guid pageId, int imageNumber, ProjectImageAsset? asset)
        {
            return asset == null ? null : VersionedProjectFormat.CanonicalAssetPath(pageId, imageNumber, asset.Extension);
        }

        private static void WriteEntry(ZipArchive archive, string entryName, byte[] content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(content, 0, content.Length);
        }

        private static void WriteAssetEntry(ZipArchive archive, string? entryName, ProjectImageAsset? asset)
        {
            if (entryName == null || asset == null) return;

            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var destination = entry.Open();
            if (asset.Content.CanSeek) asset.Content.Position = 0;
            asset.Content.CopyTo(destination);
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
    }

    public sealed class VersionedProjectReader : IProjectReader
    {
        public ProjectDocument Read(string projectFilePath)
        {
            return Read(projectFilePath, assetSink: null);
        }

        public ProjectDocument Read(string projectFilePath, IProjectAssetSink? assetSink)
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
                if (assetSink != null)
                {
                    foreach (var asset in result.Assets)
                    {
                        using var content = new MemoryStream(asset.Content, writable: false);
                        assetSink.SaveImage(asset.PageId, asset.ImageNumber, asset.Extension, content);
                    }
                }

                return result.Project;
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

                var image1 = ReadAsset(archive, pageFile.Image1Path, pageId, 1);
                var image2 = ReadAsset(archive, pageFile.Image2Path, pageId, 2);
                if (image1 != null) assets.Add(image1);
                if (image2 != null) assets.Add(image2);
                pageDocuments.Add(new PageDocument(pageId, pageFile.Name ?? manifestPage.Name, pageFile.Canvas, pageFile.Objects ?? Enumerable.Empty<MojiData>()));
            }

            if (string.IsNullOrWhiteSpace(manifest.ProjectName))
            {
                throw new InvalidDataException("Manifest does not contain a project name.");
            }

            return new VersionedProjectReadResult(new ProjectDocument(projectId, manifest.ProjectName, pageDocuments), assets);
        }

        private static VersionedProjectAssetContent? ReadAsset(ZipArchive archive, string? path, Guid pageId, int imageNumber)
        {
            var canonicalPath = VersionedProjectFormat.ValidateAssetPath(path, pageId, imageNumber);
            if (canonicalPath == null) return null;

            var entry = FindUniqueEntry(archive, canonicalPath);
            var extension = VersionedProjectFormat.NormalizeAssetExtension(Path.GetExtension(canonicalPath));
            using var stream = entry.Open();
            using var content = new MemoryStream();
            stream.CopyTo(content);
            return new VersionedProjectAssetContent(pageId, imageNumber, extension, content.ToArray());
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
