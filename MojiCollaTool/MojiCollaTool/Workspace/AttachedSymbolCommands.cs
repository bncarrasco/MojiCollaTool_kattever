using System;
using System.Linq;

namespace MojiCollaTool
{
    /// <summary>
    /// History-aware model operations for attached symbols. UI code can use
    /// these adapters without taking a dependency on the history internals.
    /// </summary>
    public static class AttachedSymbolCommands
    {
        public static Guid Add(ProjectSession session, Guid pageId, AttachedSymbolData symbol, string description = "付加記号追加")
        {
            return TryAdd(session, pageId, symbol, out var id, description) ? id : Guid.Empty;
        }

        /// <summary>
        /// Adds an attached symbol when its parent exists and is editable.
        /// A false result is a refusal and never creates history or marks the session dirty.
        /// </summary>
        public static bool TryAdd(ProjectSession session, Guid pageId, AttachedSymbolData symbol, out Guid objectId,
            string description = "付加記号追加")
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            objectId = Guid.Empty;
            var page = session.Document.GetPage(pageId);
            if (symbol.ParentId is Guid parentId)
            {
                var parent = page.AllObjects.FirstOrDefault(item => item.ObjectId == parentId);
                if (parent == null || parent.IsLocked) return false;
            }

            var trial = page.Clone(preserveObjectIds: true);
            try { trial.AddAttachedSymbol(symbol); }
            catch (InvalidOperationException) { return false; }
            catch (System.IO.InvalidDataException) { return false; }

            session.ExecutePage(pageId, target => target.AddAttachedSymbol(symbol), description);
            objectId = symbol.ObjectId;
            return true;
        }

        public static void Update(ProjectSession session, Guid pageId, Guid symbolId, Action<AttachedSymbolData> update,
            string description = "付加記号編集", string? coalesceKey = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (update == null) throw new ArgumentNullException(nameof(update));
            var sourcePage = session.Document.GetPage(pageId);
            var symbol = sourcePage.GetAttachedSymbol(symbolId);
            if (symbol.IsLocked || (symbol.ParentId is Guid parentId && sourcePage.GetDocumentObject(parentId).IsLocked)) return;
            var candidate = symbol.Clone();
            update(candidate);
            // ParentId and IsDetached define ownership. Reanchor may still
            // change GraphemeAnchor, but generic Update cannot reparent/orphan.
            if (candidate.ParentId != symbol.ParentId || candidate.IsDetached != symbol.IsDetached) return;
            session.ExecutePage(pageId, page => page.ReplaceAttachedSymbol(symbolId, candidate), description, coalesceKey);
        }

        public static void Remove(ProjectSession session, Guid pageId, Guid symbolId, string description = "付加記号削除")
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var page = session.Document.GetPage(pageId);
            var symbol = page.GetAttachedSymbol(symbolId);
            if (symbol.IsLocked || (symbol.ParentId is Guid parentId && page.GetDocumentObject(parentId).IsLocked)) return;
            session.ExecutePage(pageId, page =>
            {
                if (!page.RemoveAttachedSymbol(symbolId)) throw new InvalidOperationException("Attached symbol was not found.");
            }, description);
        }

        public static void Reanchor(ProjectSession session, Guid pageId, Guid symbolId, int graphemeIndex,
            string description = "付加記号アンカー変更")
        {
            Update(session, pageId, symbolId, symbol => symbol.GraphemeAnchor = graphemeIndex, description);
        }
    }
}
