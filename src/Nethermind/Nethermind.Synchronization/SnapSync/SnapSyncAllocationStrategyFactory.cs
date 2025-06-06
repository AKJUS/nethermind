// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain.Synchronization;
using Nethermind.Consensus;
using Nethermind.Stats;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.Peers.AllocationStrategies;

namespace Nethermind.Synchronization.SnapSync
{
    public class SnapSyncAllocationStrategyFactory : StaticPeerAllocationStrategyFactory<SnapSyncBatch>
    {

        private static readonly IPeerAllocationStrategy DefaultStrategy =
            new SatelliteProtocolPeerAllocationStrategy<ISnapSyncPeer>(new BySpeedStrategy(TransferSpeedType.SnapRanges, true), "snap");

        public SnapSyncAllocationStrategyFactory() : base(DefaultStrategy)
        {
        }
    }
}
