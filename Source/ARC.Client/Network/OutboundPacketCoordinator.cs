using System.Collections.Concurrent;

using log4net;

using ACE.Server.Network.GameMessages;
using ACE.Server.Network;

using OutboundPacket = ACE.Server.Network.ServerPacket;
using OutboundPacketFragment = ACE.Server.Network.ServerPacketFragment;
using ACE.Server.Entity;
using ACE.Server.Network.Enum;
using ACE.Server.Network.Sequence;
using System.Buffers;
using System.Net.Sockets;
using System.Text;
using ACE.Server.Network.Packets;
using Org.BouncyCastle.Asn1.Cmp;

namespace ARC.Client.Network;

public class OutboundPacketCoordinator
{
    private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");

    private readonly Connection connection;
    public readonly SessionConnectionData ConnectionData = new SessionConnectionData();

    public ushort ClientId = 0;

    private readonly Object[] currentBundleLocks = new Object[(int)GameMessageGroup.QueueMax];
    private readonly NetworkBundle[] currentBundles = new NetworkBundle[(int)GameMessageGroup.QueueMax];
    private readonly ConcurrentQueue<OutboundPacket> packetQueue = new();


    /// <summary>
    /// This is referenced from many threads:<para />
    /// ConnectionListener.OnDataReceieve()->Session.HandlePacket()->This.HandlePacket(packet), This path can come from any client or other thinkable object.<para />
    /// WorldManager.UpdateWorld()->Session.Update(lastTick)->This.Update(lastTick)
    /// </summary>
    private readonly ConcurrentDictionary<uint /*seq*/, OutboundPacket> cachedPackets = new ConcurrentDictionary<uint /*seq*/, OutboundPacket>();

    /// <summary>
    /// Number of seconds to retain cachedPackets
    /// </summary>
    private const int cachedPacketRetentionTime = 120;

    private static readonly TimeSpan cachedPacketPruneInterval = TimeSpan.FromSeconds(5);
    private DateTime lastCachedPacketPruneTime;

    private DateTime nextSend = DateTime.UtcNow;
    private const int minimumTimeBetweenBundles = 5; // 5ms

    private const int timeBetweenTimeSync = 20000; // 20s
    private const int timeBetweenAck = 2000; // 2s

    // Resync will be started after ConnectResponse, and should immediately be sent then, so no delay here.
    // Fun fact: even though we send the server time in the ConnectRequest, client doesn't seem to use it?  Therefore we must TimeSync early so client doesn't see a skew when we send it later.
    private DateTime? nextResync = null;

    // Ack should be sent after a 2 second delay, so start enabled with the delay.
    // Sending this too early seems to cause issues with clients disconnecting.
    private DateTime nextAck = DateTime.UtcNow.AddMilliseconds(timeBetweenAck);
    public uint lastReceivedPacketSequence;

    public OutboundPacketCoordinator(Connection connection)
    {
        this.connection = connection;

        for (int i = 0; i < currentBundles.Length; i++)
        {
            currentBundleLocks[i] = new object();
            currentBundles[i] = new NetworkBundle();
        }
    }

    /// <summary>
    /// Prunes the cachedPackets dictionary
    /// Checks if we should send the current bundle and then flushes all pending packets.
    /// </summary>
    public void Update()
    {
        PruneOldPackets();

        for (int i = 0; i < currentBundles.Length; i++)
        {
            NetworkBundle bundleToSend = null;

            var group = (GameMessageGroup)i;

            var currentBundleLock = currentBundleLocks[i];
            lock (currentBundleLock)
            {
                var currentBundle = currentBundles[i];

                if (group == GameMessageGroup.InvalidQueue)
                {
                    if (!currentBundle.TimeSync && nextResync != null && DateTime.UtcNow > nextResync)
                    {
                        packetLog.DebugFormat("Setting to send TimeSync packet");
                        currentBundle.TimeSync = true;
                        currentBundle.EncryptedChecksum = true;
                        nextResync = DateTime.UtcNow.AddMilliseconds(timeBetweenTimeSync);
                    }

                    if (!currentBundle.SendAck && DateTime.UtcNow > nextAck)
                    {
                        packetLog.DebugFormat("Setting to send ACK packet");
                        currentBundle.SendAck = true;
                        nextAck = DateTime.UtcNow.AddMilliseconds(timeBetweenAck);
                    }

                    if (currentBundle.NeedsSending && DateTime.UtcNow >= nextSend)
                    {
                        packetLog.DebugFormat("Swapping bundle");
                        // Swap out bundle so we can process it
                        bundleToSend = currentBundle;
                        currentBundles[i] = new NetworkBundle();
                    }
                }
                else
                {
                    if (currentBundle.NeedsSending && DateTime.UtcNow >= nextSend)
                    {
                        packetLog.DebugFormat("Swapping bundle");
                        // Swap out bundle so we can process it
                        bundleToSend = currentBundle;
                        currentBundles[i] = new NetworkBundle();
                    }
                }
            }

            // Send our bundle if we have one
            // We should be able to execute this outside the lock as Sending is single threaded
            // and all future writes from other threads will go to the new bundle
            if (bundleToSend != null)
            {
                SendBundle(bundleToSend, group);
                nextSend = DateTime.UtcNow.AddMilliseconds(minimumTimeBetweenBundles);
            }
        }

        FlushPackets();
    }

