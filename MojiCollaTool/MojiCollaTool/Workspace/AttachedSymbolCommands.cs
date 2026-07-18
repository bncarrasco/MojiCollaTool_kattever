using System;

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
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            var id = symbol.ObjectId;
            session.ExecutePage(pageId, page => page.AddAttachedSymbol(symbol), description);
            return id;
        }

        public static void Update(ProjectSession session, Guid pageId, Guid symbolId, Action<AttachedSymbolData> update,
            string description = "付加記号編集", string? coalesceKey = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            session.ExecutePage(pageId, page => page.UpdateAttachedSymbol(symbolId, update), description, coalesceKey);
        }

        public static void Remove(ProjectSession session, Guid pageId, Guid symbolId, string description = "付加記号削除")
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
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
