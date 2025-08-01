// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Text.Json.Serialization;
using System.Threading;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Blockchain.Tracing.GethStyle.Custom.Native.Call;

[JsonConverter(typeof(NativeCallTracerCallFrameConverter))]
public class NativeCallTracerCallFrame : IDisposable
{
    private const int Alive = 0;
    private const int Disposed = 1;
    private int _disposed = Alive;

    public Instruction Type { get; set; }

    public Address? From { get; set; }

    public long Gas { get; set; }

    public long GasUsed { get; set; }

    public Address? To { get; set; }

    public ArrayPoolList<byte>? Input { get; set; }

    public ArrayPoolList<byte>? Output { get; set; }

    public string? Error { get; set; }

    public string? RevertReason { get; set; }

    public ArrayPoolList<NativeCallTracerCallFrame> Calls { get; } = new(8);

    public ArrayPoolList<NativeCallTracerLogEntry>? Logs { get; set; }

    public UInt256? Value { get; set; }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, Disposed, Alive) == Alive)
        {
            Input?.Dispose();
            Output?.Dispose();
            Logs?.Dispose();
            Calls.DisposeRecursive();
        }
    }
}
