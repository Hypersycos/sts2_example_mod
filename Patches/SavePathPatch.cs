using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Runs;
using MoreSaves.MainMenu;
using System.Reflection;
using System.Text.Json;
using static Godot.HttpRequest;

namespace MoreSaves.Patches;

#pragma warning disable IDE0005

public class Store
{
    public static string currentSPSave = "";
    public static string currentMPSave = "";
    public static int saveCount = 0;
    public static int multiSaveCount = 0;
    public static NMainMenu? mainMenu = null;
    public static NMultiplayerSubmenu? submenu = null;

    public static ISaveStore? lastSaveStore = null;
    public static IEnumerable<string> spSaves = [];
    public static IEnumerable<string> mpSaves = [];

    public static string GetSaveDir(int profile) => RunSaveManager.GetRunSavePath(profile, "MoreSaves");
    public static string SaveDir => GetSaveDir(SaveManager.Instance.CurrentProfileId);
    public static ReadSaveResult<SerializableRun> GetSPRun(string name)
    {
        currentSPSave = name;
        return SaveManager.Instance.LoadRunSave();
    }

    public static ReadSaveResult<SerializableRun> GetMPRun(string name)
    {
        currentMPSave = name;
        PlatformType platformType = ((SteamInitializer.Initialized && !CommandLineHelper.HasArg("fastmp")) ? PlatformType.Steam : PlatformType.None);
        return SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(PlatformUtil.GetLocalPlayerId(platformType));
    }
}

[HarmonyPatch]
public class SubmenuPatch
{
    static NewContinueScreen? spContinueScreen;
    static NewAbandonScreen? spAbandonScreen;
    static NewMPContinueScreen? mpContinueScreen;
    static NewMPAbandonScreen? mpAbandonScreen;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NSubmenuStack), nameof(NSubmenuStack.InitializeForMainMenu))]
    static void ClearSubmenus(NMainMenu mainMenu)
    {
        spContinueScreen = null;
        spAbandonScreen = null;
        mpContinueScreen = null;
        mpAbandonScreen = null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NSubmenuStack), nameof(NSubmenuStack.Pop))]
    static void RefreshSavesOnPop()
    {
        try
        {
            Store.mainMenu?.RefreshButtons();
        }
        catch (ObjectDisposedException)
        {
            Store.mainMenu = null;
        }

        try
        {
            if (Store.submenu is not null)
                Store.submenu.GetType().GetMethod("UpdateButtons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(Store.submenu, new object?[] { });
        }
        catch (ObjectDisposedException)
        {
            Store.submenu = null;
        }
        
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NMainMenuSubmenuStack), nameof(NMainMenuSubmenuStack.GetSubmenuType), new Type[] {typeof(Type)})]
    static bool GetAddedSubmenus(Type type, NMainMenuSubmenuStack __instance, ref NSubmenu __result)
    {
        if (type == typeof(NewContinueScreen))
        {
            if (spContinueScreen == null)
            {
                spContinueScreen = NewContinueScreen.Create()!;
                spContinueScreen.Visible = false;
                __instance.AddChildSafely(spContinueScreen);
            }
            __result = spContinueScreen;
            return false;
        }

        if (type == typeof(NewAbandonScreen))
        {
            if (spAbandonScreen == null)
            {
                spAbandonScreen = NewAbandonScreen.Create()!;
                spAbandonScreen.Visible = false;
                __instance.AddChildSafely(spAbandonScreen);
            }
            __result = spAbandonScreen;
            return false;
        }

        if (type == typeof(NewMPContinueScreen))
        {
            if (mpContinueScreen == null)
            {
                mpContinueScreen = NewMPContinueScreen.Create()!;
                mpContinueScreen.Visible = false;
                __instance.AddChildSafely(mpContinueScreen);
            }
            __result = mpContinueScreen;
            return false;
        }

        if (type == typeof(NewMPAbandonScreen))
        {
            if (mpAbandonScreen == null)
            {
                mpAbandonScreen = NewMPAbandonScreen.Create()!;
                mpAbandonScreen.Visible = false;
                __instance.AddChildSafely(mpAbandonScreen);
            }
            __result = mpAbandonScreen;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(NMultiplayerSubmenu))]
