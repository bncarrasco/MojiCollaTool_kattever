using System;

namespace MojiCollaTool
{
    /// <summary>History-aware semantic commands for non-destructive balloon merge groups.</summary>
    public static class BalloonMergeCommands
    {
        public static bool Merge(ProjectSession session, Guid pageId, Guid primaryBalloonId, Guid candidateBalloonId)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var trial = session.Document.GetPage(pageId).Clone(preserveObjectIds: true);
            if (!trial.MergeBalloons(primaryBalloonId, candidateBalloonId)) return false;
            session.ExecutePage(pageId, page => page.MergeBalloons(primaryBalloonId, candidateBalloonId), "フキダシ合体");
            return true;
        }

        public static bool Unmerge(ProjectSession session, Guid pageId, Guid balloonId)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var trial = session.Document.GetPage(pageId).Clone(preserveObjectIds: true);
            if (!trial.UnmergeBalloons(balloonId)) return false;
            session.ExecutePage(pageId, page => page.UnmergeBalloons(balloonId), "フキダシ合体解除");
            return true;
        }

        public static bool Move(ProjectSession session, Guid pageId, Guid balloonId,
            double deltaX, double deltaY, string? coalesceKey = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var trial = session.Document.GetPage(pageId).Clone(preserveObjectIds: true);
            if (!trial.MoveBalloonMerge(balloonId, deltaX, deltaY)) return false;
            session.ExecutePage(pageId, page => page.MoveBalloonMerge(balloonId, deltaX, deltaY),
                "合体フキダシ移動", coalesceKey);
            return true;
        }
    }
}
