using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace MojiCollaTool
{
    /// <summary>
    /// Non-destructive relationship data for a flat balloon merge group.
    /// Member BalloonData instances remain the authoritative editable data.
    /// </summary>
    [Serializable]
    public sealed class BalloonMergeData
    {
        public Guid MergeId { get; set; } = Guid.NewGuid();

        public Guid PrimaryBalloonId { get; set; }

        [XmlArray("MemberIds")]
        [XmlArrayItem("BalloonId")]
        public List<Guid> MemberIds { get; set; } = new();

        public BalloonMergeData Clone()
        {
            return new BalloonMergeData
            {
                MergeId = MergeId,
                PrimaryBalloonId = PrimaryBalloonId,
                MemberIds = MemberIds.ToList(),
            };
        }

        public void Validate(IReadOnlyCollection<Guid> balloonIds)
        {
            if (balloonIds == null) throw new ArgumentNullException(nameof(balloonIds));
            if (MergeId == Guid.Empty) throw new InvalidDataException("Balloon merge ID must not be empty.");
            if (PrimaryBalloonId == Guid.Empty) throw new InvalidDataException("Balloon merge primary ID must not be empty.");
            if (MemberIds == null || MemberIds.Count < 2) throw new InvalidDataException("Balloon merge must contain at least two members.");
            if (MemberIds.Any(id => id == Guid.Empty)) throw new InvalidDataException("Balloon merge member ID must not be empty.");
            if (MemberIds.Distinct().Count() != MemberIds.Count) throw new InvalidDataException("Balloon merge contains duplicate members.");
            if (!MemberIds.Contains(PrimaryBalloonId)) throw new InvalidDataException("Balloon merge primary is not a member.");
            if (MemberIds.Any(id => !balloonIds.Contains(id))) throw new InvalidDataException("Balloon merge member was not found on this page.");
        }
    }
}
