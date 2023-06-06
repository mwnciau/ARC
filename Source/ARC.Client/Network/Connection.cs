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

public class NetworkSession
{
    private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");

    private const int minimumTimeBetweenBundles = 5; // 5ms
    private const int timeBetweenTimeSync = 20000; // 20s
    private const int timeBetweenAck = 2000; // 2s

    private readonly EndPoint endpoint;
    private readonly ConnectionListener connectionListener;

    private readonly Object[] currentBundleLocks = new Object[(int)GameMessageGroup.QueueMax];
    private readonly NetworkBundle[] currentBundles = new NetworkBundle[(int)GameMessageGroup.QueueMax];

    private ConcurrentDictionary<uint, InboundPacket> outOfOrderPackets = new ConcurrentDictionary<uint, InboundPacket>();
    private ConcurrentDictionary<uint, MessageBuffer> partialFragments = new ConcurrentDictionary<uint, MessageBuffer>();
    private ConcurrentDictionary<uint, ClientMessage> outOfOrderFragments = new ConcurrentDictionary<uint, ClientMessage>();

    private DateTime nextSend = DateTime.UtcNow;

    // Resync will be started after ConnectResponse, and should immediately be sent then, so no delay here.
    // Fun fact: even though we send the server time in the ConnectRequest, client doesn't seem to use it?  Therefore we must TimeSync early so client doesn't see a skew when we send it later.
    public bool sendResync;
    private DateTime nextResync = DateTime.UtcNow;

    // Ack should be sent after a 2 second delay, so start enabled with the delay.
    // Sending this too early seems to cause issues with clients disconnecting.
    private bool sendAck = true;
    private DateTime nextAck = DateTime.UtcNow.AddMilliseconds(timeBetweenAck);

    private uint lastReceivedPacketSequence = 1;
    private uint lastReceivedFragmentSequence;

    /// <summary>
    /// This is referenced from many threads:<para />
    /// ConnectionListener.OnDataReceieve()->Session.HandlePacket()->This.HandlePacket(packet), This path can come from any client or other thinkable object.<para />
    /// WorldManager.UpdateWorld()->Session.Update(lastTick)->This.Update(lastTick)
    /// </summary>
    private readonly ConcurrentDictionary<uint /*seq*/, OutboundPacket> cachedPackets = new ConcurrentDictionary<uint /*seq*/, OutboundPacket>();

    private static readonly TimeSpan cachedPacketPruneInterval = TimeSpan.FromSeconds(5);
    private DateTime lastCachedPacketPruneTime;
    /// <summary>
    /// Number of seconds to retain cachedPackets
    /// </summary>
    private const int cachedPacketRetentionTime = 120;

    /// <summary>
    /// This is referenced by multiple thread:<para />
    /// [ConnectionListener Thread + 0] WorldManager.ProcessPacket()->SendLoginRequestReject()<para />
    /// [ConnectionListener Thread + 0] WorldManager.ProcessPacket()->Session.ProcessPacket()->NetworkSession.ProcessPacket()->DoRequestForRetransmission()<para />
    /// [ConnectionListener Thread + 1] WorldManager.ProcessPacket()->Session.ProcessPacket()->NetworkSession.ProcessPacket()-> ... AuthenticationHandler<para />
    /// [World Manager Thread] WorldManager.UpdateWorld()->Session.Update(lastTick)->This.Update(lastTick)<para />
    /// </summary>
    private readonly ConcurrentQueue<OutboundPacket> packetQueue = new ConcurrentQueue<OutboundPacket>();

    public readonly SessionConnectionData ConnectionData = new SessionConnectionData();

    /// <summary>
    /// Stores the tick value for the when an active session will timeout. If this value is in the past, the session is dead/inactive.
    /// </summary>
    public long TimeoutTick { get; set; }

    public ushort ClientId { get; }
    public ushort ServerId { get; }

    public NetworkSession(EndPoint endpoint, ConnectionListener connectionListener, ushort clientId, ushort serverId)
    {
        this.endpoint = endpoint;
        this.connectionListener = connectionListener;

        ClientId = clientId;
        ServerId = serverId;

        // New network auth session timeouts will always be low.
        TimeoutTick = DateTime.UtcNow.AddSeconds(AuthenticationHandler.DefaultAuthTimeout).Ticks;

        for (int i = 0; i < currentBundles.Length; i++)
        {
            currentBundleLocks[i] = new object();
            currentBundles[i] = new NetworkBundle();
        }
    }