public class MultiplayerMenuPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("UpdateButtons")]
    static void ReEnableButton(NSubmenuButton ____hostButton, NMultiplayerSubmenu __instance)
    {
        Store.submenu = __instance;
        if (SaveManager.Instance.HasMultiplayerRunSave)
        {
            ____hostButton.Visible = true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("StartLoad")]
    static bool ModifyContinue(NMultiplayerSubmenu __instance, NSubmenuStack ____stack)
    {
/*        if (Store.multiSaveCount == 1)
        {
            return true;
        }*/

        Store.submenu = __instance;
        ____stack.PushSubmenuType<NewMPContinueScreen>();
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("AbandonRun")]
    static bool ModifyAbandon(NMultiplayerSubmenu __instance, NSubmenuStack ____stack)
    {
/*        if (Store.multiSaveCount == 1)
        {
            return true;
        }*/

        Store.submenu = __instance;
        ____stack.PushSubmenuType<NewMPAbandonScreen>();
        return false;
    }

}


[HarmonyPatch(typeof(NMainMenu))]
public class MenuPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMainMenu.Create))]
    static void GrabMenu(bool openTimeline, ref NMainMenu __result)
    {
        Store.mainMenu = __result;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMainMenu.RefreshButtons))]
    static void ReEnableButton(NMainMenuTextButton ____singleplayerButton)
    {
        if (SaveManager.Instance.HasRunSave)
        {
            try
            {
                ____singleplayerButton.Visible = true;
            }
            catch (ObjectDisposedException)
            {
                Store.mainMenu = null;
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnContinueButtonPressed")]
    static bool ModifyContinue(NMainMenu __instance, NMainMenuTextButton ____continueButton, ref NMainMenuTextButton? ____lastHitButton)
    {
/*        if (Store.saveCount == 1)
            return true;*/

        ____lastHitButton = ____continueButton;
        __instance.SubmenuStack.PushSubmenuType<NewContinueScreen>();
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnAbandonRunButtonPressed")]
    static bool ModifyAbandon(NMainMenu __instance, NMainMenuTextButton ____abandonRunButton, ref NMainMenuTextButton? ____lastHitButton)
    {
/*        if (Store.saveCount == 1)
            return true;*/

        ____lastHitButton = ____abandonRunButton;
        __instance.SubmenuStack.PushSubmenuType<NewAbandonScreen>();
        return false;
    }
}

[HarmonyPatch(typeof(NMainMenuContinueButton))]
public class ContinueButtonPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("OnFocus")]
    static bool AllowContinuePopup()
    {
        return false;
        //return Store.saveCount == 1;
    }
}

[HarmonyPatch(typeof(RunManager))]
public class RunManagerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunManager.ToSave))]
    public static void ChangeSaveName(RunManager __instance, long ____startTime)
    {
        DateTime startTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        startTime = startTime.AddSeconds(____startTime).ToLocalTime();
        RunState? state = typeof(RunManager).GetProperty("State", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(__instance) as RunState;

        if (__instance.NetService.Type == NetGameType.Singleplayer)
        {
            Store.currentSPSave = startTime.ToString("MMM dd HH-mm") + " " + state!.Players[0].Character.Title.GetFormattedText();
        }
        else
        {
            Store.currentMPSave = startTime.ToString("MMM dd HH-mm");
            foreach (Player player in state!.Players)
            {
                Store.currentMPSave += " " + PlatformUtil.GetPlayerName(__instance.NetService.Platform, player.NetId) + " " + player.Character.Title.GetFormattedText();
            }
        }
    }
}

