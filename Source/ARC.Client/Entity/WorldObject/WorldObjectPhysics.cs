using ACE.Entity;
using ACE.Entity.Enum;
using System.Numerics;

namespace ARC.Client.Entity.WorldObject;

public struct WorldObjectChildren {
    public uint Guid;
    public int LocationId;
}

public class WorldObjectPhysics
{
    public PhysicsState PhysicsState;

    /// Todo: this needs proper deserialization
    /// <see cref="ACE.Server.WorldObjects.WorldObject.SerializePhysicsData"/>
    public int? MovementDataLength;
    public byte[]? MovementData;
    public bool? IsAutonomous;
    public Placement? Placement;
    public Position? Position;
    public uint? MotionTableId;
    public uint? SoundTableId;
    public uint? PhysicsTableId;
    public uint? SetupTableId;
    public uint? WielderId;
    public ParentLocation? ParentLocation;
    public int? ChildrenCount;
    public WorldObjectChildren[]? Children;
    public float? Scale;
    public float? Friction;
    public float? Elasticity;
    public float? Translucency;
    public Vector3? Velocity;
    public Vector3? Acceleration;
    public Vector3? Omega;
    public uint? DefaultScriptId;
    public float? DefaultScriptIntensity;

    public ushort ObjectPosition;
    public ushort ObjectMovement;
    public ushort ObjectState;
    public ushort ObjectVector;
    public ushort ObjectTeleport;
    public ushort ObjectServerControl;
    public ushort ObjectForcePosition;
    public ushort ObjectVisualDesc;
    public ushort ObjectInstance;
}
