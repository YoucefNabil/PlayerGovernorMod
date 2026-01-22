using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace PlayerGovernorMod
{
    public class PlayerGovernorBehavior : CampaignBehaviorBase
    {
        // This variable acts as our memory of where we were before the save started.
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
            // Added Hero.MainHero null check for safety
            if (town != null && Hero.MainHero != null && town.Governor == Hero.MainHero)
            {
                town.Governor = null;
                Hero.MainHero.GovernorOf = null;
                _currentTownPlayerIsGoverning = null;
                InformationManager.DisplayMessage(new InformationMessage($"You are no longer overseeing {town.Name}."));
            }
        }

        private void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
        {
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
            // Null check Guard for Hero.MainHero
            if (Hero.MainHero == null) return;

            // We temporarily strip the title, BUT we keep _currentTownPlayerIsGoverning populated.
            // This allows us to know exactly which town to restore in OnSaveOver without
            // asking the engine for "CurrentSettlement".
            if (_currentTownPlayerIsGoverning != null && _currentTownPlayerIsGoverning.Governor == Hero.MainHero)
            {
                _currentTownPlayerIsGoverning.Governor = null;
                Hero.MainHero.GovernorOf = null;
                // Note: We do NOT set _currentTownPlayerIsGoverning to null here. We need it for OnSaveOver.
                InformationManager.DisplayMessage(new InformationMessage($"SAVE CLEANUP: You are no longer governor."));
            }
        }

        private void OnSaveOver(bool isSuccessful, string saveName)
        {
            // 1. Critical Lifecycle Guards
            if (Campaign.Current == null || Hero.MainHero == null)
            {
                return;
            }

            // 2. Restore State using Cached Data

            if (_currentTownPlayerIsGoverning != null)
            {
                // Double check ownership hasn't magically changed and governor is still empty
                if (_currentTownPlayerIsGoverning.OwnerClan == Clan.PlayerClan && _currentTownPlayerIsGoverning.Governor == null)
                {
                    _currentTownPlayerIsGoverning.Governor = Hero.MainHero;

                    // Only display debug message if it's useful, or comment out for production
                    InformationManager.DisplayMessage(new InformationMessage($"SAVE CLEANUP OVER: You are back at being governor."));
                }

                // If for some reason the restore failed (e.g. town lost), clear the cache
                if (_currentTownPlayerIsGoverning.Governor != Hero.MainHero)
                {
                    _currentTownPlayerIsGoverning = null;
                }
            }
        }

        private void OnGameLoadFinished()
        {
            // On Load, it is safe to check CurrentSettlement because the game state is fully initialized.
            if (Campaign.Current != null && Hero.MainHero != null && Settlement.CurrentSettlement != null)
            {
                if (Settlement.CurrentSettlement.IsTown || Settlement.CurrentSettlement.IsCastle)
                {
                    Town town = Settlement.CurrentSettlement.Town;

                    if (town != null && town.OwnerClan == Clan.PlayerClan && town.Governor == null)
                    {
                        town.Governor = Hero.MainHero;
                        _currentTownPlayerIsGoverning = town;
                        InformationManager.DisplayMessage(new InformationMessage($"You are personally overseeing {Settlement.CurrentSettlement.Name}.", Color.FromUint(0xFF00FF00)));
                    }
                }
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Leave empty
        }
    }
}