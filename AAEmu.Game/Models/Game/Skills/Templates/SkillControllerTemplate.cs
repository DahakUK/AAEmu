using AAEmu.Game.Core.Packets;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Skills.SkillControllers;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Templates;

public class SkillControllerTemplate : EffectTemplate
{
    public uint KindId { get; set; }
    public int[] Value { get; set; } = new int[15];

    public byte ActiveWeaponId { get; set; }
    // TODO 1.2 // public uint EndSkillId { get; set; }
    public override bool OnActionTime { get; }

    public override void Apply(BaseUnit caster, SkillCaster casterObj, BaseUnit target, SkillCastTarget targetObj,
        CastAction castObj,
        EffectSource source, SkillObject skillObject, DateTime time, CompressedGamePackets packetBuilder = null)
    {
        Logger.Debug("SkillControllerTemplate Apply: KindId={0}, caster={1}, target={2}", KindId, caster?.ObjId, target?.ObjId);

        if (caster is not Unit ownerUnit)
            return;

        var sc = SkillController.CreateSkillController(this, caster, target);
        if (sc == null)
            return;

        if (ownerUnit.ActiveSkillController != null)
            ownerUnit.ActiveSkillController.End();

        ownerUnit.ActiveSkillController = sc;
        sc.Execute();
    }
}
