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
    public partial class NewMPAbandonScreen : NewBaseScreen
    {
        protected override string localisationBase => "CONTINUE_MENU";

        public override void _Ready()
        {
            base._Ready();
            GetNode<MegaLabel>("TitleLabel").SetTextAutoSize("Choose Run to Abandon");
            GetNode<NJoinFriendRefreshButton>("RefreshButton").Connect(NClickableControl.SignalName.Released, Callable.From<NButton>((_) => TaskHelper.RunSafely(BuildOptions())));
        }
        protected override void InnerBuildOptions()
        {
            foreach (string file in Store.mpSaves)
            {
                Store.Logger.Info($"Creating MP Abandon button for {file}");
                RunButton btn = RunButton.Create(file, false);
                buttonContainer.AddChildSafely(btn);
                btn.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(btn.AbandonMP));
            }
        }
        public static NewMPAbandonScreen? Create() => Create<NewMPAbandonScreen>();
    }
}
