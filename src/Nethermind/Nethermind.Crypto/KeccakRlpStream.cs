// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Runtime.InteropServices;
using Nethermind.Core.Crypto;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Crypto
{
    public sealed class KeccakRlpStream : RlpStream
    {
        private readonly KeccakHash _keccakHash;

        public Hash256 GetHash() => new Hash256(_keccakHash.GenerateValueHash());

        public ValueHash256 GetValueHash() => _keccakHash.GenerateValueHash();

        public KeccakRlpStream()
        {
            KeccakHash keccakHash = KeccakHash.Create();
            _keccakHash = keccakHash;
        }

        public override void Write(ReadOnlySpan<byte> bytesToWrite)
        {
            _keccakHash.Update(bytesToWrite);
        }

        public override void WriteByte(byte byteToWrite)
        {
            _keccakHash.Update(MemoryMarshal.CreateSpan(ref byteToWrite, 1));
        }

        protected override void WriteZero(int length)
        {
            Span<byte> zeros = stackalloc byte[length];
            Write(zeros);
        }

        public override byte ReadByte()
        {
            throw new NotSupportedException("Cannot read form Keccak");
        }

        public override Span<byte> Read(int length)
        {
            throw new NotSupportedException("Cannot read form Keccak");
        }

        public override byte PeekByte()
        {
            throw new NotSupportedException("Cannot read form Keccak");
        }

        protected override byte PeekByte(int offset)
        {
            throw new NotSupportedException("Cannot read form Keccak");
        }

        protected override void SkipBytes(int length)
        {
            WriteZero(length);
        }

        public override int Position
        {
            get => throw new NotSupportedException("Cannot read form Keccak");
            set => throw new NotSupportedException("Cannot read form Keccak");
        }

        public override int Length => throw new NotSupportedException("Cannot read form Keccak");

        protected override string Description => "|KeccakRlpSTream|description missing|";
    }
}
