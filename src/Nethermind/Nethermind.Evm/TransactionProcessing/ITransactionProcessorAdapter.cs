// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Evm.Tracing;

namespace Nethermind.Evm.TransactionProcessing
{
    public interface ITransactionProcessorAdapter
    {
        TransactionResult Execute(Transaction transaction, ITxTracer txTracer);
        void SetBlockExecutionContext(in BlockExecutionContext blockExecutionContext);
    }
}