[HarmonyPatch(typeof(SaveManager))]
public class SaveManagerPatch
{
    [HarmonyReversePatch]
    [HarmonyPatch("CleanupStaleCurrentRunSaveForProfile")]
    static void CleanupStaleCurrentRunSaveForProfile(object instance, int profile, string savePath)
    {

    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(SaveManager.SyncCloudToLocal))]
    public static void SyncMoreSaves(ISaveStore ____saveStore, SaveManager __instance)
    {
        if (____saveStore is CloudSaveStore cloudSaveStore)
        {
            Log.Info("SyncCloud patch");
            for (int i = 1; i <= 3; i++)
            {
                string dir = Store.GetSaveDir(i);
                if (____saveStore.DirectoryExists(dir))
                {
                    IEnumerable<string> files = ____saveStore.GetFilesInDirectory(dir);
                    
                    HashSet<string> filesChecked = new HashSet<string>();
                    foreach (string file in files)
                    {
                        int lastDot = file.LastIndexOf('.');
                        if (lastDot == -1)
                            continue;

                        string extension = file.Substring(lastDot);
                        if (extension != ".backup" && extension != ".spsave" && extension != ".mpsave")
                            continue;

                        string fileName = file;
                        if (extension != ".backup")
                            cloudSaveStore.SyncCloudToLocal(Path.Combine(dir, file));
                        else
                            fileName = file.Substring(0, lastDot);

                        
                        if (!filesChecked.Contains(fileName))
                        {
                            filesChecked.Add(fileName);
                            Log.Info("Checking for stale "+fileName);
                            CleanupStaleCurrentRunSaveForProfile(__instance, i, "MoreSaves\\" + fileName);
                        }
                    }
                }
            }
        }
    }
}

