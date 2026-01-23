using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CoopHelper.Infrastructure
{
    /// <summary>
    /// Provides constants for header identifiers used in synchronization operations.
    /// </summary>
    /// <remarks>The <see cref="Headers"/> class defines static fields representing specific header values
    /// that are used to identify synced entity types across clients. Values 1~9999 are reserved for this helper.</remarks>
    public static class Headers
    {
        // TODO move all header definitions here
        public static readonly int SyncedFlagTrigger = 29;
        public static readonly int SyncedJelly = 30;
        public static readonly int SyncedTheo = 31;
    }
}
