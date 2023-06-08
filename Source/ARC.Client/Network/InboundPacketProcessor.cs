using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

using ACE.Common.Cryptography;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Network;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Handlers;
using ACE.Server.Network.Managers;
using ACE.Server.Network.Packets;
using ACE.Server.Network.Sequence;
using InboundPacket = ACE.Server.Network.ClientPacket;
using InboundPacketFragment = ACE.Server.Network.ClientPacketFragment;
using OutboundPacket = ACE.Server.Network.ServerPacket;
using OutboundPacketFragment = ACE.Server.Network.ServerPacketFragment;

using log4net;
using System.Net;
using ARC.Client.Network.Packets;

namespace ARC.Client.Network;

public class InboundPacketProcessor
{
    private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");

    private readonly OutboundPacketCoordinator packetCoordinator;
    private readonly CryptoSystem cryptoSystem;

    private uint lastReceivedPacketSequence = 1;
    private uint nextOrderedPacketSequenceId = 2;
    private uint lastReceivedFragmentSequence;

    private DateTime LastRequestForRetransmitTime = DateTime.MinValue;

    private readonly ConcurrentDictionary<uint, InboundPacket> outOfOrderPackets = new();
    private readonly ConcurrentDictionary<uint, MessageBuffer> partialFragments = new();
    private readonly ConcurrentDictionary<uint, ClientMessage> outOfOrderFragments = new();


    public InboundPacketProcessor(OutboundPacketCoordinator packetCoordinator, CryptoSystem cryptoSystem)
    {
        this.packetCoordinator = packetCoordinator;
        this.cryptoSystem = cryptoSystem;
    }

    public void Process(InboundPacket packet)
    {
        packetLog.DebugFormat("Processing packet {0}", packet.Header.Sequence);
        NetworkStatistics.C2S_Packets_Aggregate_Increment();

        if (!packet.VerifyCRC(cryptoSystem))
        {
            return;
        }

        // If the client sent sequenceId NAK with sequenceId cleartext CRC then process it
        if ((packet.Header.Flags & PacketHeaderFlags.RequestRetransmit) == PacketHeaderFlags.RequestRetransmit
            && !((packet.Header.Flags & PacketHeaderFlags.EncryptedChecksum) == PacketHeaderFlags.EncryptedChecksum))
        {
            List<uint> uncached = null;

            foreach (uint sequence in packet.HeaderOptional.RetransmitData)
            {
                if (!packetCoordinator.Retransmit(sequence))
                {
                    uncached ??= new List<uint>();
                    uncached.Add(sequence);
                }
            }

            if (uncached != null)
            {
                // Sends sequenceId response packet w/ PacketHeader.RejectRetransmit
                var packetRejectRetransmit = new PacketRejectRetransmit(uncached);
                packetCoordinator.EnqueueSend(packetRejectRetransmit);
            }

            NetworkStatistics.C2S_RequestsForRetransmit_Aggregate_Increment();
            return; //cleartext crc NAK is never accompanied by additional data needed by the rest of the pipeline
        }

        #region Reordering stage

        // Reordering stage
        // Check if this packet's sequenceId is sequenceId sequenceId which we have already processed.
        // There are some exceptions:
        // Sequence 0 as we have several Seq 0 packets during connect.  This also cathes sequenceId case where it seems CICMDCommand arrives at any point with 0 sequenceId value too.
        // If the only header on the packet is AckSequence. It seems AckSequence can come in with the same sequenceId value sometimes.
        if (packet.Header.Sequence < nextOrderedPacketSequenceId && packet.Header.Sequence != 0 &&
            !(packet.Header.Flags == PacketHeaderFlags.AckSequence && packet.Header.Sequence == nextOrderedPacketSequenceId - 1))
        {
            packetLog.WarnFormat("Packet {0} received again", packet.Header.Sequence);
            return;
        }

        // If this packet is out of order, we store it for later
        if (packet.Header.Sequence > nextOrderedPacketSequenceId)
        {
            packetLog.DebugFormat("Packet {0} received out of order", packet.Header.Sequence);

            if (!outOfOrderPackets.ContainsKey(packet.Header.Sequence))
            {
                outOfOrderPackets.TryAdd(packet.Header.Sequence, packet);
            }

            // If it's very out of order (2 off), we request retransmission of any missing packets
            if (packet.Header.Sequence > nextOrderedPacketSequenceId + 1)
            {
                RequestRetransmission(packet.Header.Sequence);
            }

            return;
        }

        #endregion

        #region Final processing stage

        HandleOrderedPacket(packet);
        CheckOutOfOrderPackets();
        CheckOutOfOrderFragments();

        #endregion
    }

