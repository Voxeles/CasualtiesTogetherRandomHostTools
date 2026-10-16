using System;
using LiteEntitySystem.Transport;

namespace CasualtiesTogetherRandomHostTools;

// Dummy peer that doesn't send anything
internal sealed class ClonePeer : AbstractNetPeer
{
    public override void TriggerSend()
    {
    }

    public override void SendReliableOrdered(ReadOnlySpan<byte> data)
    {
    }

    public override void SendUnreliable(ReadOnlySpan<byte> data)
    {
    }

    public override int GetMaxUnreliablePacketSize() => 1000;
}