    /// <summary>
    /// Enequeues a GameMessage for sending to this client.
    /// This may be called from many threads.
    /// </summary>
    /// <param name="messages">One or more GameMessages to send</param>
    public void EnqueueSend(params GameMessage[] messages)
    {
        if (isReleased) // Session has been removed
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

    /// <summary>
    /// Enqueues a ServerPacket for sending to this client.
    /// Currently this is only used publicly once during login.  If that changes it's thread safety should be re
    /// </summary>
    /// <param name="packets"></param>
    public void EnqueueSend(params ServerPacket[] packets)
    {
        if (isReleased) // Session has been removed
            return;

        foreach (var packet in packets)
        {
            packetLog.DebugFormat("Enqueuing Packet {0}", packet.GetHashCode());
            packetQueue.Enqueue(packet);
        }
    }

    /// <summary>
    /// Prunes the cachedPackets dictionary
    /// Checks if we should send the current bundle and then flushes all pending packets.
    /// </summary>
    public void Update()
    {
        if (isReleased) // Session has been removed
            return;

        if (DateTime.UtcNow - lastCachedPacketPruneTime > cachedPacketPruneInterval)
            PruneCachedPackets();

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
                    if (sendResync && !currentBundle.TimeSync && DateTime.UtcNow > nextResync)
                    {
                        packetLog.DebugFormat("Setting to send TimeSync packet");
                        currentBundle.TimeSync = true;
                        currentBundle.EncryptedChecksum = true;
                        nextResync = DateTime.UtcNow.AddMilliseconds(timeBetweenTimeSync);
                    }

                    if (sendAck && !currentBundle.SendAck && DateTime.UtcNow > nextAck)
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

    private void PruneCachedPackets()
    {
        lastCachedPacketPruneTime = DateTime.UtcNow;

        var currentTime = (ushort)Timers.PortalYearTicks;

        // Make sure our comparison still works when ushort wraps every 18.2 hours.
        var removalList = cachedPackets.Values.Where(x => (currentTime >= x.Header.Time ? currentTime : currentTime + ushort.MaxValue) - x.Header.Time > cachedPacketRetentionTime);

        foreach (var packet in removalList)
            cachedPackets.TryRemove(packet.Header.Sequence, out _);
    }

    // This is called from ConnectionListener.OnDataReceieve()->Session.ProcessPacket()->This
    /// <summary>
    /// Processes and incoming packet from a client.
    /// </summary>
    /// <param name="packet">The InboundPacket to process.</param>
    public void ProcessPacket(InboundPacket packet)
    {
        if (isReleased) // Session has been removed
            return;

        packetLog.DebugFormat("Processing packet {0}", packet.Header.Sequence);
        NetworkStatistics.C2S_Packets_Aggregate_Increment();

        if (!packet.VerifyCRC(ConnectionData.CryptoClient))
        {
            return;
        }

        // If the client sent a NAK with a cleartext CRC then process it
        if ((packet.Header.Flags & PacketHeaderFlags.RequestRetransmit) == PacketHeaderFlags.RequestRetransmit
            && !((packet.Header.Flags & PacketHeaderFlags.EncryptedChecksum) == PacketHeaderFlags.EncryptedChecksum))
        {
            List<uint> uncached = null;

            foreach (uint sequence in packet.HeaderOptional.RetransmitData)
            {
                if (!Retransmit(sequence))
                {
                    if (uncached == null)
                        uncached = new List<uint>();

                    uncached.Add(sequence);
                }
            }

            if (uncached != null)
            {
                // Sends a response packet w/ PacketHeader.RejectRetransmit
                var packetRejectRetransmit = new PacketRejectRetransmit(uncached);
                EnqueueSend(packetRejectRetransmit);
            }

            NetworkStatistics.C2S_RequestsForRetransmit_Aggregate_Increment();
            return; //cleartext crc NAK is never accompanied by additional data needed by the rest of the pipeline
        }

        #region Reordering stage

        // Reordering stage
        // Check if this packet's sequence is a sequence which we have already processed.
        // There are some exceptions:
        // Sequence 0 as we have several Seq 0 packets during connect.  This also cathes a case where it seems CICMDCommand arrives at any point with 0 sequence value too.
        // If the only header on the packet is AckSequence. It seems AckSequence can come in with the same sequence value sometimes.
        if (packet.Header.Sequence <= lastReceivedPacketSequence && packet.Header.Sequence != 0 &&
            !(packet.Header.Flags == PacketHeaderFlags.AckSequence && packet.Header.Sequence == lastReceivedPacketSequence))
        {
            packetLog.WarnFormat("Packet {0} received again", packet.Header.Sequence);
            return;
        }

        // Check if this packet's sequence is greater then the next one we should be getting.
        // If true we must store it to replay once we have caught up.
        var desiredSeq = lastReceivedPacketSequence + 1;
        if (packet.Header.Sequence > desiredSeq)
        {
            packetLog.DebugFormat("Packet {0} received out of order", packet.Header.Sequence);

            if (!outOfOrderPackets.ContainsKey(packet.Header.Sequence))
                outOfOrderPackets.TryAdd(packet.Header.Sequence, packet);

            if (desiredSeq + 2 <= packet.Header.Sequence && DateTime.UtcNow - LastRequestForRetransmitTime > new TimeSpan(0, 0, 1))
                DoRequestForRetransmission(packet.Header.Sequence);

            return;
        }

        #endregion

        #region Final processing stage

        // Processing stage
        // If we reach here, this is a packet we should proceed with processing.
        HandleOrderedPacket(packet);

        // Process data now in sequence
        // Finally check if we have any out of order packets or fragments we need to process;
        CheckOutOfOrderPackets();
        CheckOutOfOrderFragments();

        #endregion
    }

    const uint MaxNumNakSeqIds = 115; //464 + header = 484;  (464 - 4) / 4

    /// <summary>
    /// request retransmission of lost sequences
    /// </summary>
    /// <param name="rcvdSeq">the sequence of the packet that was just received.</param>
    private void DoRequestForRetransmission(uint rcvdSeq)
    {
        var desiredSeq = lastReceivedPacketSequence + 1;
        List<uint> needSeq = new List<uint>();
        needSeq.Add(desiredSeq);
        uint bottom = desiredSeq + 1;
        if (rcvdSeq < bottom || rcvdSeq - bottom > CryptoSystem.MaximumEffortLevel)
        {
            throw new Exception(SessionTerminationReasonHelper.GetDescription(SessionTerminationReason.AbnormalSequenceReceived));
        }
        uint seqIdCount = 1;
        for (uint a = bottom; a < rcvdSeq; a++)
        {
            if (!outOfOrderPackets.ContainsKey(a))
            {
                needSeq.Add(a);
                seqIdCount++;
                if (seqIdCount >= MaxNumNakSeqIds)
                {
                    break;
                }
            }
        }

        OutboundPacket reqPacket = new OutboundPacket();
        byte[] reqData = new byte[4 + (needSeq.Count * 4)];
        MemoryStream msReqData = new MemoryStream(reqData, 0, reqData.Length, true, true);
        msReqData.Write(BitConverter.GetBytes((uint)needSeq.Count), 0, 4);
        needSeq.ForEach(k => msReqData.Write(BitConverter.GetBytes(k), 0, 4));
        reqPacket.Data = msReqData;
        reqPacket.Header.Flags = PacketHeaderFlags.RequestRetransmit;

        EnqueueSend(reqPacket);

        LastRequestForRetransmitTime = DateTime.UtcNow;
        packetLog.DebugFormat("Requested retransmit of {0}", needSeq.Select(k => k.ToString()).Aggregate((a, b) => a + ", " + b));
        NetworkStatistics.S2C_RequestsForRetransmit_Aggregate_Increment();
    }

    private DateTime LastRequestForRetransmitTime = DateTime.MinValue;

    /// <summary>
    /// Handles a packet<para />
    /// Packets at this stage are already verified, "half processed", and reordered
    /// </summary>
    /// <param name="packet">InboundPacket to handle</param>
    private void HandleOrderedPacket(InboundPacket packet)
    {
        packetLog.DebugFormat("Handling packet {0}", packet.Header.Sequence);

        // If we have an AcknowledgeSequence flag, we can clear our cached packet buffer up to that sequence.
        if (packet.Header.HasFlag(PacketHeaderFlags.AckSequence))
            AcknowledgeSequence(packet.HeaderOptional.AckSequence);

        if (packet.Header.HasFlag(PacketHeaderFlags.TimeSync))
        {
            packetLog.DebugFormat("Incoming TimeSync TS: {0}", packet.HeaderOptional.TimeSynch);
            // Do something with this...
            // Based on network traces these are not 1:1.  Server seems to send them every 20 seconds per port.
            // Client seems to send them alternatingly every 2 or 4 seconds per port.
            // We will send this at a 20 second time interval.  I don't know what to do with these when we receive them at this point.
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

        // Update the last received sequence.
        if (packet.Header.Sequence != 0 && packet.Header.Flags != PacketHeaderFlags.AckSequence)
            lastReceivedPacketSequence = packet.Header.Sequence;
    }

    private void HandleConnectRequest(InboundPacket packet)
    {
        var request = new InboundConnectRequest(packet);

        EnqueueSend(new OutboundConnectResponse(request.cookie));
    }

    /// <summary>
    /// Processes a fragment, combining split fragments as needed, then handling them
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
                // No existing buffer, so add a new one for this fragment sequence.
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

        // If message is not null, we have a complete message to handle
        if (message != null)
        {
            // First check if this message is the next sequence, if it is not, add it to our outOfOrderFragments
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
    /// Handles a ClientMessage by calling using InboundMessageManager
    /// </summary>
    /// <param name="message">ClientMessage to process</param>
    private void HandleFragment(ClientMessage message)
    {
        // Todo
        // InboundMessageManager.HandleClientMessage(message, session);
        lastReceivedFragmentSequence++;
    }

    /// <summary>
    /// Checks if we now have packets queued out of order which should be processed as the next sequence.
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
    /// Checks if we now have fragments queued out of order which should be handled as the next sequence.
    /// </summary>
    private void CheckOutOfOrderFragments()
    {
        while (outOfOrderFragments.TryRemove(lastReceivedFragmentSequence + 1, out var message))
        {
            packetLog.DebugFormat("Ready to handle out of order fragment {0}", lastReceivedFragmentSequence + 1);
            HandleFragment(message);
        }
    }

    //private List<EchoStamp> EchoStamps = new List<EchoStamp>();

    private static int EchoLogInterval = 5;
    private static int EchoInterval = 10;
    private static float EchoThreshold = 2.0f;
    private static float DiffThreshold = 0.01f;

    private float lastClientTime;
    private DateTime lastServerTime;

    private double lastDiff;
    private int echoDiff;

    private void AcknowledgeSequence(uint sequence)
    {
        // TODO Sending Acks seems to cause some issues.  Needs further research.
        // if (!sendAck)
        //    sendAck = true;

        var removalList = cachedPackets.Keys.Where(x => x < sequence);

        foreach (var key in removalList)
            cachedPackets.TryRemove(key, out _);
    }

    private bool Retransmit(uint sequence)
    {
        if (cachedPackets.TryGetValue(sequence, out var cachedPacket))
        {
            packetLog.DebugFormat("Retransmit {0}", sequence);

            if (!cachedPacket.Header.HasFlag(PacketHeaderFlags.Retransmission))
                cachedPacket.Header.Flags |= PacketHeaderFlags.Retransmission;

            SendPacketRaw(cachedPacket);

            return true;
        }

        if (cachedPackets.Count > 0)
        {
            // This is to catch a race condition between .Count and .Min() and .Max()
            try
            {
                log.Error($"Retransmit requested packet {sequence} not in cache. Cache range {cachedPackets.Keys.Min()} - {cachedPackets.Keys.Max()}.");
            }
            catch
            {
                log.Error($"Retransmit requested packet {sequence} not in cache. Cache is empty. Race condition threw exception.");
            }
        }
        else
            log.Error($"Retransmit requested packet {sequence} not in cache. Cache is empty.");

        return false;
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
            packet.Header.Id = ServerId;
            packet.Header.Iteration = 0x14;
            packet.Header.Time = (ushort)Timers.PortalYearTicks;

            if (packet.Header.Sequence >= 2u && !isNak)
                cachedPackets.TryAdd(packet.Header.Sequence, packet);

            SendPacket(packet);
        }
    }

    private void SendPacket(OutboundPacket packet)
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

    private void SendPacketRaw(OutboundPacket packet)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent((int)(PacketHeader.HeaderSize + (packet.Data?.Length ?? 0) + (packet.Fragments.Count * PacketFragment.MaxFragementSize)));

        try
        {
            var socket = connectionListener.Socket;

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
                socket.SendTo(buffer, size, SocketFlags.None, endpoint);
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


    private bool isReleased;

    /// <summary>
    /// This will empty out arrays, collections and dictionaries, and mark the object as released.
    /// Any further work assigned to this object will be ignored.
    /// </summary>
    public void ReleaseResources()
    {
        isReleased = true;

        for (int i = 0; i < currentBundles.Length; i++)
            currentBundles[i] = null;

        outOfOrderPackets.Clear();
        partialFragments.Clear();
        outOfOrderFragments.Clear();

        cachedPackets.Clear();

        packetQueue.Clear();

        ConnectionData.CryptoClient.ReleaseResources();
    }
}
