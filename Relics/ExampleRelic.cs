using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace FirstMod.Relics;

public sealed class ExampleRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override bool IsAllowed(IRunState runState) => true;
    public override bool ShouldReceiveCombatHooks => true;

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        // When player deals damage, gain 2 Block
        if (dealer?.IsPlayer == true && result.TotalDamage > 0)
        {
            dealer.GainBlockInternal(2);
            Flash();
        }

        return Task.CompletedTask;
    }
}