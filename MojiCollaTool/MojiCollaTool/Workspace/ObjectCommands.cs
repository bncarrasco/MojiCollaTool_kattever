using System;

namespace MojiCollaTool
{
    /// <summary>
    /// History-aware commands shared by text, balloons, and attached symbols.
    /// The page model remains the single source of truth for block resolution,
    /// lock validation, and no-op detection.
    /// </summary>
    public static class ObjectCommands
    {
        public static bool MoveOrder(ProjectSession session, Guid pageId, Guid objectId,
            ObjectOrderOperation operation)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var page = session.Document.GetPage(pageId);
            var trial = page.Clone(preserveObjectIds: true);
            if (!trial.MoveObjectOrder(objectId, operation)) return false;
            session.ExecutePage(pageId, target => target.MoveObjectOrder(objectId, operation),
                "オブジェクト重なり順変更");
            return true;
        }

        public static bool Move(ProjectSession session, Guid pageId, Guid objectId,
            ObjectOrderOperation operation)
            => MoveOrder(session, pageId, objectId, operation);

        public static bool SetLocked(ProjectSession session, Guid pageId, Guid objectId, bool isLocked)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var page = session.Document.GetPage(pageId);
            if (page.GetDocumentObject(objectId).IsLocked == isLocked) return false;
            session.ExecutePage(pageId, target => target.SetObjectLocked(objectId, isLocked),
                isLocked ? "オブジェクトをロック" : "オブジェクトのロックを解除");
            return true;
        }

        public static bool SetLock(ProjectSession session, Guid pageId, Guid objectId, bool isLocked)
            => SetLocked(session, pageId, objectId, isLocked);

        public static bool Lock(ProjectSession session, Guid pageId, Guid objectId)
            => SetLocked(session, pageId, objectId, true);

        public static bool Unlock(ProjectSession session, Guid pageId, Guid objectId)
            => SetLocked(session, pageId, objectId, false);
    }

    /// <summary>
    /// Explicitly named alias for callers that prefer the page-object wording.
    /// Both entry points route to the same production command implementation.
    /// </summary>
    public static class PageObjectCommands
    {
        public static bool MoveOrder(ProjectSession session, Guid pageId, Guid objectId,
            ObjectOrderOperation operation)
            => ObjectCommands.MoveOrder(session, pageId, objectId, operation);

        public static bool SetLocked(ProjectSession session, Guid pageId, Guid objectId, bool isLocked)
            => ObjectCommands.SetLocked(session, pageId, objectId, isLocked);

        public static bool Lock(ProjectSession session, Guid pageId, Guid objectId)
            => ObjectCommands.Lock(session, pageId, objectId);

        public static bool Unlock(ProjectSession session, Guid pageId, Guid objectId)
            => ObjectCommands.Unlock(session, pageId, objectId);
    }
}
