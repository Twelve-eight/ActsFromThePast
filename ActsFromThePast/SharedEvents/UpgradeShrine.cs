

using ActsFromThePast.Interfaces;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace ActsFromThePast.SharedEvents;

public sealed class UpgradeShrine : CustomEventModel, IShrineEvent
{
    public override ActModel[] Acts => Array.Empty<ActModel>();
    
    private const int KneelDamage = 10;

    public override bool IsAllowed(IRunState runState)
    {
        if (!ActsFromThePastConfig.RebalancedModeEffective)
            return true;

        return runState.Players.All(p =>
            PileType.Deck.GetPile(p).Cards.Any(c => c.IsUpgradable));
    }
    
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new IntVar("KneelDamage", KneelDamage)
    };
    
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>();

        if (Owner.Deck.Cards.Any(c => c.IsUpgradable))
        {
            options.Add(Option(Pray));
        }
        else
        {
            options.Add(new EventOption(this, null,
                $"{Id.Entry}.pages.INITIAL.options.PRAY_LOCKED",
                Array.Empty<IHoverTip>()));
        }

        if (ActsFromThePastConfig.RebalancedModeEffective)
        {
            options.Add(Option(Kneel, "INITIAL_REBALANCED").ThatDoesDamage(KneelDamage));
        }
        else
        {
            options.Add(Option(Leave));
        }

        return options;
    }

    private async Task Pray()
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 1);
        var card = (await CardSelectCmd.FromDeckForUpgrade(Owner, prefs)).FirstOrDefault();

        if (card != null)
            CardCmd.Upgrade(card);

        SetEventFinished(PageDescription("PRAY"));
    }

    private Task Leave()
    {
        SetEventFinished(PageDescription("LEAVE"));
        return Task.CompletedTask;
    }
    
    private async Task Kneel()
    {
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            KneelDamage,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null, null);
        foreach (var card in Owner.Deck.Cards
                     .Where(c => c.IsUpgradable)
                     .ToList()
                     .StableShuffle(Owner.RunState.Rng.Niche)
                     .Take(2))
        {
            CardCmd.Upgrade(card);
        }
        SetEventFinished(PageDescription("PRAY"));
    }
}