    private void PruneOldPackets()
    {
        if (DateTime.UtcNow - lastCachedPacketPruneTime < cachedPacketPruneInterval)
        {
            return;
        }

        lastCachedPacketPruneTime = DateTime.UtcNow;

        var currentTime = (ushort)Timers.PortalYearTicks;

        // Make sure our comparison still works when ushort wraps every 18.2 hours.
        var removalList = cachedPackets.Values.Where(x => (currentTime >= x.Header.Time ? currentTime : currentTime + ushort.MaxValue) - x.Header.Time > cachedPacketRetentionTime);

        foreach (var packet in removalList)
            cachedPackets.TryRemove(packet.Header.Sequence, out _);
    }

    public void PruneAcknowledgedPackets(uint sequence)
    {
        var removalList = cachedPackets.Keys.Where(x => x < sequence);

        foreach (var key in removalList)
            cachedPackets.TryRemove(key, out _);
    }

    public void EnqueueSend(params GameMessage[] messages)
    {
        if (!connection.IsActive())
            return;

        foreach (var message in messages)
        {
            var grp = message.Group;
            var currentBundleLock = currentBundleLocks[(int)grp];
            lock (currentBundleLock)
            {
                var currentBundle = currentBundles[(int)grp];
                currentBundle.EncryptedChecksum = true;
                packetLog.DebugFormat("Enqueuing Message {0}", message.Opcode);
                currentBundle.Enqueue(message);
            }
        }
    }

    public void EnqueueSend(params ServerPacket[] packets)
    {
        if (!connection.IsActive())
            return;

        foreach (var packet in packets)
        {
            packetLog.DebugFormat("Enqueuing Packet {0}", packet.GetHashCode());
            packetQueue.Enqueue(packet);
        }
    }

    public void Retransmit(List<uint> sequenceIds)
    {
        NetworkStatistics.S2C_RequestsForRetransmit_Aggregate_Increment();

        var missingSequenceIds = new List<uint>();
        foreach(var sequenceId in sequenceIds)
        {
            if (cachedPackets.TryGetValue(sequenceId, out var cachedPacket))
            {
                packetLog.DebugFormat("Retransmit {0}", sequenceId);

                cachedPacket.Header.Flags |= PacketHeaderFlags.Retransmission;
                SendPacketRaw(cachedPacket);

                continue;
            }

            LogRetransmitError(sequenceId);
            missingSequenceIds.Add(sequenceId);
        }


        if (missingSequenceIds.Count > 0)
        {
            EnqueueSend(new PacketRejectRetransmit(missingSequenceIds));
        }
    }

    private void LogRetransmitError(uint sequenceId)
    {
        if (cachedPackets.Count > 0)
        {
            // This is to catch a race condition between .Count and .Min() and .Max()
            try
            {
                log.Error($"Retransmit requested packet {sequenceId} not in cache. Cache range {cachedPackets.Keys.Min()} - {cachedPackets.Keys.Max()}.");
            }
            catch
            {
                log.Error($"Retransmit requested packet {sequenceId} not in cache. Cache is empty. Race condition threw exception.");
            }
        }
        else
        {
            log.Error($"Retransmit requested packet {sequenceId} not in cache. Cache is empty.");
        }
    }

