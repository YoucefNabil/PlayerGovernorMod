using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace PlayerGovernorMod
{
    public class PlayerGovernorBehavior : CampaignBehaviorBase
    {
        private Town _currentTownPlayerIsGoverning = null;

        public override void RegisterEvents()
        {
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, OnBeforeSave);
            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(this, OnSaveOver);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
        }


        private void RemovePlayerAsGovernor(Town town)
        {
            if (town != null && town.Governor == Hero.MainHero)
            {
                town.Governor = null;
                Hero.MainHero.GovernorOf = null;
                _currentTownPlayerIsGoverning = null;
                InformationManager.DisplayMessage(new InformationMessage($"You are no longer overseeing {town.Name}."));
            }
        }

        private void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
        {
            // Ensure it's the player entering a town/castle they own
            if (hero == Hero.MainHero && (settlement.IsTown || settlement.IsCastle))
            {
                Town town = settlement.Town;
                if (town != null && town.OwnerClan == Clan.PlayerClan && town.Governor == null)
                {
                    town.Governor = Hero.MainHero;
                    _currentTownPlayerIsGoverning = town;
                    InformationManager.DisplayMessage(new InformationMessage($"You are personally overseeing {settlement.Name}.", Color.FromUint(0xFF00FF00)));
                }
            }
        }

        private void OnSettlementLeft(MobileParty mobileParty, Settlement settlement)
        {
            if (mobileParty != null && mobileParty.IsMainParty && (settlement.IsTown || settlement.IsCastle))
            {
                RemovePlayerAsGovernor(settlement.Town);
            }
        }

        private void OnBeforeSave()
        {
            // Strip player governor status before the save engine serializes the Town data
            if (_currentTownPlayerIsGoverning != null && _currentTownPlayerIsGoverning.Governor == Hero.MainHero)
            {
                _currentTownPlayerIsGoverning.Governor = null;
                Hero.MainHero.GovernorOf = null;
                InformationManager.DisplayMessage(new InformationMessage($"SAVE CLEANUP: You are no longer governor."));
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
            // Re-apply the governor status after the save file is finished writing
            // Check if player is still currently in a valid settlement
            if (Settlement.CurrentSettlement != null && (Settlement.CurrentSettlement.IsTown || Settlement.CurrentSettlement.IsCastle))
            {
                Town town = Settlement.CurrentSettlement.Town;
                if (town != null && town.OwnerClan == Clan.PlayerClan && town.Governor == null)
                {
                    town.Governor = Hero.MainHero;
                    _currentTownPlayerIsGoverning = town;
                    InformationManager.DisplayMessage(new InformationMessage($"SAVE CLEANUP OVER: You are back at being governor."));
                }
            }
        }

        private void OnGameLoadFinished()
        {
            // When the game loads, check if we are already sitting in a town we own
            if (Settlement.CurrentSettlement != null && (Settlement.CurrentSettlement.IsTown || Settlement.CurrentSettlement.IsCastle))
            {
                Town town = Settlement.CurrentSettlement.Town;

                // Ensure the player owns it and it hasn't already had a governor assigned in the save
                if (town != null && town.OwnerClan == Clan.PlayerClan && town.Governor == null)
                {
                    town.Governor = Hero.MainHero;
                    _currentTownPlayerIsGoverning = town;

                    // Notifying the player on load since they are technically "entering" the modded state
                    InformationManager.DisplayMessage(new InformationMessage($"You are personally overseeing {Settlement.CurrentSettlement.Name}.", Color.FromUint(0xFF00FF00)));
                }
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Leave empty to ensure no custom mod data is injected into the save file structure
        }
    }
}