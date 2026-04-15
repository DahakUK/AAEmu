using System.Diagnostics;
using AAEmu.Game.Core.Packets;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Skills.Plots.Type;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;

using NLog;

namespace AAEmu.Game.Models.Game.Skills.Plots.Tree;

public class PlotNode
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    // Tree
    public PlotTree Tree;
    public PlotNode Parent;
    public List<PlotNode> Children = [];
    // Plots
    public PlotEventTemplate Event;
    public PlotNextEvent ParentNextEvent;

    private bool IsChannelStart()
    {
        foreach (var child in Children)
        {
            if (child.ParentNextEvent.Channeling == true)
                return true;
        }
        return false;
    }

    public int ComputeDelayMs(PlotState state, PlotTargetInfo targetInfo)
    {
        return ParentNextEvent.GetDelay(state, targetInfo, Parent);
    }

    public bool CheckConditions(PlotState state, PlotTargetInfo targetInfo)
    {
        return Event.Conditions.All(condition => condition.CheckCondition(state, targetInfo));
    }

    public void Execute(PlotState state, PlotTargetInfo targetInfo, CompressedGamePackets packets = null)
    {
        //Logger.Debug("Executing plot node with id {0}", Event.Id);

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        byte flag = 2;
        foreach (var eff in Event.Effects)
        {
            try
            {
                eff.ApplyEffect(state, targetInfo, Event, ref flag, IsChannelStart());
            }
            catch (Exception e)
            {
                state?.Caster?.SendPacket(new SCChatMessagePacket(Chat.ChatType.Notice, "Plot Effects Error - Check Logs"));
                Logger.Error("[Plot Effects Error]: {0}\n{1}", e.Message, e.StackTrace);
            }
        }

        double castTime = Event.NextEvents
             .Where(nextEvent => nextEvent.Casting || nextEvent.Channeling)
             .Max(nextEvent => nextEvent.Delay / 10 as int?) ?? 0;
        castTime = state.Caster.ApplySkillModifiers(state.ActiveSkill, SkillAttribute.CastTime, castTime) * state.Caster.CastTimeMul;
        castTime = Math.Max(castTime, 0);

        if (castTime > 0)
            state.IsCasting = true;
        if (ParentNextEvent?.Casting ?? false)
            state.IsCasting = false;

        if (ParentNextEvent?.Channeling ?? false)
            state.IsChanneling = false;
        else
            state.IsChanneling = true;

        if (Event.HasSpecialEffects() || castTime > 0 || Event.Conditions.Count > 0)
        {
            var skill = state.ActiveSkill;
            var unkId = (ParentNextEvent?.Casting ?? false) || (ParentNextEvent?.Channeling ?? false) ? state.Caster.ObjId : 0;

            // For SkillController effects the per-effect SourceId governs which unit
            // performs the movement. The event-level targetInfo.Source (always the original
            // skill caster) would incorrectly assign the controller to the caster instead of
            // the intended target (e.g. making the player jump instead of the fish).
            BaseUnit effectiveCasterUnit = targetInfo.Source;
            var scEffect = Event.Effects.FirstOrDefault(e => e.ActualType == "SkillController");
            if (scEffect != null)
            {
                effectiveCasterUnit = scEffect.SourceId switch
                {
                    PlotEffectSource.OriginalSource => state.Caster,
                    PlotEffectSource.OriginalTarget => state.Target,
                    PlotEffectSource.Source         => targetInfo.Source,
                    PlotEffectSource.Target         => targetInfo.Target,
                    _                               => targetInfo.Source
                };
            }

            PlotObject casterPlotObj;
            if (effectiveCasterUnit.ObjId == uint.MaxValue)
                casterPlotObj = new PlotObject(effectiveCasterUnit.Transform);
            else
                casterPlotObj = new PlotObject(effectiveCasterUnit);

            PlotObject targetPlotObj;
            if (targetInfo.Target.ObjId == uint.MaxValue)
                targetPlotObj = new PlotObject(targetInfo.Target.Transform);
            else
                targetPlotObj = new PlotObject(targetInfo.Target);

            var targetCount = (byte)targetInfo.EffectedTargets.Count;

            var packet = new SCPlotEventPacket(skill.TlId, Event.Id, skill.Template.Id, casterPlotObj,
                targetPlotObj, unkId, (ushort)castTime, flag, 0, targetCount);

            if (packets != null)
                packets.AddPacket(packet);
            else
                state.Caster.BroadcastPacket(packet, true);

            Logger.Trace($"Execute Took {stopwatch.ElapsedMilliseconds} to finish.");
        }
    }
}
