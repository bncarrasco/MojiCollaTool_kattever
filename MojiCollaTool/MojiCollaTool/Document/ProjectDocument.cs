using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MojiCollaTool
{
    /// <summary>
    /// 順序付きの複数ページを所有する永続化対象プロジェクトモデルです。
    /// </summary>
    public sealed class ProjectDocument
    {
        private readonly List<PageDocument> _pages = new List<PageDocument>();

        public ProjectDocument()
            : this("無題")
        {
        }

        public ProjectDocument(string name)
            : this(Guid.NewGuid(), name, createInitialPage: true)
        {
        }

        public ProjectDocument(Guid projectId, string name, IEnumerable<PageDocument> pages)
            : this(projectId, name, createInitialPage: false)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));

            foreach (var page in pages)
            {
                if (page == null) throw new ArgumentException("Page collection contains null.", nameof(pages));
                AddExistingPage(page.Clone());
            }

            if (_pages.Count == 0)
            {
                throw new ArgumentException("A project must contain at least one page.", nameof(pages));
            }
        }

        private ProjectDocument(Guid projectId, string name, bool createInitialPage)
        {
            if (projectId == Guid.Empty) throw new ArgumentException("Project ID must not be empty.", nameof(projectId));
            ProjectId = projectId;
            Name = name ?? throw new ArgumentNullException(nameof(name));

            if (createInitialPage)
            {
                AddPage();
            }
        }

        public Guid ProjectId { get; }

        /// <summary>
        /// ユーザーが入力したプロジェクト名。空白を含めて変更しません。
        /// </summary>
        public string Name { get; private set; }

        public IReadOnlyList<PageDocument> Pages => new ReadOnlyCollection<PageDocument>(_pages);

        public int PageCount => _pages.Count;

        public void Rename(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public PageDocument AddPage(string? name = null)
        {
            return InsertPage(_pages.Count, name);
        }

        public PageDocument InsertPage(int index, string? name = null)
        {
            if (index < 0 || index > _pages.Count) throw new ArgumentOutOfRangeException(nameof(index));

            var page = new PageDocument(name ?? CreateDefaultPageName(_pages.Count + 1));
            _pages.Insert(index, page);
            NormalizeOrder();
            return page;
        }

        public PageDocument ClonePage(Guid pageId, string? name = null)
        {
            var source = GetPage(pageId);
            var clone = source.Clone(name: name);
            _pages.Insert(source.Order + 1, clone);
            NormalizeOrder();
            return clone;
        }

        public PageDocument ClonePage(PageDocument page, string? name = null)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            return ClonePage(page.PageId, name);
        }

        public void RenamePage(Guid pageId, string name)
        {
            GetPage(pageId).Rename(name);
        }

        public PageDocument RemovePage(Guid pageId)
        {
            var page = GetPage(pageId);
            if (_pages.Count == 1)
            {
                throw new InvalidOperationException("A project must contain at least one page.");
            }

            _pages.Remove(page);
            NormalizeOrder();
            return page;
        }

        public void MovePage(Guid pageId, int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= _pages.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(targetIndex));
            }

            var page = GetPage(pageId);
            _pages.Remove(page);
            _pages.Insert(targetIndex, page);
            NormalizeOrder();
        }

        public PageDocument GetPage(Guid pageId)
        {
            var page = _pages.SingleOrDefault(candidate => candidate.PageId == pageId);
            return page ?? throw new KeyNotFoundException($"Page was not found: {pageId}");
        }

        public bool ContainsPage(Guid pageId)
        {
            return _pages.Any(page => page.PageId == pageId);
        }

        /// <summary>
        /// モデル全体を新しいProjectIdで複製します。
        /// </summary>
        public ProjectDocument Clone(string? name = null)
        {
            var clone = new ProjectDocument(Guid.NewGuid(), name ?? Name, createInitialPage: false);
            foreach (var page in _pages)
            {
                var clonedPage = page.Clone();
                clone._pages.Add(clonedPage);
            }

            clone.NormalizeOrder();
            return clone;
        }

        public static ProjectDocument FromLegacy(
            string name,
            CanvasData canvas,
            IEnumerable<MojiData> mojiDatas)
        {
            return LegacyProjectDataAdapter.Import(name, canvas, mojiDatas);
        }

        internal void AddExistingPage(PageDocument page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (ContainsPage(page.PageId)) throw new InvalidOperationException("Duplicate page ID.");

            _pages.Add(page);
            NormalizeOrder();
        }

        private void NormalizeOrder()
        {
            for (var index = 0; index < _pages.Count; index++)
            {
                _pages[index].Order = index;
            }
        }

        private static string CreateDefaultPageName(int number)
        {
            return number.ToString("00");
        }
    }
}
