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
using MegaCrit.Sts2.Core.Nodes;
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
using System.Runtime.InteropServices;
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

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new("MoreSaves", MegaCrit.Sts2.Core.Logging.LogType.Generic);
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
    public static void ClearSubmenus(NMainMenu mainMenu)
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

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMultiplayerSubmenu._Ready))]
    static void RewireButtons(NMultiplayerSubmenu __instance, NMainMenuTextButton ____loadButton, NMainMenuTextButton ____abandonButton)
    {
        Store.submenu = __instance;

        foreach (var conn in ____loadButton.GetSignalConnectionList(NClickableControl.SignalName.Released))
        {
            Callable callable = (Callable)conn["callable"];
            StringName signal = conn["signal"].AsSignal().Name;

            if (____loadButton.IsConnected(signal, callable))
            {
                ____loadButton.Disconnect(signal, callable);
            }
        }

        foreach (var conn in ____abandonButton.GetSignalConnectionList(NClickableControl.SignalName.Released))
        {
            Callable callable = (Callable)conn["callable"];
            StringName signal = conn["signal"].AsSignal().Name;

            if (____abandonButton.IsConnected(signal, callable))
            {
                ____abandonButton.Disconnect(signal, callable);
            }
        }

        Callable continueMenu = Callable.From((NButton b) => Store.mainMenu!.SubmenuStack.PushSubmenuType<NewMPContinueScreen>());
        ____loadButton.Connect(NClickableControl.SignalName.Released, continueMenu);

        Callable abandonMenu = Callable.From((NButton b) => Store.mainMenu!.SubmenuStack.PushSubmenuType<NewMPAbandonScreen>());
        ____abandonButton.Connect(NClickableControl.SignalName.Released, abandonMenu);
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
        SubmenuPatch.ClearSubmenus(__result);
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

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMainMenu._Ready))]
    static void RewireButtons(NMainMenu __instance, NMainMenuTextButton ____continueButton, NMainMenuTextButton ____abandonRunButton, ref NMainMenuTextButton? ____lastHitButton)
    {
        foreach (var conn in ____continueButton.GetSignalConnectionList(NClickableControl.SignalName.Released))
        {
            Callable callable = (Callable)conn["callable"];
            StringName signal = conn["signal"].AsSignal().Name;

            if (____continueButton.IsConnected(signal, callable))
            {
                ____continueButton.Disconnect(signal, callable);
            }
        }

        foreach (var conn in ____abandonRunButton.GetSignalConnectionList(NClickableControl.SignalName.Released))
        {
            Callable callable = (Callable)conn["callable"];
            StringName signal = conn["signal"].AsSignal().Name;

            if (____abandonRunButton.IsConnected(signal, callable))
            {
                ____abandonRunButton.Disconnect(signal, callable);
            }
        }

        Callable continueMenu = Callable.From((NButton b) => __instance.SubmenuStack.PushSubmenuType<NewContinueScreen>());
        ____continueButton.Connect(NClickableControl.SignalName.Released, continueMenu);

        Callable abandonMenu = Callable.From((NButton b) => __instance.SubmenuStack.PushSubmenuType<NewAbandonScreen>());
        ____abandonRunButton.Connect(NClickableControl.SignalName.Released, abandonMenu);
    }
}

[HarmonyPatch(typeof(NContinueRunInfo))]
public class ContinueButtonPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NContinueRunInfo.HasResult), MethodType.Getter)]
    static bool DisableContinuePopup(ref bool __result)
    {
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(RunManager))]
public class RunManagerPatch
{
    public static string FilterInvalid(string unfiltered)
    {
        return String.Join("", unfiltered.Split('/', '<', '>', ':', '"', '\\', '|', '?', '*'));
    }

    public static string GetFilteredCharacter(LocString characterTitle)
    {
        return FilterInvalid(characterTitle.GetFormattedText());
    }

    public static string GetSingleplayerName(long startTime, int ascension, LocString characterTitle)
    {
        DateTime startDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        startDate = startDate.AddSeconds(startTime).ToLocalTime();
        return $"{startDate.ToString("MMM dd HH-mm")} A{ascension} {GetFilteredCharacter(characterTitle)}";
    }

    public static string GetMultiplayerName(long startTime, int ascension, IEnumerable<KeyValuePair<LocString, ulong>> characterTitles, PlatformType platform)
    {
        DateTime startDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        startDate = startDate.AddSeconds(startTime).ToLocalTime();

        var getName = (KeyValuePair<LocString, ulong> x) => $"{FilterInvalid(PlatformUtil.GetPlayerName(platform, x.Value))} ({GetFilteredCharacter(x.Key)})";

        return $"{startDate.ToString("MMM dd HH-mm")} A{ascension} {String.Join(", ", characterTitles.Select(getName))}";
    }

