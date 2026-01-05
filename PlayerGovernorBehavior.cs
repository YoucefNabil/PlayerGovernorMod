using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace PlayerGovernorMod
{
    public class PlayerGovernorBehavior : CampaignBehaviorBase
    {
        // We track the town reference here so we don't have to ask the 
        // engine for "CurrentSettlement" during sensitive save/load events.
        private Town _currentTownPlayerIsGoverning = null;

        public override void RegisterEvents()
        {
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, OnBeforeSave);
            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(this, OnSaveOver);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
        }

        private void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
        {
            if (hero == Hero.MainHero && (settlement.IsTown || settlement.IsCastle))
            {
                Town town = settlement.Town;
                // Only take over if player owns it and no one else is governing
                if (town != null && town.OwnerClan == Clan.PlayerClan && town.Governor == null)
                {
                    ApplyGovernorStatus(town);
                    InformationManager.DisplayMessage(new InformationMessage($"You are personally overseeing {settlement.Name}.", Color.FromUint(0xFF00FF00)));
                }
            }
        }

        private void OnSettlementLeft(MobileParty mobileParty, Settlement settlement)
        {
            if (mobileParty != null && mobileParty.IsMainParty)
            {
                RemoveGovernorStatus();
                InformationManager.DisplayMessage(new InformationMessage($"You are no longer governor."));
            }
        }

        private void OnBeforeSave()
        {
            // Remove the player from the governor slot so the save file remains "vanilla"
            if (_currentTownPlayerIsGoverning != null)
            {
                _currentTownPlayerIsGoverning.Governor = null;
                if (Hero.MainHero != null)
                {
                    Hero.MainHero.GovernorOf = null;
                    InformationManager.DisplayMessage(new InformationMessage($"SAVE CLEANUP: You are no longer governor."));
                }
            }
        }

        private void OnSaveOver(bool isSuccessful, string saveName)
        {
            // 1. Check if the Campaign object still exists. 
            // If Campaign.Current is null, Hero.MainHero will CRASH.
            if (TaleWorlds.CampaignSystem.Campaign.Current == null)
            {
                return;
            }

            try
            {
                // 2. Use the local variable check. 
                // We use the full path to avoid any ambiguity during shutdown.
                if (_currentTownPlayerIsGoverning != null && TaleWorlds.CampaignSystem.Hero.MainHero != null)
                {
                    ApplyGovernorStatus(_currentTownPlayerIsGoverning);
                    InformationManager.DisplayMessage(new InformationMessage($"SAVE OVER: You are once again the governor."));
                }
            }
            catch
            {
                // Fail silently during shutdown
            }
        }

        private void OnGameLoadFinished()
        {
            // Use Hero.MainHero.CurrentSettlement as it is more stable on load than Settlement.CurrentSettlement
            Settlement current = Hero.MainHero?.CurrentSettlement;
            if (current != null && (current.IsTown || current.IsCastle))
            {
                Town town = current.Town;
                if (town != null && town.OwnerClan == Clan.PlayerClan && town.Governor == null)
                {
                    ApplyGovernorStatus(town);
                    InformationManager.DisplayMessage(new InformationMessage($"Resuming oversight of {current.Name}.", Color.FromUint(0xFF00FF00)));
                }
            }
        }

        private void ApplyGovernorStatus(Town town)
        {
            if (town == null || Hero.MainHero == null) return;

            town.Governor = Hero.MainHero;
            Hero.MainHero.GovernorOf = town;
            _currentTownPlayerIsGoverning = town;
        }

        private void RemoveGovernorStatus()
        {
            if (_currentTownPlayerIsGoverning != null)
            {
                _currentTownPlayerIsGoverning.Governor = null;
                _currentTownPlayerIsGoverning = null;
            }

            if (Hero.MainHero != null)
            {
                Hero.MainHero.GovernorOf = null;
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Explicitly empty to keep saves clean
        }
    }
}