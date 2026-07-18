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
            session.ExecutePage(pageId, target => target.RemoveBalloon(target.GetBalloon(balloonId)), "フキダシ削除");
            return true;
        }

        public static void Update(ProjectSession session, Guid pageId, Guid balloonId, Action<BalloonData> update)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (update == null) throw new ArgumentNullException(nameof(update));
            session.ExecutePage(pageId, page => page.UpdateBalloon(balloonId, update), "フキダシ更新");
        }

        public static void SetTail(ProjectSession session, Guid pageId, Guid balloonId, BalloonTailData? tail)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            session.ExecutePage(pageId, page => page.SetBalloonTail(balloonId, tail), "フキダシしっぽ変更");
        }

        public static void LinkText(ProjectSession session, Guid pageId, Guid balloonId, Guid textObjectId, TextLinkData? link = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            session.ExecutePage(pageId, page => page.LinkBalloonText(balloonId, textObjectId, link), "フキダシ文字リンク");
        }

        public static void UnlinkText(ProjectSession session, Guid pageId, Guid balloonId)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            session.ExecutePage(pageId, page => page.UnlinkBalloonText(balloonId), "フキダシ文字リンク解除");
        }
    }
}
