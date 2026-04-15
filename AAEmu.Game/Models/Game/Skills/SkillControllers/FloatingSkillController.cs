using System.Numerics;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.StaticValues;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.Skills.SkillControllers;

/// <summary>
/// Telekinesis / levitation controller. Holds the Owner near the Target, applying a height offset.
/// Used by skills such as Telekinesis that should lift a target unit off the ground.
/// </summary>
public class FloatingSkillController : SkillController
{
    public int Duration { get; set; }
    public float HeightOffset { get; set; }

    private readonly DateTime _endTime;

    public FloatingSkillController(SkillControllerTemplate template, BaseUnit owner, BaseUnit target)
    {
        Template = template;
        Owner = owner as Unit;
        Target = target as Unit;

        // Value[2] = duration in ms (same layout as LeapSkillController)
        // Value[3] = height offset in mm
        Duration = template.Value[2];
        HeightOffset = template.Value[3] / 1000f;

        _endTime = DateTime.UtcNow.AddMilliseconds(Duration > 0 ? Duration : 3000);
    }

    public void Tick(TimeSpan delta)
    {
        if (DateTime.UtcNow >= _endTime || Owner.IsDead)
        {
            End();
            return;
        }

        if (Owner.Buffs.HasEffectsMatchingCondition(e => e.Template.Stun || e.Template.Sleep))
        {
            End();
            return;
        }

        HoldPosition();
    }

    public override void Execute()
    {
        base.Execute();
        TickManager.Instance.OnTick.Subscribe(Tick, TimeSpan.FromMilliseconds(100));
    }

    public override void End()
    {
        base.End();
        TickManager.Instance.OnTick.UnSubscribe(Tick);
    }

    private void HoldPosition()
    {
        // Track horizontally to the target and hold at HeightOffset above it
        var targetPos = Target?.Transform.World.Position ?? Owner.Transform.World.Position;

        var newX = targetPos.X;
        var newY = targetPos.Y;
        var newZ = targetPos.Z + HeightOffset;

        var oldPosition = Owner.Transform.Local.ClonePosition();
        Owner.Transform.Local.SetPosition(newX, newY, newZ);

        var moveType = (UnitMoveType)MoveType.GetType(MoveTypeEnum.Unit);
        var angle = MathUtil.CalculateAngleFrom(Owner.Transform.Local.Position, targetPos);
        var (velX, velY) = MathUtil.AddDistanceToFront(0, 0, 0, (float)angle.DegToRad());
        var (rx, ry, rz) = Owner.Transform.Local.ToRollPitchYawSBytesMovement();

        moveType.X = Owner.Transform.Local.Position.X;
        moveType.Y = Owner.Transform.Local.Position.Y;
        moveType.Z = Owner.Transform.Local.Position.Z;
        moveType.VelX = (short)velX;
        moveType.VelY = (short)velY;
        moveType.RotationX = rx;
        moveType.RotationY = ry;
        moveType.RotationZ = rz;
        moveType.ActorFlags = 5;
        moveType.Flags = MoveTypeFlags.Moving;
        moveType.DeltaMovement = new sbyte[3];
        moveType.DeltaMovement[0] = 0;
        moveType.DeltaMovement[1] = 0;
        moveType.DeltaMovement[2] = 127;
        moveType.Stance = 0;
        moveType.Alertness = MoveTypeAlertness.Combat;
        moveType.Time = (uint)(DateTime.UtcNow - DateTime.UtcNow.Date).TotalMilliseconds;

        Owner.CheckMovedPosition(oldPosition);
        Owner.BroadcastPacket(new SCOneUnitMovementPacket(Owner.ObjId, moveType), false);
    }
}