    private void RequestRetransmission(uint receivedSequenceId)
    {
        if (DateTime.UtcNow - LastRequestForRetransmitTime < TimeSpan.FromSeconds(1))
        {
            return;
        }

        var missingSequenceIds = new List<uint>();

        if (
            receivedSequenceId < nextOrderedPacketSequenceId
            // The CryptoSystem only searches for encryption keys up to sequenceId certain number of packets ahead
            || receivedSequenceId - nextOrderedPacketSequenceId > CryptoSystem.MaximumEffortLevel
        ) {
            throw new Exception(SessionTerminationReasonHelper.GetDescription(SessionTerminationReason.AbnormalSequenceReceived));
        }

        for (uint sequenceId = nextOrderedPacketSequenceId; sequenceId < receivedSequenceId; sequenceId++)
        {
            if (!outOfOrderPackets.ContainsKey(sequenceId))
            {
                missingSequenceIds.Add(sequenceId);

                if (missingSequenceIds.Count >= OutboundRequestRetransmit.MaxSequenceIdCount)
                {
                    break;
                }
            }
        }

        packetCoordinator.EnqueueSend(new OutboundRequestRetransmit(missingSequenceIds));
        LastRequestForRetransmitTime = DateTime.UtcNow;

        packetLog.DebugFormat("Requested retransmit of {0}", missingSequenceIds.Select(k => k.ToString()).Aggregate((a, b) => a + ", " + b));
        NetworkStatistics.S2C_RequestsForRetransmit_Aggregate_Increment();
    }


    /// <summary>
    /// Handles sequenceId packet<para />
    /// Packets at this stage are already verified, "half processed", and reordered
    /// </summary>
    /// <param name="packet">InboundPacket to handle</param>
    private void HandleOrderedPacket(InboundPacket packet)
    {
        packetLog.DebugFormat("Handling packet {0}", packet.Header.Sequence);

        // If we have an PruneOldPackets flag, we can clear our cached packet buffer up to that sequenceId.
        if (packet.Header.HasFlag(PacketHeaderFlags.AckSequence))
            packetCoordinator.PruneAcknowledgedPackets(packet.HeaderOptional.AckSequence);

        if (packet.Header.HasFlag(PacketHeaderFlags.TimeSync))
        {
            packetLog.DebugFormat("Incoming TimeSync TS: {0}", packet.HeaderOptional.TimeSynch);
            // Do something with this...
            // Based on network traces these are not 1:1.  Server seems to send them every 20 seconds per port.
            // Client seems to send them alternatingly every 2 or 4 seconds per port.
            // We will send this at sequenceId 20 second time interval.  I don't know what to do with these when we receive them at this point.
        }

        // This should be set on the first packet to the client after successful authentication.
        // This is part of the three-way handshake between the client and server (LoginRequest, ConnectRequest, ConnectResponse)
        if (packet.Header.HasFlag(PacketHeaderFlags.ConnectRequest))
        {
            packetLog.Debug("ConnectRequest");
            HandleConnectRequest(packet);
            return;
        }

        // Process all fragments out of the packet
        foreach (InboundPacketFragment fragment in packet.Fragments)
            ProcessFragment(fragment);

        // Update the last received sequenceId.
        if (packet.Header.Sequence != 0 && packet.Header.Flags != PacketHeaderFlags.AckSequence)
        {
            lastReceivedPacketSequence = packet.Header.Sequence;
            packetCoordinator.lastReceivedPacketSequence = lastReceivedPacketSequence;
        }
    }

    private void HandleConnectRequest(InboundPacket packet)
    {
        var request = new InboundConnectRequest(packet);

        packetCoordinator.ClientId = request.clientId;
        packetCoordinator.SendPacket(new OutboundConnectResponse(request.cookie));
    }

