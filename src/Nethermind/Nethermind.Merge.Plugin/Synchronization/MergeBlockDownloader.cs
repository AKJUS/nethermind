// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Logging;
using Nethermind.Synchronization;
using Nethermind.Synchronization.Blocks;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.Peers;
using Nethermind.Synchronization.Reporting;

namespace Nethermind.Merge.Plugin.Synchronization
{
    public class MergeBlockDownloader(
        IBeaconPivot beaconPivot,
        IBlockTree blockTree,
        IBlockValidator blockValidator,
        ISyncReport syncReport,
        IReceiptStorage receiptStorage,
        ISpecProvider specProvider,
        IBetterPeerStrategy betterPeerStrategy,
        IFullStateFinder fullStateFinder,
        IForwardHeaderProvider forwardHeaderProvider,
        ISyncPeerPool syncPeerPool,
        IReceiptsRecovery receiptsRecovery,
        IBlockProcessingQueue blockProcessingQueue,
        ISyncConfig syncConfig,
        ILogManager logManager)
        : BlockDownloader(
            blockTree,
            blockValidator,
            syncReport,
            receiptStorage,
            specProvider,
            betterPeerStrategy,
            fullStateFinder,
            forwardHeaderProvider,
            syncPeerPool,
            receiptsRecovery,
            blockProcessingQueue,
            syncConfig,
            logManager)
    {
        private readonly IBlockTree _blockTree = blockTree;
        private readonly ILogger _logger = logManager.GetClassLogger();

        protected override BlockTreeSuggestOptions GetSuggestOption(bool shouldProcess, Block currentBlock)
        {
            BlockTreeSuggestOptions suggestOptions =
                shouldProcess ? BlockTreeSuggestOptions.ShouldProcess : BlockTreeSuggestOptions.None;

            bool isKnownBeaconBlock = _blockTree.IsKnownBeaconBlock(currentBlock.Number, currentBlock.GetOrCalculateHash());
            if (_logger.IsTrace) _logger.Trace($"Current block {currentBlock}, BeaconPivot: {beaconPivot.PivotNumber}, IsKnownBeaconBlock: {isKnownBeaconBlock}");

            if (isKnownBeaconBlock)
            {
                suggestOptions |= BlockTreeSuggestOptions.FillBeaconBlock;
            }

            if (_logger.IsTrace)
                _logger.Trace(
                    $"MergeBlockDownloader - SuggestBlock {currentBlock}, IsKnownBeaconBlock {isKnownBeaconBlock} ShouldProcess: {shouldProcess}");
            return suggestOptions;
        }
    }
}