    private void FlushPackets()
    {
        while (packetQueue.TryDequeue(out var packet))
        {
            packetLog.DebugFormat("Flushing packets, count {0}", packetQueue.Count);

            if (packet.Header.HasFlag(PacketHeaderFlags.EncryptedChecksum) && ConnectionData.PacketSequence.CurrentValue == 0)
                ConnectionData.PacketSequence = new UIntSequence(1);

            bool isNak = packet.Header.Flags.HasFlag(PacketHeaderFlags.RequestRetransmit);

            // If we are only ACKing, then we don't seem to have to increment the sequence
            if (packet.Header.Flags == PacketHeaderFlags.AckSequence || isNak)
                packet.Header.Sequence = ConnectionData.PacketSequence.CurrentValue;
            else
                packet.Header.Sequence = ConnectionData.PacketSequence.NextValue;
            packet.Header.Id = ClientId;
            packet.Header.Iteration = 0x14;
            packet.Header.Time = (ushort)Timers.PortalYearTicks;

            if (packet.Header.Sequence >= 2u && !isNak)
                cachedPackets.TryAdd(packet.Header.Sequence, packet);

            SendPacket(packet);
        }
    }

    public void SendPacket(OutboundPacket packet)
    {
        packetLog.DebugFormat("Sending packet {0}", packet.GetHashCode());
        NetworkStatistics.S2C_Packets_Aggregate_Increment();

        if (packet.Header.HasFlag(PacketHeaderFlags.EncryptedChecksum))
        {
            uint issacXor = ConnectionData.IssacServer.Next();
            packetLog.DebugFormat("Setting Issac for packet {0} to {1}", packet.GetHashCode(), issacXor);
            packet.IssacXor = issacXor;
        }

        SendPacketRaw(packet);
    }

    public void SendPacketRaw(OutboundPacket packet)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent((int)(PacketHeader.HeaderSize + (packet.Data?.Length ?? 0) + (packet.Fragments.Count * PacketFragment.MaxFragementSize)));

