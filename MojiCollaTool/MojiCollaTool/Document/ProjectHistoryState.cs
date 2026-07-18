using System;
using System.Collections.Generic;
using System.Linq;

namespace MojiCollaTool
{
    /// <summary>
    /// 履歴用の小さなmementoです。ページ追加・削除にも対応するため順序だけは全件保持し、
    /// ページ内容はその操作が影響したページだけをdeep copyします。
    /// </summary>
    internal sealed class ProjectHistoryState
    {
        private ProjectHistoryState(Guid projectId, string name, IReadOnlyList<Guid> pageOrder,
            IReadOnlyDictionary<Guid, PageDocument> pages)
        {
            ProjectId = projectId;
            Name = name;
            PageOrder = pageOrder;
            Pages = pages;
        }

        public Guid ProjectId { get; }
        public string Name { get; }
        public IReadOnlyList<Guid> PageOrder { get; }
        public IReadOnlyDictionary<Guid, PageDocument> Pages { get; }

        public static ProjectHistoryState Capture(ProjectDocument project, IEnumerable<Guid> affectedPageIds)
        {
            var ids = new HashSet<Guid>(affectedPageIds);
            var pages = project.Pages
                .Where(page => ids.Contains(page.PageId))
                .ToDictionary(page => page.PageId, page => page.Clone(page.PageId, preserveObjectIds: true));
            return new ProjectHistoryState(project.ProjectId, project.Name,
                project.Pages.Select(page => page.PageId).ToArray(), pages);
        }

        public long EstimateBytes()
        {
            var objectCount = Pages.Values.Sum(page => page.Objects.Count);
            return Math.Max(512L, 1024L + PageOrder.Count * 32L + Pages.Count * 2048L + objectCount * 4096L);
        }
    }
}
