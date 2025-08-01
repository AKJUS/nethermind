// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Threading;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;

namespace Nethermind.Blockchain.Tracing;

/// <summary>
/// For testing
/// </summary>
public class AlwaysCancelTxTracer : ITxTracer
{
    private const string ErrorMessage = "Cancelling tracer invoked.";

    private static AlwaysCancelTxTracer? _instance;

    private AlwaysCancelTxTracer()
    {
    }

    public static AlwaysCancelTxTracer Instance
    {
        get { return LazyInitializer.EnsureInitialized(ref _instance, static () => new AlwaysCancelTxTracer()); }
    }

    public bool IsTracingReceipt => true;
    public bool IsTracingActions => true;
    public bool IsTracingOpLevelStorage => true;
    public bool IsTracingMemory => true;
    public bool IsTracingInstructions => true;
    public bool IsTracingRefunds => true;
    public bool IsTracingCode => true;
    public bool IsTracingStack => true;
    public bool IsTracingState => true;
    public bool IsTracingStorage => true;
    public bool IsTracingBlockHash => true;
    public bool IsTracingAccess => true;
    public bool IsTracingFees => true;
    public bool IsTracingLogs => true;

    public void MarkAsSuccess(Address recipient, GasConsumed gasSpent, byte[] output, LogEntry[] logs, Hash256? stateRoot = null) => throw new OperationCanceledException(ErrorMessage);

    public void MarkAsFailed(Address recipient, GasConsumed gasSpent, byte[] output, string? error, Hash256? stateRoot = null) => throw new OperationCanceledException(ErrorMessage);

    public void StartOperation(int pc, Instruction opcode, long gas, in ExecutionEnvironment env, int codeSection = 0, int functionDepth = 0) => throw new OperationCanceledException(ErrorMessage);

    public void ReportOperationError(EvmExceptionType error) => throw new OperationCanceledException(ErrorMessage);

    public void ReportOperationRemainingGas(long gas) => throw new OperationCanceledException(ErrorMessage);

    public void ReportLog(LogEntry log) => throw new OperationCanceledException(ErrorMessage);

    public void SetOperationMemorySize(ulong newSize) => throw new OperationCanceledException(ErrorMessage);

    public void ReportMemoryChange(long offset, in ReadOnlySpan<byte> data) => throw new OperationCanceledException(ErrorMessage);
    public void ReportStorageChange(in ReadOnlySpan<byte> key, in ReadOnlySpan<byte> value) => throw new OperationCanceledException(ErrorMessage);

    public void SetOperationStack(TraceStack stack) => throw new OperationCanceledException(ErrorMessage);

    public void ReportStackPush(in ReadOnlySpan<byte> stackItem) => throw new OperationCanceledException(ErrorMessage);

    public void SetOperationMemory(TraceMemory memoryTrace) => throw new OperationCanceledException(ErrorMessage);

    public void SetOperationStorage(Address address, UInt256 storageIndex, ReadOnlySpan<byte> newValue, ReadOnlySpan<byte> currentValue) => throw new OperationCanceledException(ErrorMessage);

    public void LoadOperationStorage(Address address, UInt256 storageIndex, ReadOnlySpan<byte> value) => throw new OperationCanceledException(ErrorMessage);

    public void ReportSelfDestruct(Address address, UInt256 balance, Address refundAddress) => throw new OperationCanceledException(ErrorMessage);

    public void ReportBalanceChange(Address address, UInt256? before, UInt256? after) => throw new OperationCanceledException(ErrorMessage);

    public void ReportCodeChange(Address address, byte[] before, byte[] after) => throw new OperationCanceledException(ErrorMessage);

    public void ReportNonceChange(Address address, UInt256? before, UInt256? after) => throw new OperationCanceledException(ErrorMessage);

    public void ReportAccountRead(Address address) => throw new OperationCanceledException(ErrorMessage);

    public void ReportStorageChange(in StorageCell storageCell, byte[] before, byte[] after) => throw new OperationCanceledException(ErrorMessage);

    public void ReportStorageRead(in StorageCell storageCell) => throw new OperationCanceledException(ErrorMessage);

    public void ReportAction(long gas, UInt256 value, Address from, Address to, ReadOnlyMemory<byte> input, ExecutionType callType, bool isPrecompileCall = false) => throw new OperationCanceledException(ErrorMessage);

    public void ReportActionEnd(long gas, ReadOnlyMemory<byte> output) => throw new OperationCanceledException(ErrorMessage);
    public void ReportActionError(EvmExceptionType exceptionType) => throw new OperationCanceledException(ErrorMessage);
    public void ReportActionRevert(long gas, ReadOnlyMemory<byte> output) => throw new OperationCanceledException(ErrorMessage);

    public void ReportActionEnd(long gas, Address deploymentAddress, ReadOnlyMemory<byte> deployedCode) => throw new OperationCanceledException(ErrorMessage);
    public void ReportBlockHash(Hash256 blockHash) => throw new OperationCanceledException(ErrorMessage);

    public void ReportByteCode(ReadOnlyMemory<byte> byteCode) => throw new OperationCanceledException(ErrorMessage);
    public void ReportGasUpdateForVmTrace(long refund, long gasAvailable) => throw new OperationCanceledException(ErrorMessage);
    public void ReportRefund(long refund) => throw new OperationCanceledException(ErrorMessage);
    public void ReportExtraGasPressure(long extraGasPressure) => throw new OperationCanceledException(ErrorMessage);
    public void ReportAccess(IReadOnlyCollection<Address> accessedAddresses, IReadOnlyCollection<StorageCell> accessedStorageCells) => throw new OperationCanceledException(ErrorMessage);
    public void ReportFees(UInt256 fees, UInt256 burntFees) => throw new OperationCanceledException(ErrorMessage);
    public void Dispose() { }
}
