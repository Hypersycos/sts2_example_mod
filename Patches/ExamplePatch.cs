using FirstMod.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;

namespace FirstMod.Patches;

[HarmonyPatch(typeof(Player), nameof(Player.CreateForNewRun), new Type[] { typeof(CharacterModel), typeof(UnlockState), typeof(ulong) })]
public class ExamplePatch
{
    static void Postfix(Player __result)
    {
        // give 999 gold at the start of the run
        __result.Gold = 999;

        // give the player the example relic at the start of the run
        var customRelic = ModelDb.Relic<ExampleRelic>().ToMutable();
        __result.AddRelicInternal(customRelic);
    }
}
