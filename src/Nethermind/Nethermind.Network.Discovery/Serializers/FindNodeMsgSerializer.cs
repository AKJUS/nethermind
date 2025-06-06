// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac.Features.AttributeFilters;
using DotNetty.Buffers;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;
using Nethermind.Network.Discovery.Messages;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Network.Discovery.Serializers;

public class FindNodeMsgSerializer : DiscoveryMsgSerializerBase, IZeroInnerMessageSerializer<FindNodeMsg>
{
    public FindNodeMsgSerializer(IEcdsa ecdsa, [KeyFilter(IProtectedPrivateKey.NodeKey)] IPrivateKeyGenerator nodeKey, INodeIdResolver nodeIdResolver)
        : base(ecdsa, nodeKey, nodeIdResolver) { }

    public void Serialize(IByteBuffer byteBuffer, FindNodeMsg msg)
    {
        int length = GetLength(msg, out int contentLength);

        byteBuffer.MarkIndex();
        PrepareBufferForSerialization(byteBuffer, length, (byte)msg.MsgType);
        NettyRlpStream stream = new(byteBuffer);
        stream.StartSequence(contentLength);
        stream.Encode(msg.SearchedNodeId);
        stream.Encode(msg.ExpirationTime);

        byteBuffer.ResetIndex();
        AddSignatureAndMdc(byteBuffer, length + 1);
    }

    public FindNodeMsg Deserialize(IByteBuffer msgBytes)
    {
        (PublicKey FarPublicKey, _, IByteBuffer Data) = PrepareForDeserialization(msgBytes);
        NettyRlpStream rlpStream = new(Data);
        rlpStream.ReadSequenceLength();
        byte[] searchedNodeId = rlpStream.DecodeByteArray();
        long expirationTime = rlpStream.DecodeLong();

        FindNodeMsg findNodeMsg = new(FarPublicKey, expirationTime, searchedNodeId);
        return findNodeMsg;
    }

    public int GetLength(FindNodeMsg msg, out int contentLength)
    {
        contentLength = Rlp.LengthOf(msg.SearchedNodeId);
        contentLength += Rlp.LengthOf(msg.ExpirationTime);

        return Rlp.LengthOfSequence(contentLength);
    }
}
