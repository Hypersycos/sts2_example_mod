using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MoreSaves.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MoreSaves.MainMenu
{
    public partial class NewMPContinueScreen : NewBaseScreen
    {
        protected override string localisationBase => "CONTINUE_MENU";

        protected override void InnerBuildOptions()
        {
            foreach(string file in Store.mpSaves)
            {
                Store.Logger.Info($"Creating MP Continue button for {file}");
                RunButton btn = RunButton.Create(file, false);
                buttonContainer.AddChildSafely(btn);
                btn.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(btn.ContinueMP));
            }
        }
        public static NewMPContinueScreen? Create()
        {
            NewMPContinueScreen? screen = Create<NewMPContinueScreen>();
            screen!.GetNode<NJoinFriendRefreshButton>("RefreshButton").Visible = false;
            screen!.GetNode<MegaLabel>("TitleLabel").SetTextAutoSize("Choose Run to Continue");
            return screen;
        }
    }
}