    [HarmonyPrefix]
    [HarmonyPatch("InitializeNewRun")]
    public static void ChangeSaveName(RunManager __instance, long ____startTime)
    {
        RunState? state = typeof(RunManager).GetProperty("State", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(__instance) as RunState;

        if (__instance.NetService.Type == NetGameType.Singleplayer)
        {
            Store.currentSPSave = GetSingleplayerName(____startTime, state!.AscensionLevel, state.Players[0].Character.Title);
        }
        else
        {
            
            Store.currentMPSave = GetMultiplayerName(____startTime, state!.AscensionLevel, state.Players.Select((x) => new KeyValuePair<LocString, ulong>(x.Character.Title, x.NetId)), __instance.NetService.Platform);
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
            for (int i = 1; i <= 3; i++)
            {
                string dir = Store.GetSaveDir(i);
                if (cloudSaveStore.CloudStore.DirectoryExists(dir))
                {
                    IEnumerable<string> files = cloudSaveStore.CloudStore.GetFilesInDirectory(dir);
                    IEnumerable<string> localFiles = [];
                    if (cloudSaveStore.LocalStore.DirectoryExists(dir))
                        localFiles = cloudSaveStore.LocalStore.GetFilesInDirectory(dir);

                    IEnumerable<string> mergedFiles = files.Union(localFiles);

                    HashSet<string> filesChecked = new HashSet<string>();
                    foreach (string file in mergedFiles)
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

        HashSet<string> files = new HashSet<string>();
        IEnumerable<string> fullFiles = ____saveStore.GetFilesInDirectory(Store.SaveDir).Where((name) => (name.Length > 6 && name.Substring(name.Length - 6) == "spsave") ||
                                                                                                (name.Length > 13 && name.Substring(name.Length - 13) == "spsave.backup"));
        foreach (string file in fullFiles)
        {
            if (file[file.Length - 6] == 's')
                files.Add(file.Substring(0, file.Length - 7));
            else
                files.Add(file.Substring(0, file.Length - 14));
        }

        if (Store.currentSPSave == "" && (____saveStore.FileExists(oldPath) || ____saveStore.FileExists(oldPath+".backup")))
        {
            Store.currentSPSave = "current_run";
            ReadSaveResult<SerializableRun> vanilla = __instance.LoadRunSave();

            if (vanilla.Success)
            {
                LocString characterTitle = new LocString("characters", vanilla.SaveData!.Players[0].CharacterId!.Entry + ".title");

                string newName = RunManagerPatch.GetSingleplayerName(vanilla.SaveData!.StartTime, vanilla.SaveData.Ascension, characterTitle);
                string copyPath = Path.Combine(Store.SaveDir, newName + ".spsave");

                Store.Logger.Info("Moving from " + oldPath + " to " + copyPath);

                if (____saveStore.FileExists(oldPath))
                    ____saveStore.RenameFile(oldPath, copyPath);

                if (____saveStore.FileExists(oldPath + ".backup"))
                {
                    if (____saveStore is CloudSaveStore cloudStore)
                        cloudStore.LocalStore.RenameFile(oldPath + ".backup", copyPath + ".backup");
                    else if (____saveStore is not ICloudSaveStore)
                        ____saveStore.RenameFile(oldPath + ".backup", copyPath + ".backup");
                }

                files.Add(newName);
            }

            Store.currentSPSave = "";
        }

        Store.spSaves = files;
        Store.saveCount = files.Count();
        __result = Store.saveCount > 0;
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

        HashSet<string> files = new HashSet<string>();
        IEnumerable<string> fullFiles = ____saveStore.GetFilesInDirectory(Store.SaveDir).Where((name) => (name.Length > 6 && name.Substring(name.Length - 6) == "mpsave") ||
                                                                                                (name.Length > 13 && name.Substring(name.Length - 13) == "mpsave.backup"));
        foreach(string file in fullFiles)
        {
            if (file[file.Length - 6] == 'm')
                files.Add(file.Substring(0,file.Length-7));
            else
                files.Add(file.Substring(0,file.Length-14));
        }

        if (Store.currentMPSave == "" && (____saveStore.FileExists(oldPath) || ____saveStore.FileExists(oldPath+".backup")))
        {
            Store.currentMPSave = "current_run_mp";
            ReadSaveResult<SerializableRun> vanilla = __instance.LoadAndCanonicalizeMultiplayerRunSave(PlatformUtil.GetLocalPlayerId(PlatformUtil.PrimaryPlatform));

            if (vanilla.Success)
            {
                var getTitles = (SerializablePlayer x) => new KeyValuePair<LocString, ulong>(new LocString("characters", x.CharacterId!.Entry + ".title"), x.NetId);

                string newName = RunManagerPatch.GetMultiplayerName(vanilla.SaveData!.StartTime, vanilla.SaveData.Ascension, vanilla.SaveData!.Players.Select(getTitles), PlatformUtil.PrimaryPlatform);

                string copyPath = Path.Combine(Store.SaveDir, newName + ".mpsave");

                Store.Logger.Info("Moving from " + oldPath + " to " + copyPath);

                if (____saveStore.FileExists(oldPath))
                    ____saveStore.RenameFile(oldPath, copyPath);

                if (____saveStore.FileExists(oldPath + ".backup"))
                {
                    if (____saveStore is CloudSaveStore cloudStore)
                        cloudStore.LocalStore.RenameFile(oldPath + ".backup", copyPath + ".backup");
                    else if (____saveStore is not ICloudSaveStore)
                        ____saveStore.RenameFile(oldPath + ".backup", copyPath + ".backup");
                }

                files.Add(newName);
            }

            Store.currentMPSave = "";
        }

        Store.mpSaves = files;
        Store.multiSaveCount = files.Count();
        __result = Store.multiSaveCount > 0;
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
    public static bool SkipSPLoadIfBlank(ref ReadSaveResult<SerializableRun> __result)
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
    public static bool SkipMPLoadIfBlank(ref ReadSaveResult<SerializableRun> __result)
    {
        if (Store.currentMPSave == "")
        {
            __result = new ReadSaveResult<SerializableRun>(ReadSaveStatus.FileNotFound, "Tried to load while MPSave is blank");
            return false;
        }
        return true;
    }
}