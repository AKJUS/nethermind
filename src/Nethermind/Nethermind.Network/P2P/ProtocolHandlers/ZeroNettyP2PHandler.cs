// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.IO;
using System.Net.Sockets;
using DotNetty.Buffers;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using Nethermind.Core.Exceptions;
using Nethermind.Logging;
using Nethermind.Network.Rlpx;
using Nethermind.Stats.Model;
using Snappier;

namespace Nethermind.Network.P2P.ProtocolHandlers;

public class ZeroNettyP2PHandler : SimpleChannelInboundHandler<ZeroPacket>
{
    private readonly ISession _session;
    private readonly ILogger _logger;

    public bool SnappyEnabled { get; private set; }

    public ZeroNettyP2PHandler(ISession session, ILogManager logManager)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logManager?.GetClassLogger<ZeroNettyP2PHandler>() ?? throw new ArgumentNullException(nameof(logManager));
    }

    public void Init(IPacketSender packetSender, IChannelHandlerContext context)
    {
        _session.Init(5, context, packetSender);
    }

    public override void ChannelRegistered(IChannelHandlerContext context)
    {
        if (_logger.IsDebug) _logger.Debug($"Registering {nameof(ZeroNettyP2PHandler)}");
        base.ChannelRegistered(context);
    }

    protected override void ChannelRead0(IChannelHandlerContext ctx, ZeroPacket input)
    {
        IByteBuffer content = input.Content;
        int readableBytes = content.ReadableBytes;
        if (readableBytes > SnappyParameters.MaxSnappyLength)
        {
            _session.InitiateDisconnect(DisconnectReason.BreachOfProtocol, "Max message size exceeded");
            return;
        }
        if (SnappyEnabled)
        {
            int uncompressedLength = Snappy.GetUncompressedLength(
                content.Array.AsSpan(content.ArrayOffset + content.ReaderIndex, readableBytes));

            if (uncompressedLength > SnappyParameters.MaxSnappyLength)
            {
                _session.InitiateDisconnect(DisconnectReason.BreachOfProtocol, "Max message size exceeded");
                return;
            }

            if (readableBytes > SnappyParameters.MaxSnappyLength / 4)
            {
                if (_logger.IsTrace) _logger.Trace($"Big Snappy message of length {readableBytes}");
            }
            else
            {
                if (_logger.IsTrace) _logger.Trace($"Uncompressing with Snappy a message of length {readableBytes}");
            }

            IByteBuffer output = ctx.Allocator.Buffer(uncompressedLength);

            try
            {
                int length = Snappy.Decompress(
                    content.Array.AsSpan(content.ArrayOffset + content.ReaderIndex, readableBytes),
                    output.Array.AsSpan(output.ArrayOffset + output.WriterIndex));
                output.SetWriterIndex(output.WriterIndex + length);
            }
            catch (InvalidDataException)
            {
                output.SafeRelease();
                // Data is not compressed sometimes, so we pass directly.
                _session.ReceiveMessage(input);
                return;
            }
            catch (Exception)
            {
                content.SkipBytes(readableBytes);
                output.SafeRelease();
                throw;
            }

            content.SkipBytes(readableBytes);
            ZeroPacket outputPacket = new(output);
            try
            {
                outputPacket.PacketType = input.PacketType;
                _session.ReceiveMessage(outputPacket);
            }
            finally
            {
                outputPacket.SafeRelease();
            }
        }
        else
        {
            _session.ReceiveMessage(input);
        }
    }

    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
        //In case of SocketException we log it as debug to avoid noise
        string clientId = _session?.Node?.ToString(Node.Format.Console) ?? $"unknown {_session?.RemoteHost}";
        if (exception is SocketException)
        {
            if (_logger.IsTrace) _logger.Trace($"Error in communication with {clientId} (SocketException): {exception}");
        }
        else
        {
            if (_logger.IsDebug) _logger.Debug($"Error in communication with {clientId}: {exception}");
        }

        if (exception is IInternalNethermindException)
        {
            // Do nothing as we don't want to drop peer for internal issue.
        }
        else if (_session?.Node?.IsStatic != true)
        {
            _session.InitiateDisconnect(DisconnectReason.Exception,
                $"Error in communication with {clientId} ({exception.GetType().Name}): {exception.Message}");
        }
        else
        {
            base.ExceptionCaught(context, exception);
        }
    }

    public void EnableSnappy()
    {
        SnappyEnabled = true;
    }
}
