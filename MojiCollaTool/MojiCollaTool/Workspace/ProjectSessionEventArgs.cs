using System;

namespace MojiCollaTool
{
    public sealed class ProjectSessionEventArgs : EventArgs
    {
        public ProjectSessionEventArgs(ProjectSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public ProjectSession Session { get; }
    }
}
