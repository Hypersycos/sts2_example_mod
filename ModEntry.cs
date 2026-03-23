using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;
using MoreSaves.Patches;

[ModInitializer("Initialize")]
public class ModEntry
{ 
    public static void Initialize()
    {
        var harmony = new Harmony("MoreSaves.patch");
        harmony.PatchAll();

        ISaveStore saveStore = (typeof(SaveManager).GetField("_saveStore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                                                   .GetValue(SaveManager.Instance) as ISaveStore)!;

        SaveManagerPatch.SyncMoreSaves(saveStore, SaveManager.Instance);
    }
}