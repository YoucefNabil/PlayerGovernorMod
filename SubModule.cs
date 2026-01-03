using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace PlayerGovernorMod
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            // Ensure we are only adding this to the Campaign mode (not Custom Battle)
            if (gameStarterObject is CampaignGameStarter campaignStarter)
            {
                // Register your behavior class here
                campaignStarter.AddBehavior(new PlayerGovernorBehavior());
            }
        }
    }
}