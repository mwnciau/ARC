using ACE.Common.Cryptography;
using ACE.Server.Network;
using ACE.Server.Network.Enum;
using ACE.Server.Network.Packets;
using ARC.Client.Network.Packets;
using InboundPacket = ACE.Server.Network.ClientPacket;
using InboundPacketFragment = ACE.Server.Network.ClientPacketFragment;
using log4net;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ARC.Client.Network;

public class InboundPacketProcessor
{
    private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");

    private readonly CryptoSystem cryptoSystem;

    private uint nextOrderedPacketSequenceId = 2;
    private uint nextOrderedFragmentSequence = 1;

    private DateTime LastRequestForRetransmitTime = DateTime.MinValue;

    private readonly ConcurrentDictionary<uint, InboundPacket> outOfOrderPackets = new();
    private readonly ConcurrentDictionary<uint, MessageBuffer> partialFragments = new();
    private readonly ConcurrentDictionary<uint, ClientMessage> outOfOrderFragments = new();

    private readonly Session session;

    public InboundPacketProcessor(Session session)
    {
        this.session = session;
    }

    public void Process(InboundPacket packet)
    {
        packetLog.DebugFormat("Processing packet {0}", packet.Header.Sequence);
        NetworkStatistics.S2C_Packets_Aggregate_Increment();

        // If the packet has an invalid checksum, ignore it
        // Todo should there be a better way to access this?
        if (
            !packet.Header.HasFlag(PacketHeaderFlags.ConnectRequest)
            &&!packet.VerifyCRC(session.PacketQueue.ConnectionData?.ServerCryptoVerifier)
        ) {
            return;
        }

        if (packet.Header.HasFlag(PacketHeaderFlags.RequestRetransmit)) {
            session.PacketQueue.Retransmit(packet.HeaderOptional.RetransmitData);

            // RequestRetransmit packets are never accompanied by additional data
            return;
        }

        if (PacketHasAlreadyBeenProcessed(packet)) {
            packetLog.WarnFormat("Packet {0} received again", packet.Header.Sequence);

            return;
        }

        if (packet.Header.Sequence > nextOrderedPacketSequenceId) {
            packetLog.DebugFormat("Packet {0} received out of order", packet.Header.Sequence);
            StoreOutOfOrderPacket(packet);

            return;
        }

        HandleOrderedPacket(packet);
        CheckOutOfOrderPackets();
        CheckOutOfOrderFragments();
    }

    private bool PacketHasAlreadyBeenProcessed(InboundPacket packet)
    {
        return
            // Check if we have already processed this packet
            packet.Header.Sequence < nextOrderedPacketSequenceId
            // Exception: several sequence 0 packets are used during connection
            && packet.Header.Sequence != 0
            // Exception: the sequence for AckSequence packets is sometimes the same as the previous packet
            && !(packet.Header.HasFlag(PacketHeaderFlags.AckSequence) && packet.Header.Sequence == nextOrderedPacketSequenceId - 1);
    }

    private void StoreOutOfOrderPacket(InboundPacket packet)
    {
        if (!outOfOrderPackets.ContainsKey(packet.Header.Sequence)) {
            outOfOrderPackets.TryAdd(packet.Header.Sequence, packet);
        }

        // If it's very out of order (2 off), we request retransmission of any missing packets
        if (packet.Header.Sequence > nextOrderedPacketSequenceId + 1) {
            RequestRetransmission(packet.Header.Sequence);
        }
    }

    private void RequestRetransmission(uint receivedSequenceId)
    {
        Debug.Assert(receivedSequenceId > nextOrderedPacketSequenceId);

        if (DateTime.UtcNow - LastRequestForRetransmitTime < TimeSpan.FromSeconds(1)) {
            return;
        }

        // Check if the sequence ID is too far ahead: the CryptoSystem only checks encryption keys a certain number of
        // packets ahead.
        if (receivedSequenceId - nextOrderedPacketSequenceId > CryptoSystem.MaximumEffortLevel) {
            throw new Exception(SessionTerminationReasonHelper.GetDescription(SessionTerminationReason.AbnormalSequenceReceived));
        }

        List<uint> sequenceIds = MissingSequenceIdsUpTo(receivedSequenceId);

        session.PacketQueue.EnqueueSend(new OutboundRequestRetransmit(sequenceIds));
        LastRequestForRetransmitTime = DateTime.UtcNow;

        packetLog.DebugFormat("Requested retransmit of {0}", sequenceIds.Select(k => k.ToString()).Aggregate((a, b) => a + ", " + b));
        NetworkStatistics.S2C_RequestsForRetransmit_Aggregate_Increment();
    }

    private List<uint> MissingSequenceIdsUpTo(uint receivedSequenceId)
    {
        var missingSequenceIds = new List<uint>();

        for (uint sequenceId = nextOrderedPacketSequenceId; sequenceId < receivedSequenceId; sequenceId++)
        {
            if (outOfOrderPackets.ContainsKey(sequenceId))
            {
                continue;
            }

            missingSequenceIds.Add(sequenceId);

            if (missingSequenceIds.Count >= OutboundRequestRetransmit.MaxSequenceIdCount)
            {
                break;
            }
        }

        return missingSequenceIds;
    }

