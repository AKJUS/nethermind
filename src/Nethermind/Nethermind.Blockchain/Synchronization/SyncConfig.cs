// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only
using Nethermind.Config;
using Nethermind.Core.Extensions;
using Nethermind.Db;

namespace Nethermind.Blockchain.Synchronization
{
    [ConfigCategory(Description = "Configuration of the synchronization modes.")]
    public class SyncConfig : ISyncConfig
    {
        private bool _synchronizationEnabled = true;
        private bool _fastSync;

        public static ISyncConfig Default { get; } = new SyncConfig();
        public static ISyncConfig WithFullSyncOnly { get; } = new SyncConfig { FastSync = false };
        public static ISyncConfig WithFastSync { get; } = new SyncConfig { FastSync = true };
        public static ISyncConfig WithEth2Merge { get; } = new SyncConfig { FastSync = false, BlockGossipEnabled = false };

        public bool NetworkingEnabled { get; set; } = true;

        public bool SynchronizationEnabled
        {
            get => NetworkingEnabled && _synchronizationEnabled;
            set => _synchronizationEnabled = value;
        }

        public long? FastSyncCatchUpHeightDelta { get; set; } = 8192;
        bool ISyncConfig.FastBlocks { get; set; }
        public bool UseGethLimitsInFastBlocks { get; set; } = true;
        public bool FastSync { get => _fastSync || SnapSync; set => _fastSync = value; }
        public bool DownloadHeadersInFastSync { get; set; } = true;
        public bool DownloadBodiesInFastSync { get; set; } = true;
        public bool DownloadReceiptsInFastSync { get; set; } = true;
        public long AncientBodiesBarrier { get; set; }
        public long AncientReceiptsBarrier { get; set; }
        public string PivotTotalDifficulty { get; set; }
        private string _pivotNumber = "0";
        public string PivotNumber
        {
            get => FastSync || SnapSync ? _pivotNumber : "0";
            set => _pivotNumber = value;
        }

        private string? _pivotHash;
        public string? PivotHash
        {
            get => FastSync || SnapSync ? _pivotHash : null;
            set => _pivotHash = value;
        }
        public int MaxAttemptsToUpdatePivot { get; set; } = int.MaxValue;
        public bool SnapSync { get; set; } = false;
        public int SnapSyncAccountRangePartitionCount { get; set; } = 8;
        public bool FixReceipts { get; set; } = false;
        public bool FixTotalDifficulty { get; set; } = false;
        public long FixTotalDifficultyStartingBlock { get; set; } = 1;
        public long? FixTotalDifficultyLastBlock { get; set; } = null;
        public bool StrictMode { get; set; } = false;
        public bool BlockGossipEnabled { get; set; } = true;
        public bool NonValidatorNode { get; set; } = false;
        public ITunableDb.TuneType TuneDbMode { get; set; } = ITunableDb.TuneType.HeavyWrite;
        public ITunableDb.TuneType BlocksDbTuneDbMode { get; set; } = ITunableDb.TuneType.EnableBlobFiles;
        public int MaxProcessingThreads { get; set; }
        public bool ExitOnSynced { get; set; } = false;
        public int ExitOnSyncedWaitTimeSec { get; set; } = 60;
        public int MallocTrimIntervalSec { get; set; } = 300;
        public bool? SnapServingEnabled { get; set; } = null;
        public int MultiSyncModeSelectorLoopTimerMs { get; set; } = 1000;
        public int SyncDispatcherEmptyRequestDelayMs { get; set; } = 10;
        public int SyncDispatcherAllocateTimeoutMs { get; set; } = 1000;
        public bool NeedToWaitForHeader { get; set; }
        public bool VerifyTrieOnStateSyncFinished { get; set; }
        public bool TrieHealing { get; set; } = true;
        public int StateMaxDistanceFromHead { get; set; } = 128;
        public int StateMinDistanceFromHead { get; set; } = 32;
        public bool GCOnFeedFinished { get; set; } = true;
        /// <summary>
        /// Additional delay in blocks between best suggested header and synced state to allow faster state switching for PoW chains
        /// with higher block processing frequency. Effectively this is the max allowed difference between best header (used as sync
        /// pivot) and synced state block, to assume that state is synced and node can start processing blocks
        /// </summary>
        public int HeaderStateDistance { get; set; } = 0;

        public ulong FastHeadersMemoryBudget { get; set; } = (ulong)128.MB();
        public bool EnableSnapSyncStorageRangeSplit { get; set; } = false;
        public long ForwardSyncDownloadBufferMemoryBudget { get; set; } = 200.MiB();
        public long ForwardSyncBlockProcessingQueueMemoryBudget { get; set; } = 200.MiB();

        public override string ToString()
        {
            return
                $"SyncConfig details. FastSync {FastSync}, PivotNumber: {PivotNumber} DownloadHeadersInFastSync {DownloadHeadersInFastSync}, DownloadBodiesInFastSync {DownloadBodiesInFastSync}, DownloadReceiptsInFastSync {DownloadReceiptsInFastSync}, AncientBodiesBarrier {AncientBodiesBarrier}, AncientReceiptsBarrier {AncientReceiptsBarrier}";
        }

    }
}
