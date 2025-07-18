// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain.Tracing;
using Nethermind.Evm.Tracing;

namespace Nethermind.Consensus.Producers
{
    public interface IBlockProducerInfo
    {
        IBlockProducer BlockProducer { get; }
        IBlockProductionCondition Condition { get; }
        IBlockTracer BlockTracer => NullBlockTracer.Instance;
    }
}
