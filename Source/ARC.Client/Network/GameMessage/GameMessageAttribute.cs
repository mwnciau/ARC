using System;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages;

namespace ARC.Client.Network.GameMessage;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class GameMessageAttribute : Attribute
{
    public GameMessageOpcode Opcode { get; }

    public GameMessageAttribute(GameMessageOpcode opcode)
    {
        Opcode = opcode;
    }
}