    private void HandleOrderedPacket(InboundPacket packet)
    {
        packetLog.DebugFormat("Handling packet {0}", packet.Header.Sequence);

        // If we have an AckSequence flag, we can clear our cached packet buffer up to that sequenceId
        if (packet.Header.HasFlag(PacketHeaderFlags.AckSequence)) {
            session.PacketQueue.PruneAcknowledgedPackets(packet.HeaderOptional.AckSequence);
        }

        if (packet.Header.HasFlag(PacketHeaderFlags.TimeSync)) {
            packetLog.DebugFormat("Incoming TimeSync TS: {0}", packet.HeaderOptional.TimeSynch);
            // Todo: Do something with this...
            // Based on network traces these are not 1:1.  Server seems to send them every 20 seconds per port.
            // Client seems to send them alternatingly every 2 or 4 seconds per port.
            // We will send this at sequenceId 20 second time interval.  I don't know what to do with these when we receive them at this point.
        }

        // This should be set on the first packet to the client after successful authentication. Itis part of the
        // three-way handshake between the client and server (LoginRequest, ConnectRequest, ConnectResponse).
        if (packet.Header.HasFlag(PacketHeaderFlags.ConnectRequest)) {
            packetLog.Debug("ConnectRequest");
            session.PacketQueue.HandleConnectRequest(new InboundConnectRequest(packet));

            return;
        }

        foreach (InboundPacketFragment fragment in packet.Fragments.Cast<InboundPacketFragment>()) {
            ProcessFragment(fragment);
        }

        if (packet.Header.Sequence != 0 && packet.Header.Flags != PacketHeaderFlags.AckSequence) {
            nextOrderedPacketSequenceId = packet.Header.Sequence + 1;
            session.PacketQueue.lastReceivedPacketSequence = packet.Header.Sequence;
        }
    }

    /// <summary>
    /// Processes sequenceId fragment, combining split fragments as needed, then handling them
    /// </summary>
    private void ProcessFragment(InboundPacketFragment fragment)
    {
        packetLog.DebugFormat("Processing fragment {0}", fragment.Header.Sequence);

        ClientMessage message = null;

        // Check if this fragment is split
        if (fragment.Header.Count != 1) {
            // Packet is split
            packetLog.DebugFormat("Fragment {0} is split, this index {1} of {2} fragments", fragment.Header.Sequence, fragment.Header.Index, fragment.Header.Count);

            if (partialFragments.TryGetValue(fragment.Header.Sequence, out var buffer)) {
                // Existing buffer, add this to it and check if we are finally complete.
                buffer.AddFragment(fragment);
                packetLog.DebugFormat("Added fragment {0} to existing buffer. Buffer at {1} of {2}", fragment.Header.Sequence, buffer.Count, buffer.TotalFragments);
                if (buffer.Complete)
                {
                    // The buffer is complete, so we can go ahead and handle
                    packetLog.DebugFormat("Buffer {0} is complete", buffer.Sequence);
                    message = buffer.GetMessage();
                    partialFragments.TryRemove(fragment.Header.Sequence, out _);
                }
            } else {
                // No existing buffer, so add sequenceId new one for this fragment sequenceId.
                packetLog.DebugFormat("Creating new buffer {0} for this split fragment", fragment.Header.Sequence);
                var newBuffer = new MessageBuffer(fragment.Header.Sequence, fragment.Header.Count);
                newBuffer.AddFragment(fragment);

                packetLog.DebugFormat("Added fragment {0} to the new buffer. Buffer at {1} of {2}", fragment.Header.Sequence, newBuffer.Count, newBuffer.TotalFragments);
                partialFragments.TryAdd(fragment.Header.Sequence, newBuffer);
            }
        } else {
            // Packet is not split, proceed with handling it.
            packetLog.DebugFormat("Fragment {0} is not split", fragment.Header.Sequence);
            message = new ClientMessage(fragment.Data);
        }

        // If message is not null, we have a complete message to handle
        if (message != null) {
            // First check if this message is the next sequenceId, if it is not, add it to our outOfOrderFragments
            if (fragment.Header.Sequence == nextOrderedFragmentSequence) {
                packetLog.DebugFormat("Handling fragment {0}", fragment.Header.Sequence);
                HandleFragment(message);
            } else {
                packetLog.DebugFormat("Fragment {0} is early, nextOrderedFragmentSequence = {1}", fragment.Header.Sequence, nextOrderedFragmentSequence);
                outOfOrderFragments.TryAdd(fragment.Header.Sequence, message);
            }
        }
    }

    private void HandleFragment(ClientMessage message)
    {
        InboundMessageManager.HandleInboundMessage(message, session);
        packetLog.DebugFormat("Received fragment with opcode {0}", message.Opcode);
        nextOrderedFragmentSequence++;
    }

    /// <summary>
    /// Process any stored packets that are now in order
    /// </summary>
    private void CheckOutOfOrderPackets()
    {
        while (outOfOrderPackets.TryRemove(nextOrderedPacketSequenceId, out var packet)) {
            packetLog.DebugFormat("Ready to handle out-of-order packet {0}", packet.Header.Sequence);
            HandleOrderedPacket(packet);
        }
    }

    /// <summary>
    /// Process any stored fragments that are now in order
    /// </summary>
    private void CheckOutOfOrderFragments()
    {
        while (outOfOrderFragments.TryRemove(nextOrderedFragmentSequence, out var message)) {
            packetLog.DebugFormat("Ready to handle out of order fragment {0}", nextOrderedFragmentSequence);
            HandleFragment(message);
        }
    }
}
