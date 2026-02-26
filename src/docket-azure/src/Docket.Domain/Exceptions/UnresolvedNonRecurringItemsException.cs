using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Docket.Domain.Exceptions
{
    public class UnresolvedNonRecurringItemsException : DocketException
    {
        public Guid MinutesId { get; }
        public IReadOnlyList<Guid> BlockingItemIds { get; }

        public UnresolvedNonRecurringItemsException(
            Guid minutesId,
            IReadOnlyList<Guid> blockingItemIds)
            : base("DEFERRED_FINALIZED", $"Minutes {minutesId} cannot be finalized: " +
                   $"{blockingItemIds.Count} open item(s) on non-recurring topics must be resolved.")
        {
            MinutesId = minutesId;
            BlockingItemIds = blockingItemIds;
        }
    }
}