        try
        {
            var socket = connection.Socket;

            packet.CreateReadyToSendPacket(buffer, out var size);

            packetLog.Debug(packet.ToString());

            if (packetLog.IsDebugEnabled)
            {
                var listenerEndpoint = (System.Net.IPEndPoint)socket.LocalEndPoint;
                var sb = new StringBuilder();
                sb.AppendLine(String.Format("Sending Packet (Len: {0}) [{1}:{2}]", size, listenerEndpoint.Address, listenerEndpoint.Port));
                sb.AppendLine(buffer.BuildPacketString(0, size));
                packetLog.Debug(sb.ToString());
            }

            try
            {
                socket.SendTo(buffer, size, SocketFlags.None, connection.ListenerEndpoint);
            }
            catch (SocketException ex)
            {
                // Unhandled Exception: System.Net.Sockets.SocketException: A message sent on a datagram socket was larger than the internal message buffer or some other network limit, or the buffer used to receive a datagram into was smaller than the datagram itself
                // at System.Net.Sockets.Socket.UpdateStatusAfterSocketErrorAndThrowException(SocketError error, String callerName)
                // at System.Net.Sockets.Socket.SendTo(Byte[] buffer, Int32 offset, Int32 size, SocketFlags socketFlags, EndPoint remoteEP)

                var listenerEndpoint = (System.Net.IPEndPoint)socket.LocalEndPoint;
                var sb = new StringBuilder();
                sb.AppendLine(ex.ToString());
                sb.AppendLine(String.Format("Sending Packet (Len: {0}) [{1}:{2}]", buffer.Length, listenerEndpoint.Address, listenerEndpoint.Port));
                log.Error(sb.ToString());

                throw new Exception(SessionTerminationReasonHelper.GetDescription(SessionTerminationReason.SendToSocketException));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, true);
        }
    }

    /// <summary>
    /// This function handles turning a bundle of messages (representing all messages accrued in a timeslice),
    /// into 1 or more packets, combining multiple messages into one packet or spliting large message across
    /// several packets as needed.
    /// </summary>
    /// <param name="bundle"></param>
    private void SendBundle(NetworkBundle bundle, GameMessageGroup group)
    {
        packetLog.DebugFormat("Sending Bundle");

        bool writeOptionalHeaders = true;

        List<MessageFragment> fragments = new List<MessageFragment>();

        // Pull all messages out and create MessageFragment objects
        while (bundle.HasMoreMessages)
        {
            var message = bundle.Dequeue();

            var fragment = new MessageFragment(message, ConnectionData.FragmentSequence++);
            fragments.Add(fragment);
        }

        packetLog.DebugFormat("Bundle Fragment Count: {0}", fragments.Count);

        // Loop through while we have fragements
        while (fragments.Count > 0 || writeOptionalHeaders)
        {
            OutboundPacket packet = new OutboundPacket();
            PacketHeader packetHeader = packet.Header;

            if (fragments.Count > 0)
                packetHeader.Flags |= PacketHeaderFlags.BlobFragments;

            if (bundle.EncryptedChecksum)
                packetHeader.Flags |= PacketHeaderFlags.EncryptedChecksum;

            int availableSpace = OutboundPacket.MaxPacketSize;

            // Pull first message and see if it is a large one
            var firstMessage = fragments.FirstOrDefault();
            if (firstMessage != null)
            {
                // If a large message send only this one, filling the whole packet
                if (firstMessage.DataRemaining >= availableSpace)
                {
                    packetLog.DebugFormat("Sending large fragment");
                    OutboundPacketFragment spf = firstMessage.GetNextFragment();
                    packet.Fragments.Add(spf);
                    availableSpace -= spf.Length;
                    if (firstMessage.DataRemaining <= 0)
                        fragments.Remove(firstMessage);
                }
                // Otherwise we'll write any optional headers and process any small messages that will fit
                else
                {
                    if (writeOptionalHeaders)
                    {
                        writeOptionalHeaders = false;
                        WriteOptionalHeaders(bundle, packet);
                        if (packet.Data != null)
                            availableSpace -= (int)packet.Data.Length;
                    }

                    // Create a list to remove completed messages after iterator
                    List<MessageFragment> removeList = new List<MessageFragment>();

                    foreach (MessageFragment fragment in fragments)
                    {
                        bool fragmentSkipped = false;

                        // Is this a large fragment and does it have a tail that needs sending?
                        if (!fragment.TailSent && availableSpace >= fragment.TailSize)
                        {
                            packetLog.DebugFormat("Sending tail fragment");
                            OutboundPacketFragment spf = fragment.GetTailFragment();
                            packet.Fragments.Add(spf);
                            availableSpace -= spf.Length;
                        }
                        // Otherwise will this message fit in the remaining space?
                        else if (availableSpace >= fragment.NextSize)
                        {
                            packetLog.DebugFormat("Sending small message");
                            OutboundPacketFragment spf = fragment.GetNextFragment();
                            packet.Fragments.Add(spf);
                            availableSpace -= spf.Length;
                        }
                        else
                            fragmentSkipped = true;

                        // If message is out of data, set to remove it
                        if (fragment.DataRemaining <= 0)
                            removeList.Add(fragment);

                        // UIQueue messages must go out in order. Otherwise, you might see an NPC's tells in an order that doesn't match their defined emotes.
                        if (fragmentSkipped && group == GameMessageGroup.UIQueue)
                            break;
                    }

                    // Remove all completed messages
                    fragments.RemoveAll(x => removeList.Contains(x));
                }
            }
            // If no messages, write optional headers
            else
            {
                packetLog.DebugFormat("No messages, just sending optional headers");
                if (writeOptionalHeaders)
                {
                    writeOptionalHeaders = false;
                    WriteOptionalHeaders(bundle, packet);
                    if (packet.Data != null)
                        availableSpace -= (int)packet.Data.Length;
                }
            }
            EnqueueSend(packet);
        }
    }

    private void WriteOptionalHeaders(NetworkBundle bundle, OutboundPacket packet)
    {
        PacketHeader packetHeader = packet.Header;

        if (bundle.SendAck) // 0x4000
        {
            packetHeader.Flags |= PacketHeaderFlags.AckSequence;
            packetLog.DebugFormat("Outgoing AckSeq: {0}", lastReceivedPacketSequence);
            packet.InitializeDataWriter();
            packet.DataWriter.Write(lastReceivedPacketSequence);
        }

        if (bundle.TimeSync) // 0x1000000
        {
            packetHeader.Flags |= PacketHeaderFlags.TimeSync;
            packetLog.DebugFormat("Outgoing TimeSync TS: {0}", Timers.PortalYearTicks);
            packet.InitializeDataWriter();
            packet.DataWriter.Write(Timers.PortalYearTicks);
        }

        if (bundle.ClientTime != -1f) // 0x4000000
        {
            packetHeader.Flags |= PacketHeaderFlags.EchoResponse;
            packetLog.DebugFormat("Outgoing EchoResponse: {0}", bundle.ClientTime);
            packet.InitializeDataWriter();
            packet.DataWriter.Write(bundle.ClientTime);
            packet.DataWriter.Write((float)Timers.PortalYearTicks - bundle.ClientTime);
        }
    }
}
