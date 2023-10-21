using System.Collections.Generic;
using System.Linq;

namespace Rip2p
{
    public class NetworkSyncIdRangeService
    {
        private const ushort RangePerConnection = 500;

        private ushort _nextRangeStart;
        private List<ushort> _freedRangeMins = new();

        public (ushort minId, ushort maxId) GetFreeRange()
        {
            ushort minId;
            if (_freedRangeMins.Any())
            {
                minId = _freedRangeMins.Last();
                _freedRangeMins.RemoveAt(_freedRangeMins.Count - 1);
            }
            else
            {
                minId = _nextRangeStart;
                _nextRangeStart += RangePerConnection;
            }

            return (minId, (ushort) (minId + RangePerConnection - 1));
        }

        public void FreeRange(ushort minId)
        {
            _freedRangeMins.Add(minId);
        }
    }
}