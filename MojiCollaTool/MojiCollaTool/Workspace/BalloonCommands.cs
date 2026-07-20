using System;

namespace MojiCollaTool
{
    /// <summary>
    /// Semantic balloon operations routed through ProjectSession history.
    /// UI command bindings are intentionally deferred to TASK-120/130.
    /// </summary>
    public static class BalloonCommands
    {
        public static Guid Add(ProjectSession session, Guid pageId, BalloonData balloon)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (balloon == null) throw new ArgumentNullException(nameof(balloon));
            var objectId = balloon.ObjectId;
            session.ExecutePage(pageId, page => page.AddBalloon(balloon), "フキダシ追加");
            return objectId;
        }

        public static bool Remove(ProjectSession session, Guid pageId, Guid balloonId)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var page = session.Document.GetPage(pageId);
            if (!page.ContainsBalloon(balloonId)) return false;
            var balloon = page.GetBalloon(balloonId);
            if (balloon.IsLocked || IsLinkedTextMutationBlocked(page, balloon)) return false;
            session.ExecutePage(pageId, target => target.RemoveBalloon(target.GetBalloon(balloonId)), "フキダシ削除");
            return true;
        }

        public static void Update(ProjectSession session, Guid pageId, Guid balloonId, Action<BalloonData> update,
            string description = "フキダシ更新", string? coalesceKey = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (update == null) throw new ArgumentNullException(nameof(update));
            var sourcePage = session.Document.GetPage(pageId);
            var before = sourcePage.GetBalloon(balloonId);
            if (before.IsLocked) return;
            var candidate = before.Clone();
            update(candidate);
            // TextLink is a relationship and has dedicated lock-aware link
            // commands. Do not let generic Update bypass them or create history.
            if (candidate.TextLink?.TextObjectId != before.TextLink?.TextObjectId) return;
            session.ExecutePage(pageId, page => page.UpdateBalloon(balloonId, update), description, coalesceKey);
        }

        public static void SetTail(ProjectSession session, Guid pageId, Guid balloonId, BalloonTailData? tail)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.Document.GetPage(pageId).GetBalloon(balloonId).IsLocked) return;
            session.ExecutePage(pageId, page => page.SetBalloonTail(balloonId, tail), "フキダシしっぽ変更");
        }

        public static void LinkText(ProjectSession session, Guid pageId, Guid balloonId, Guid textObjectId, TextLinkData? link = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var page = session.Document.GetPage(pageId);
            var balloon = page.GetBalloon(balloonId);
            if (!page.ContainsObject(textObjectId) || balloon.IsLocked || page.GetObject(textObjectId).IsLocked ||
                IsLinkedTextMutationBlocked(page, balloon)) return;
            session.ExecutePage(pageId, page => page.LinkBalloonText(balloonId, textObjectId, link), "フキダシ文字リンク");
        }

        public static void UnlinkText(ProjectSession session, Guid pageId, Guid balloonId)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var page = session.Document.GetPage(pageId);
            var balloon = page.GetBalloon(balloonId);
            if (balloon.IsLocked || IsLinkedTextMutationBlocked(page, balloon)) return;
            session.ExecutePage(pageId, page => page.UnlinkBalloonText(balloonId), "フキダシ文字リンク解除");
        }

        public static bool MoveComposition(ProjectSession session, Guid pageId, Guid balloonId, BalloonCompositionOrder operation)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var trial = session.Document.GetPage(pageId).Clone(preserveObjectIds: true);
            if (!trial.MoveBalloonComposition(balloonId, operation)) return false;
            session.ExecutePage(pageId, page => page.MoveBalloonComposition(balloonId, operation), "フキダシ重なり順変更");
            return true;
        }

        private static bool IsLinkedTextMutationBlocked(PageDocument page, BalloonData balloon)
            => balloon.TextLink?.TextObjectId is Guid textObjectId &&
               (!page.ContainsObject(textObjectId) || page.GetDocumentObject(textObjectId).IsLocked);
    }
}
