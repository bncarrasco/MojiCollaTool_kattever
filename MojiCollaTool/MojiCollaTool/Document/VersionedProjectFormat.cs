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
    }

    public interface IProjectWriter
    {
        void Write(string projectFilePath, ProjectDocument project, bool createBackup = false);
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
    }

    public sealed class VersionedProjectWriter : IProjectWriter
    {
        public void Write(string projectFilePath, ProjectDocument project, bool createBackup = false)
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
                WriteArchive(temporaryPath, project);

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

        private static void WriteArchive(string archivePath, ProjectDocument project)
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
                var pageFile = new VersionedPageFile
                {
                    PageId = page.PageId.ToString("D"),
                    Name = page.Name,
                    Order = page.Order,
                    Canvas = page.Canvas.ToLegacyData(),
                    Objects = page.MojiDatas.Select(PageDocument.CloneMojiData).ToList(),
                };
                WriteEntry(archive, VersionedProjectFormat.CanonicalPagePath(page.PageId), VersionedProjectFormat.Serialize(VersionedProjectFormat.PageSerializer, pageFile));
            }
        }

        private static void WriteEntry(ZipArchive archive, string entryName, byte[] content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(content, 0, content.Length);
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

                return ReadProject(archive, manifest);
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

        private static ProjectDocument ReadProject(ZipArchive archive, VersionedProjectManifest manifest)
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
            if (pages.Select(page => page.Order).Distinct().Count() != pages.Length || pages.Any(page => page.Order < 0))
            {
                throw new InvalidDataException("Manifest page order is invalid.");
            }

            var pageDocuments = new List<PageDocument>(pages.Length);
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

                if (!string.Equals(pageFile.PageId, pageId.ToString("D"), StringComparison.OrdinalIgnoreCase) || pageFile.Order != manifestPage.Order)
                {
                    throw new InvalidDataException("Page metadata does not match the manifest.");
                }

                pageDocuments.Add(new PageDocument(pageId, pageFile.Name ?? manifestPage.Name, pageFile.Canvas, pageFile.Objects ?? Enumerable.Empty<MojiData>()));
            }

            if (string.IsNullOrWhiteSpace(manifest.ProjectName))
            {
                throw new InvalidDataException("Manifest does not contain a project name.");
            }

            return new ProjectDocument(projectId, manifest.ProjectName, pageDocuments);
        }

        private static ZipArchiveEntry FindUniqueEntry(ZipArchive archive, string entryName)
        {
            var entries = archive.Entries.Where(entry => string.Equals(entry.FullName.Replace('\\', '/'), entryName, StringComparison.Ordinal)).ToArray();
            if (entries.Length != 1) throw new InvalidDataException($"Required archive entry is missing or duplicated: {entryName}");
            return entries[0];
        }
    }

    public sealed class VersionedProjectPersistence : IProjectReader, IProjectWriter
    {
        private readonly VersionedProjectReader _reader = new VersionedProjectReader();
        private readonly VersionedProjectWriter _writer = new VersionedProjectWriter();

        public ProjectDocument Read(string projectFilePath) => _reader.Read(projectFilePath);

        public void Write(string projectFilePath, ProjectDocument project, bool createBackup = false) => _writer.Write(projectFilePath, project, createBackup);
    }
}
