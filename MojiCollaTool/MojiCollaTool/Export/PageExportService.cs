using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MojiCollaTool.Export
{
    public enum PageExportScope
    {
        CurrentPage,
        AllPages,
    }

    public enum PageExportFormat
    {
        Png,
        Jpeg,
    }

    public sealed class PageExportRequest
    {
        public PageExportRequest(
            ProjectDocument project,
            PageExportScope scope,
            PageDocument? currentPage,
            string outputDirectory,
            string filePrefix,
            PageExportFormat format)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));
            Scope = scope;
            CurrentPage = currentPage;
            OutputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
            FilePrefix = filePrefix ?? throw new ArgumentNullException(nameof(filePrefix));
            Format = format;
        }

        public ProjectDocument Project { get; }
        public PageExportScope Scope { get; }
        public PageDocument? CurrentPage { get; }
        public string OutputDirectory { get; }
        public string FilePrefix { get; }
        public PageExportFormat Format { get; }
    }

    public sealed class PageExportItem
    {
        internal PageExportItem(PageDocument page, int index, string filePath)
        {
            Page = page;
            Index = index;
            FilePath = filePath;
        }

        public PageDocument Page { get; }
        public int Index { get; }
        public string FilePath { get; }
    }

    public sealed class PageExportPlan
    {
        internal PageExportPlan(string outputDirectory, PageExportFormat format, IReadOnlyList<PageExportItem> items)
        {
            OutputDirectory = outputDirectory;
            Format = format;
            Items = items;
        }

        public string OutputDirectory { get; }
        public PageExportFormat Format { get; }
        public IReadOnlyList<PageExportItem> Items { get; }
    }

    public sealed class PageExportFailure
    {
        internal PageExportFailure(PageExportItem item, Exception exception)
        {
            Item = item;
            Exception = exception;
        }

        public PageExportItem Item { get; }
        public Exception Exception { get; }
    }

    public sealed class PageExportResult
    {
        internal PageExportResult(IReadOnlyList<PageExportItem> succeeded, IReadOnlyList<PageExportFailure> failed)
        {
            Succeeded = succeeded;
            Failed = failed;
        }

        public IReadOnlyList<PageExportItem> Succeeded { get; }
        public IReadOnlyList<PageExportFailure> Failed { get; }
        public int SuccessCount => Succeeded.Count;
        public int FailureCount => Failed.Count;
    }

    public sealed class PageExportStartException : IOException
    {
        public PageExportStartException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    public sealed class PageExportCollisionException : IOException
    {
        public PageExportCollisionException(IReadOnlyList<string> paths)
            : base("出力先に既存ファイルがあります。")
        {
            Paths = paths;
        }

        public IReadOnlyList<string> Paths { get; }
    }

    /// <summary>
    /// ページ順、ファイル名、衝突、失敗継続をUIから分離した一括出力サービスです。
    /// 既存ファイルは上書きせず、rendererが生成した一時ファイルを成功時だけ確定名へ移動します。
    /// </summary>
    public sealed class PageExportService
    {
        public PageExportPlan CreatePlan(PageExportRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Project.Pages.Count == 0) throw new PageExportStartException("出力対象ページがありません。");

            var orderedPages = request.Project.Pages.OrderBy(page => page.Order).ToArray();
            PageDocument[] pages;
            if (request.Scope == PageExportScope.CurrentPage)
            {
                if (request.CurrentPage == null || !request.Project.ContainsPage(request.CurrentPage.PageId))
                {
                    throw new PageExportStartException("現在のページが出力対象プロジェクトにありません。");
                }

                pages = new[] { request.Project.GetPage(request.CurrentPage.PageId) };
            }
            else
            {
                pages = orderedPages;
            }

            var outputDirectory = NormalizeDirectory(request.OutputDirectory);
            var prefix = SanitizeFilePart(request.FilePrefix, "MojiColla");
            var extension = request.Format == PageExportFormat.Jpeg ? "jpg" : "png";
            var padding = Math.Max(2, orderedPages.Length.ToString().Length);
            var items = pages.Select(page =>
            {
                var index = page.Order + 1;
                var pageName = SanitizeFilePart(page.Name, "page");
                var fileName = $"{prefix}_{index.ToString($"D{padding}")}_{pageName}.{extension}";
                return new PageExportItem(page, index, Path.Combine(outputDirectory, fileName));
            }).ToArray();

            var duplicatePaths = items
                .GroupBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicatePaths.Length > 0)
            {
                throw new PageExportCollisionException(duplicatePaths);
            }

            var existingPaths = items.Where(item => File.Exists(item.FilePath)).Select(item => item.FilePath).ToArray();
            if (existingPaths.Length > 0) throw new PageExportCollisionException(existingPaths);

            return new PageExportPlan(outputDirectory, request.Format, items);
        }

        public PageExportResult Execute(
            PageExportPlan plan,
            Action<PageExportItem, string, PageExportFormat> renderer)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            EnsureOutputDirectory(plan.OutputDirectory);

            var succeeded = new List<PageExportItem>();
            var failed = new List<PageExportFailure>();
            foreach (var item in plan.Items)
            {
                var temporaryPath = Path.Combine(
                    plan.OutputDirectory,
                    $".{Path.GetFileName(item.FilePath)}.{Guid.NewGuid():N}.tmp");
                try
                {
                    renderer(item, temporaryPath, plan.Format);
                    if (!File.Exists(temporaryPath))
                    {
                        throw new IOException("レンダラーが画像ファイルを作成しませんでした。");
                    }

                    File.Move(temporaryPath, item.FilePath);
                    succeeded.Add(item);
                }
                catch (Exception ex)
                {
                    TryDelete(temporaryPath);
                    failed.Add(new PageExportFailure(item, ex));
                }
            }

            return new PageExportResult(succeeded, failed);
        }

        public static string SanitizeFilePart(string? value, string fallback)
        {
            var source = string.IsNullOrWhiteSpace(value) ? fallback : value;
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(source!.Length);
            foreach (var character in source)
            {
                builder.Append(character < ' ' || invalid.Contains(character) ? '_' : character);
            }

            var result = builder.ToString().TrimEnd(' ', '.');
            if (result.Length == 0) result = fallback;
            if (IsReservedWindowsName(result)) result += "_";
            return result;
        }

        private static string NormalizeDirectory(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new PageExportStartException("出力先フォルダーを指定してください。");
            try
            {
                return Path.GetFullPath(outputDirectory);
            }
            catch (Exception ex)
            {
                throw new PageExportStartException("出力先フォルダーが不正です。", ex);
            }
        }

        private static void EnsureOutputDirectory(string outputDirectory)
        {
            try
            {
                Directory.CreateDirectory(outputDirectory);
            }
            catch (Exception ex)
            {
                throw new PageExportStartException("出力先フォルダーを作成できないため、出力を開始できません。", ex);
            }
        }

        private static bool IsReservedWindowsName(string value)
        {
            var stem = value.Split('.')[0].TrimEnd(' ', '.');
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)) return true;
            return (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                   stem.Length == 4 && stem[3] is >= '1' and <= '9';
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // A cleanup failure is reported as the page failure itself.
            }
        }
    }
}
