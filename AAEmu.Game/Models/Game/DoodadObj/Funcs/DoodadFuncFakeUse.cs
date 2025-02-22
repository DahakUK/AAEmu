﻿using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncFakeUse : DoodadFuncTemplate
{
    // doodad_funcs
    public uint SkillId { get; set; }
    public uint FakeSkillId { get; set; }
    public bool TargetParent { get; set; }

    public override void Use(BaseUnit caster, Doodad owner, uint skillId, int nextPhase = 0)
    {
        if (caster is Character)
        {
            Logger.Debug($"DoodadFuncFakeUse: skillId {skillId}, nextPhase {nextPhase},  SkillId {SkillId}, FakeSkillId {FakeSkillId}, TargetParent {TargetParent}");
        }
        else
        {
            Logger.Trace($"DoodadFuncFakeUse: skillId {skillId}, nextPhase {nextPhase},  SkillId {SkillId}, FakeSkillId {FakeSkillId}, TargetParent {TargetParent}");
        }

        if (caster == null)
        {
            return;
        }

        if (SkillId != 0)
        {
            var skillCaster = SkillCaster.GetByType(SkillCasterType.Doodad);
            skillCaster.ObjId = owner.ObjId;

            var target = SkillCastTarget.GetByType(SkillCastTargetType.Unit);
            target.ObjId = caster.ObjId;
            if (TargetParent)
            {
                //target owner/doodad
                target = SkillCastTarget.GetByType(SkillCastTargetType.Doodad);
                target.ObjId = owner.ParentObjId;
            }

            var skill = new Skill(SkillManager.Instance.GetSkillTemplate(SkillId));
            skill.Use(caster, skillCaster, target, null, false, out _);
            owner.ToNextPhase = true;
        }
        else if (FakeSkillId != 0)
        {
            if (FakeSkillId == skillId && nextPhase > 0)
            {
                owner.ToNextPhase = true;
                // Removed the duplicate skill call
            }

            if (skillId > 0 && nextPhase > 0) // TODO quest ID=3357, Harvest the Cotton didn't work
            {
                owner.ToNextPhase = true;
            }
        }

        if (FakeSkillId == 0 && SkillId == 0)
        {
            owner.ToNextPhase = true; // TODO otherwise quest ID=1970, Stolen Glory didn't work
        }
    }
}
