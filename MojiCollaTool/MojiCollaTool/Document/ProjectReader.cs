using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MojiCollaTool
{
    public enum ProjectFormatKind
    {
        Versioned,
        Legacy,
    }

    public sealed class ProjectReadWarning
    {
        public ProjectReadWarning(string code, string message)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public string Code { get; }

        public string Message { get; }
    }

    public sealed class ProjectReadResult
    {
        public ProjectReadResult(
            ProjectDocument project,
            ProjectFormatKind format,
            IEnumerable<ProjectReadWarning>? warnings = null)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));
            Format = format;
            Warnings = (warnings ?? Enumerable.Empty<ProjectReadWarning>()).ToArray();
        }

        public ProjectDocument Project { get; }

        public ProjectFormatKind Format { get; }

        public IReadOnlyList<ProjectReadWarning> Warnings { get; }

        public bool IsLegacy => Format == ProjectFormatKind.Legacy;
    }

    public interface IProjectResultReader
    {
        ProjectReadResult ReadResult(string projectFilePath);

        ProjectReadResult ReadResult(string projectFilePath, IProjectAssetSink? assetSink);
    }

    public static class ProjectFormatDetector
    {
        public const int MaxArchiveEntries = VersionedProjectFormat.MaxArchiveEntries;

        public static ProjectFormatKind Detect(string projectFilePath)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath))
            {
                throw new ArgumentException("Project path is required.", nameof(projectFilePath));
            }

            if (!File.Exists(projectFilePath))
            {
                throw new FileNotFoundException("Project file was not found.", projectFilePath);
            }

            try
            {
                using var archive = ZipFile.OpenRead(projectFilePath);
                if (archive.Entries.Count > MaxArchiveEntries)
                {
                    throw new InvalidDataException("The project archive contains too many entries.");
                }

                var names = archive.Entries
                    .Select(entry => entry.FullName.Replace('\\', '/'))
                    .ToArray();

                // The manifest is the format discriminator. A malformed manifest is
                // deliberately left to the versioned reader so it cannot be treated
                // as a legacy archive accidentally.
                if (names.Contains(VersionedProjectFormat.ManifestEntryName, StringComparer.Ordinal))
                {
                    return ProjectFormatKind.Versioned;
                }

                var rootEntries = new HashSet<string>(
                    names.Where(name => !name.Contains('/')),
                    StringComparer.OrdinalIgnoreCase);
                if (rootEntries.Contains("Info.txt") && rootEntries.Contains("CanvasData.xml"))
                {
                    return ProjectFormatKind.Legacy;
                }

                throw new InvalidDataException("The project archive format is not recognized.");
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("The project archive format could not be detected.", ex);
            }
        }
    }

    /// <summary>
    /// Detects the archive format and delegates to the corresponding reader.
    /// </summary>
    public sealed class ProjectReader : IProjectReader, IProjectResultReader
    {
        public ProjectDocument Read(string projectFilePath)
        {
            return ReadResult(projectFilePath).Project;
        }

        public ProjectDocument Read(string projectFilePath, IProjectAssetSink? assetSink)
        {
            return ReadResult(projectFilePath, assetSink).Project;
        }

        public ProjectReadResult ReadResult(string projectFilePath)
        {
            return ReadResult(projectFilePath, assetSink: null);
        }

        public ProjectReadResult ReadResult(string projectFilePath, IProjectAssetSink? assetSink)
        {
            return ProjectFormatDetector.Detect(projectFilePath) switch
            {
                ProjectFormatKind.Legacy => new LegacyProjectReader().ReadResult(projectFilePath, assetSink),
                ProjectFormatKind.Versioned => ReadVersioned(projectFilePath, assetSink),
                _ => throw new InvalidDataException("The project archive format is not recognized."),
            };
        }

        private static ProjectReadResult ReadVersioned(string projectFilePath, IProjectAssetSink? assetSink)
        {
            var project = new VersionedProjectReader().Read(projectFilePath, assetSink);
            return new ProjectReadResult(project, ProjectFormatKind.Versioned);
        }
    }

    /// <summary>
    /// Reads the original root-entry mctzip format and maps it to one page.
    /// </summary>
    public sealed class LegacyProjectReader : IProjectReader, IProjectResultReader
    {
        public const string LegacyImportWarningCode = "LEGACY_IMPORT";

        public const string LegacyImportWarningMessage =
            "旧形式のプロジェクトを読み込みました。新形式で保存すると移行されます。";

        public ProjectDocument Read(string projectFilePath)
        {
            return ReadResult(projectFilePath).Project;
        }

        public ProjectDocument Read(string projectFilePath, IProjectAssetSink? assetSink)
        {
            return ReadResult(projectFilePath, assetSink).Project;
        }

        public ProjectReadResult ReadResult(string projectFilePath)
        {
            return ReadResult(projectFilePath, assetSink: null);
        }

        public ProjectReadResult ReadResult(string projectFilePath, IProjectAssetSink? assetSink)
        {
            using var legacyData = DataIO.ReadProjectData(projectFilePath);
            var projectName = Path.GetFileNameWithoutExtension(Path.GetFullPath(projectFilePath));
            if (string.IsNullOrWhiteSpace(projectName)) projectName = "旧形式プロジェクト";

            var project = LegacyProjectDataAdapter.Import(
                projectName,
                legacyData.CanvasData,
                legacyData.MojiDatas);
            var assets = ReadAssets(legacyData, project.Pages[0].PageId);

            ApplyAssets(project, assets, assetSink);

            return new ProjectReadResult(
                project,
                ProjectFormatKind.Legacy,
                new[] { new ProjectReadWarning(LegacyImportWarningCode, LegacyImportWarningMessage) });
        }

        private static IReadOnlyList<ProjectAssetRestore> ReadAssets(LegacyProjectData legacyData, Guid pageId)
        {
            var assets = new List<ProjectAssetRestore>();
            AddAsset(assets, legacyData, pageId, 1, legacyData.CanvasData.ImageData1);
            AddAsset(assets, legacyData, pageId, 2, legacyData.CanvasData.ImageData2);
            return assets;
        }

        private static void AddAsset(
            ICollection<ProjectAssetRestore> assets,
            LegacyProjectData legacyData,
            Guid pageId,
            int imageNumber,
            ImageData imageData)
        {
            if (imageData == null || imageData.IsNullData()) return;

            var imagePath = DataIO.GetImagePath(imageNumber, legacyData.StagingDirectoryPath);
            if (string.IsNullOrEmpty(imagePath))
            {
                throw new InvalidDataException($"CanvasData references Image{imageNumber}, but the image file is missing.");
            }

            var extension = VersionedProjectFormat.NormalizeAssetExtension(Path.GetExtension(imagePath));
            assets.Add(new ProjectAssetRestore(pageId, imageNumber, extension, File.ReadAllBytes(imagePath)));
        }

        private static void ApplyAssets(
            ProjectDocument project,
            IReadOnlyList<ProjectAssetRestore> assets,
            IProjectAssetSink? assetSink)
        {
            if (assets.Count == 0) return;
            if (assetSink == null)
            {
                throw new InvalidDataException("The legacy project contains image assets and requires an asset sink.");
            }

            try
            {
                if (assetSink is IProjectAssetBatchSink batchSink)
                {
                    batchSink.SaveImages(project, assets);
                    return;
                }

                if (assets.Count > 1)
                {
                    throw new InvalidDataException("Multiple legacy image assets require a batch asset sink to prevent partial restoration.");
                }

                var asset = assets[0];
                using var content = new MemoryStream(asset.Content, writable: false);
                assetSink.SaveImage(asset.PageId, asset.ImageNumber, asset.Extension, content);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Legacy project image asset restoration failed.", ex);
            }
        }
    }
}
