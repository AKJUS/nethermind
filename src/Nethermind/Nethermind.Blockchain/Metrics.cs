// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.ComponentModel;
using System.Runtime.Serialization;
using Nethermind.Core.Attributes;
using Nethermind.Core.Metric;
using Nethermind.Int256;

// ReSharper disable InconsistentNaming

namespace Nethermind.Blockchain;

public static class Metrics
{
    [CounterMetric]
    [Description("Total MGas processed")]
    public static double Mgas { get; set; }

    [GaugeMetric]
    [Description("MGas processed per second")]
    public static double MgasPerSec { get; set; }

    [CounterMetric]
    [Description("Total number of transactions processed")]
    public static long Transactions { get; set; }

    [GaugeMetric]
    [Description("Total number of blocks processed")]
    public static long Blocks { get; set; }

    [CounterMetric]
    [Description("Total number of chain reorganizations")]
    public static long Reorganizations { get; set; }

    [GaugeMetric]
    [Description("Number of blocks awaiting for recovery of public keys from signatures.")]
    public static long RecoveryQueueSize { get; set; }

    [GaugeMetric]
    [Description("Number of blocks awaiting for processing.")]
    public static long ProcessingQueueSize { get; set; }

    [CounterMetric]
    [Description("Total number of sealed blocks")]
    public static long BlocksSealed { get; set; }

    [CounterMetric]
    [Description("Total number of failed block seals")]
    public static long FailedBlockSeals { get; set; }

    [GaugeMetric]
    [Description("Gas Used in processed blocks")]
    public static long GasUsed { get; set; }

    [GaugeMetric]
    [Description("Gas Limit for processed blocks")]
    public static long GasLimit { get; set; }

    [GaugeMetric]
    [Description("Total difficulty on the chain")]
    public static UInt256 TotalDifficulty { get; set; }

    [GaugeMetric]
    [Description("Difficulty of the last block")]
    public static UInt256 LastDifficulty { get; set; }

    [GaugeMetric]
    [Description("Indicator if blocks can be produced")]
    public static long CanProduceBlocks;

    [GaugeMetric]
    [Description("Number of ms to process the last processed block.")]
    public static long LastBlockProcessingTimeInMs;

    //EIP-2159: Common Prometheus Metrics Names for Clients
    [GaugeMetric]
    [Description("The current height of the canonical chain.")]
    [DataMember(Name = "ethereum_blockchain_height")]
    public static long BlockchainHeight { get; set; }

    //EIP-2159: Common Prometheus Metrics Names for Clients
    [GaugeMetric]
    [Description("The estimated highest block available.")]
    [DataMember(Name = "ethereum_best_known_block_number")]
    public static long BestKnownBlockNumber { get; set; }

    [GaugeMetric]
    [Description("Number of invalid blocks.")]
    public static long BadBlocks;

    [GaugeMetric]
    [Description("Number of invalid blocks with extra data set to 'Nethermind'.")]
    public static long BadBlocksByNethermindNodes;

    [GaugeMetric]
    [Description("State root calculation time")]
    public static double StateMerkleizationTime { get; set; }

    [DetailedMetric]
    [ExponentialPowerHistogramMetric(Start = 10, Factor = 1.2, Count = 30)]
    [Description("Histogram of block MGas per second")]
    public static IMetricObserver BlockMGasPerSec { get; set; } = new NoopMetricObserver();

    [DetailedMetric]
    [ExponentialPowerHistogramMetric(Start = 100, Factor = 1.25, Count = 50)]
    [Description("Histogram of block prorcessing time")]
    public static IMetricObserver BlockProcessingTimeMicros { get; set; } = new NoopMetricObserver();
}