[HarmonyPatch(typeof(RunSaveManager))]
public class RunSaveManagerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("CurrentRunSavePath", MethodType.Getter)]
    static bool SingleplayerPath(ref string __result, IProfileIdProvider ____profileIdProvider)
    {
        if (Store.currentSPSave == "current_run")
            return true;

        __result = Path.Combine(Store.SaveDir, Store.currentSPSave + ".spsave");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("CurrentMultiplayerRunSavePath", MethodType.Getter)]
    static bool MultiplayerPath(ref string __result, IProfileIdProvider ____profileIdProvider)
    {
        if (Store.currentMPSave == "current_run_mp")
            return true;

        __result = Path.Combine(Store.SaveDir, Store.currentMPSave + ".mpsave");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunSaveManager.HasRunSave), MethodType.Getter)]
    static bool HasSingleplayerRun(ref bool __result, ISaveStore ____saveStore, IProfileIdProvider ____profileIdProvider, RunSaveManager __instance)
    {
        Store.lastSaveStore = ____saveStore;
        if (!____saveStore.DirectoryExists(Store.SaveDir))
            ____saveStore.CreateDirectory(Store.SaveDir);

        string oldPath = RunSaveManager.GetRunSavePath(____profileIdProvider.CurrentProfileId, "current_run.save");

        IEnumerable<string> files = ____saveStore.GetFilesInDirectory(Store.SaveDir).Where((name) => name.Length > 6 && name.Substring(name.Length - 6) == "spsave");

        if (Store.currentSPSave == "" && ____saveStore.FileExists(oldPath))
        {
            Store.currentSPSave = "current_run";
            ReadSaveResult<SerializableRun> vanilla = __instance.LoadRunSave();

            if (vanilla.Success)
            {
                DateTime startTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                startTime = startTime.AddSeconds(vanilla.SaveData!.StartTime).ToLocalTime();

                string character = new LocString("characters", vanilla.SaveData!.Players[0].CharacterId!.Entry + ".title").GetFormattedText();

                string copyPath = startTime.ToString("MMM dd HH-mm") + " " + character;
                copyPath = Path.Combine(Store.SaveDir, copyPath + ".spsave");

                Log.Info("Moving from "+oldPath+" to "+copyPath);
                ____saveStore.RenameFile(oldPath, copyPath);
                files = files.AddItem(Store.currentSPSave + ".spsave");

                /*if (!____saveStore.FileExists(copyPath) || ____saveStore.GetLastModifiedTime(copyPath) < ____saveStore.GetLastModifiedTime(oldPath))
                {
                    ____saveStore.WriteFile(copyPath, ____saveStore.ReadFile(oldPath)!);
                    
                }*/
            }
        }

        Store.spSaves = files;
        Store.saveCount = files.Count();
        __result = Store.saveCount > 0;

/*        if (__result)
        {
            Store.currentSPSave = files.Last();
            Store.currentSPSave = Store.currentSPSave.Substring(0, Store.currentSPSave.Length - 7);
        }
        else
        {
            Store.currentSPSave = "";
        }*/
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunSaveManager.HasMultiplayerRunSave), MethodType.Getter)]
    static bool HasMultiplayerRun(ref bool __result, ISaveStore ____saveStore, IProfileIdProvider ____profileIdProvider, RunSaveManager __instance)
    {
        Store.lastSaveStore = ____saveStore;
        if (!____saveStore.DirectoryExists(Store.SaveDir))
            ____saveStore.CreateDirectory(Store.SaveDir);

        string oldPath = RunSaveManager.GetRunSavePath(____profileIdProvider.CurrentProfileId, "current_run_mp.save");

        IEnumerable<string> files = ____saveStore.GetFilesInDirectory(Store.SaveDir).Where((name) => name.Length > 6 && name.Substring(name.Length - 6) == "mpsave");

        if (Store.currentMPSave == "" && ____saveStore.FileExists(oldPath))
        {
            Store.currentMPSave = "current_run_mp";
            ReadSaveResult<SerializableRun> vanilla = __instance.LoadAndCanonicalizeMultiplayerRunSave(PlatformUtil.GetLocalPlayerId(PlatformUtil.PrimaryPlatform));

            if (vanilla.Success)
            {
                DateTime startTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                startTime = startTime.AddSeconds(vanilla.SaveData!.StartTime).ToLocalTime();

                string copyPath = startTime.ToString("MMM dd HH-mm");

                foreach (SerializablePlayer player in vanilla.SaveData!.Players)
                {
                    string character = new LocString("characters", player.CharacterId!.Entry + ".title").GetFormattedText();
                    copyPath += " " + PlatformUtil.GetPlayerName(PlatformUtil.PrimaryPlatform, player.NetId) + " " + character;
                }

                copyPath = Path.Combine(Store.SaveDir, copyPath + ".mpsave");

                Log.Info("Moving from " + oldPath + " to " + copyPath);
                ____saveStore.RenameFile(oldPath, copyPath);
                files = files.AddItem(Store.currentMPSave + ".mpsave");

                /*if (!____saveStore.FileExists(copyPath) || ____saveStore.GetLastModifiedTime(copyPath) < ____saveStore.GetLastModifiedTime(oldPath))
                {
                    ____saveStore.WriteFile(copyPath, ____saveStore.ReadFile(oldPath)!);
                    
                }*/
            }
        }

        Store.mpSaves = files;
        Store.multiSaveCount = files.Count();
        __result = Store.multiSaveCount > 0;

/*        if (__result)
        {
            Store.currentMPSave = files.Last();
            Store.currentMPSave = Store.currentMPSave.Substring(0, Store.currentMPSave.Length - 7);
        }
        else
        {
            Store.currentMPSave = "";
        }*/
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(RunSaveManager.DeleteCurrentMultiplayerRun))]
    public static void SetNameAfterMPDelete()
    {
        Store.currentMPSave = "";
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(RunSaveManager.DeleteCurrentRun))]
    public static void SetNameAfterSPDelete()
    {
        Store.currentSPSave = "";
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunSaveManager.LoadRunSave))]
    public static bool SkipSPLoadIfBlank(ReadSaveResult<SerializableRun> __result)
    {
        if (Store.currentSPSave == "")
        {
            __result = new ReadSaveResult<SerializableRun>(ReadSaveStatus.FileNotFound, "Tried to load while SPSave is blank");
            return false;
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunSaveManager.LoadMultiplayerRunSave))]
    public static bool SkipMPLoadIfBlank(ReadSaveResult<SerializableRun> __result)
    {
        if (Store.currentMPSave == "")
        {
            __result = new ReadSaveResult<SerializableRun>(ReadSaveStatus.FileNotFound, "Tried to load while MPSave is blank");
            return false;
        }
        return true;
    }
}