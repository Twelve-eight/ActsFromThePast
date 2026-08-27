using ActsFromThePast.Interfaces;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;

namespace ActsFromThePast.SharedEvents;

public sealed class Lab : CustomEventModel, IShrineEvent
{
    private const int PotionCount = 2;

    public override ActModel[] Acts => Array.Empty<ActModel>();
    
    bool IShrineEvent.IsOneTimeEvent => true;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (ActsFromThePastConfig.RebalancedModeEffective)
        {
            return new[]
            {
                Option(Search),
                Option(Ransack, "INITIAL_REBALANCED")
            };
        }
        return new[] { Option(Search) };
    }
    
    public override void OnRoomEnter()
    {
        AFTPModAudio.Play("events", "lab");
    }
    
    private async Task Search()
    {
        var rewards = new List<Reward>(PotionCount);
        for (int i = 0; i < PotionCount; i++)
        {
            rewards.Add(new PotionReward(Owner));
        }
        await RewardsCmd.OfferCustom(Owner, rewards);
        SetEventFinished(PageDescription("SEARCH"));
    }
    
    private async Task Ransack()
    {
        await PlayerCmd.GainMaxPotionCount(1, Owner);
        SetEventFinished(PageDescription("RANSACK"));
    }
}