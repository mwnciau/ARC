using ACE.Server.Network;
using ACE.Server.Network.Structure;
using System.Diagnostics;
using OutboundPacket = ACE.Server.Network.ServerPacket;

namespace ARC.Client.Network.Packets;

public class OutboundRequestRetransmit : OutboundPacket
{
    // Max packet size (minus header) = 464, minus 4 for the count and divide by 4.
    public const uint MaxSequenceIdCount = 115;

    public OutboundRequestRetransmit(List<uint> sequenceIds)
    {
        Debug.Assert(sequenceIds.Count <= MaxSequenceIdCount);

        Header.Flags = PacketHeaderFlags.RequestRetransmit;

        InitializeDataWriter();

        DataWriter.Write(sequenceIds.Count);
        foreach(uint sequenceId in sequenceIds)
        {
            DataWriter.Write(sequenceId);
        }
    }
}
