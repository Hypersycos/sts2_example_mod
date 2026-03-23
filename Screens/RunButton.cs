using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MoreSaves.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreSaves.MainMenu
{
    [HarmonyPatch]
    public partial class RunButton : NJoinFriendButton
    {
        ReadSaveResult<SerializableRun>? readResult;
        string myName = "";
        bool isSP;

        public static RunButton Create(string name, bool isSP)
        {
            PackedScene scene = PreloadManager.Cache.GetScene(scenePath);
            NJoinFriendButton oldBtn = scene.Instantiate<NJoinFriendButton>(PackedScene.GenEditState.Disabled);

            var myScript = new RunButton();

            myScript.Size = new Vector2(384, 100);

            foreach (Node child in oldBtn.GetChildren())
            {
                if (child is Node2D n2d)
                {
                    Vector2 oldPos = n2d.GlobalPosition;
                    oldBtn.RemoveChild(child);
                    myScript.AddChild(child);
                    n2d.GlobalPosition = oldPos;
                }
                else if (child is Control c)
                {
                    Vector2 oldPos = c.GlobalPosition;

                    oldBtn.RemoveChild(child);
                    myScript.AddChild(child);

                    c.GlobalPosition = oldPos;
                }
            }

            myScript.myName = name;
            myScript.isSP = isSP;
            return myScript;
        }

        public override void _Ready()
        {
            //base._Ready();
            if (isSP)
                readResult = Store.GetSPRun(myName);
            else
                readResult = Store.GetMPRun(myName);

            ConnectSignals();
            MegaRichTextLabel node = GetNode<MegaRichTextLabel>("TextHolder/Text");
            NinePatchRect node2 = GetNode<NinePatchRect>("Image");
            node.Text = "[center]" + myName + "[/center]";
            typeof(NJoinFriendButton).GetField("_hsv", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                                     .SetValue(this, (ShaderMaterial?)node2?.Material);

            /*            runinfo.SetResult(readResult);*/
        }

        public void ContinueSP(NButton _)
        {
            if (readResult == null || !readResult.Success)
            {
                NErrorPopup modalToCreate = NErrorPopup.Create(new LocString("main_menu_ui", "INVALID_SAVE_POPUP.title"), new LocString("main_menu_ui", "INVALID_SAVE_POPUP.description_run"), new LocString("main_menu_ui", "INVALID_SAVE_POPUP.dismiss"), showReportBugButton: true)!;
                NModalContainer.Instance?.Add(modalToCreate);
                NModalContainer.Instance?.ShowBackstop();
                return;
            }
            Store.currentSPSave = myName;
            TaskHelper.RunSafely(ContinueSPAsync());
        }

        protected async Task ContinueSPAsync()
        {
            NAudioManager.Instance?.StopMusic();
            SerializableRun serializableRun = readResult!.SaveData!;
            RunState runState = RunState.FromSerializable(serializableRun);

            RunManager.Instance.SetUpSavedSinglePlayer(runState, serializableRun);
            Log.Info($"Continuing run with character: {serializableRun.Players[0].CharacterId}");

            SfxCmd.Play(runState.Players[0].Character.CharacterTransitionSfx);
            await NGame.Instance!.Transition.FadeOut(0.8f, runState.Players[0].Character.CharacterSelectTransitionPath);

            NGame.Instance.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
            await NGame.Instance.LoadRun(runState, serializableRun.PreFinishedRoom);
            await NGame.Instance.Transition.FadeIn();
        }

        public void ContinueMP(NButton _)
        {
            if (readResult == null || !readResult.Success)
            {
                Log.Warn("Broken multiplayer run save detected");
                NErrorPopup modalToCreate = NErrorPopup.Create(new LocString("main_menu_ui", "INVALID_SAVE_POPUP.title"), new LocString("main_menu_ui", "INVALID_SAVE_POPUP.description_run"), new LocString("main_menu_ui", "INVALID_SAVE_POPUP.dismiss"), showReportBugButton: true)!;
                NModalContainer.Instance!.Add(modalToCreate);
                NModalContainer.Instance.ShowBackstop();
                return;
            }
            Store.currentMPSave = myName;
            StartHost(Store.submenu!, readResult.SaveData!);
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(NMultiplayerSubmenu), nameof(NMultiplayerSubmenu.StartHost))]
        public static void StartHost(object instance, SerializableRun run)
        {

        }

        public void AbandonSP(NButton _)
        {
            Store.currentSPSave = myName;
            NModalContainer.Instance?.Add(NAbandonRunConfirmPopup.Create(Store.mainMenu)!);
        }

        public void AbandonMP(NButton _)
        {
            Store.currentMPSave = myName;

            AbandonRun(Store.submenu!, _);
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(NMultiplayerSubmenu), "AbandonRun")]
        public static void AbandonRun(object instance, NButton _)
        {

        }
    }
}