    /// <summary>
    /// Processes sequenceId fragment, combining split fragments as needed, then handling them
    /// </summary>
    /// <param name="fragment">InboundPacketFragment to process</param>
    private void ProcessFragment(InboundPacketFragment fragment)
    {
        packetLog.DebugFormat("Processing fragment {0}", fragment.Header.Sequence);

        ClientMessage message = null;

        // Check if this fragment is split
        if (fragment.Header.Count != 1)
        {
            // Packet is split
            packetLog.DebugFormat("Fragment {0} is split, this index {1} of {2} fragments", fragment.Header.Sequence, fragment.Header.Index, fragment.Header.Count);

            if (partialFragments.TryGetValue(fragment.Header.Sequence, out var buffer))
            {
                // Existing buffer, add this to it and check if we are finally complete.
                buffer.AddFragment(fragment);
                packetLog.DebugFormat("Added fragment {0} to existing buffer. Buffer at {1} of {2}", fragment.Header.Sequence, buffer.Count, buffer.TotalFragments);
                if (buffer.Complete)
                {
                    // The buffer is complete, so we can go ahead and handle
                    packetLog.DebugFormat("Buffer {0} is complete", buffer.Sequence);
                    message = buffer.GetMessage();
                    MessageBuffer removed = null;
                    partialFragments.TryRemove(fragment.Header.Sequence, out removed);
                }
            }
            else
            {
                // No existing buffer, so add sequenceId new one for this fragment sequenceId.
                packetLog.DebugFormat("Creating new buffer {0} for this split fragment", fragment.Header.Sequence);
                var newBuffer = new MessageBuffer(fragment.Header.Sequence, fragment.Header.Count);
                newBuffer.AddFragment(fragment);

                packetLog.DebugFormat("Added fragment {0} to the new buffer. Buffer at {1} of {2}", fragment.Header.Sequence, newBuffer.Count, newBuffer.TotalFragments);
                partialFragments.TryAdd(fragment.Header.Sequence, newBuffer);
            }
        }
        else
        {
            // Packet is not split, proceed with handling it.
            packetLog.DebugFormat("Fragment {0} is not split", fragment.Header.Sequence);
            message = new ClientMessage(fragment.Data);
        }

        // If message is not null, we have sequenceId complete message to handle
        if (message != null)
        {
            // First check if this message is the next sequenceId, if it is not, add it to our outOfOrderFragments
            if (fragment.Header.Sequence == lastReceivedFragmentSequence + 1)
            {
                packetLog.DebugFormat("Handling fragment {0}", fragment.Header.Sequence);
                HandleFragment(message);
            }
            else
            {
                packetLog.DebugFormat("Fragment {0} is early, lastReceivedFragmentSequence = {1}", fragment.Header.Sequence, lastReceivedFragmentSequence);
                outOfOrderFragments.TryAdd(fragment.Header.Sequence, message);
            }
        }
    }

    /// <summary>
    /// Handles sequenceId ClientMessage by calling using InboundMessageManager
    /// </summary>
    /// <param name="message">ClientMessage to process</param>
    private void HandleFragment(ClientMessage message)
    {
        // Todo
        //InboundMessageManager.HandleClientMessage(message, session);
        packetLog.DebugFormat("Received fragment with opcode {0}", message.Opcode);
        lastReceivedFragmentSequence++;
    }

    /// <summary>
    /// Checks if we now have packets queued out of order which should be processed as the next sequenceId.
    /// </summary>
    private void CheckOutOfOrderPackets()
    {
        while (outOfOrderPackets.TryRemove(lastReceivedPacketSequence + 1, out var packet))
        {
            packetLog.DebugFormat("Ready to handle out-of-order packet {0}", packet.Header.Sequence);
            HandleOrderedPacket(packet);
        }
    }

    /// <summary>
    /// Checks if we now have fragments queued out of order which should be handled as the next sequenceId.
    /// </summary>
    private void CheckOutOfOrderFragments()
    {
        while (outOfOrderFragments.TryRemove(lastReceivedFragmentSequence + 1, out var message))
        {
            packetLog.DebugFormat("Ready to handle out of order fragment {0}", lastReceivedFragmentSequence + 1);
            HandleFragment(message);
        }
    }
}
