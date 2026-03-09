using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace PlayerGovernorMod
{
    public class PlayerGovernorBehavior : CampaignBehaviorBase
    {
        private Town _currentTownPlayerIsGoverning = null;
        private Settlement _pendingGovernorSettlement = null;
        private bool _strippedForSave = false;

        public override void RegisterEvents()
        {
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, OnBeforeSave);
            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(this, OnSaveOver);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        }

        // ── Two distinct checks with distinct purposes ────────────────────────────────

        // "Is the player currently registered as governor in the game state?"
        // Used for REMOVAL paths only — must work even when the player can no longer
        // hold the role (prisoner, dead). No CanBeGovernorOrHavePartyRole call here.
        private bool PlayerIsGovernor(Town town)
        {
            return town != null
                && Hero.MainHero != null
                && town.Governor == Hero.MainHero;
        }

        // "Is it valid to assign the player as governor right now?"
        // Used for ASSIGNMENT paths only.
        // IsDead is explicit because CanBeGovernorOrHavePartyRole only hard-codes
        // a prisoner check in its source — it does not cover the dead state.
        // OwnerClan is null-guarded because it can be null mid-save or during
        // ownership transitions.
        private bool PlayerCanBecomeGovernor(Town town)
        {
            return town != null
                && Hero.MainHero != null
                && !Hero.MainHero.IsDead
                && town.OwnerClan != null
                && town.OwnerClan == Clan.PlayerClan
                && town.Governor == null
                && Hero.MainHero.CanBeGovernorOrHavePartyRole();
        }

        // ── Governor management ──────────────────────────────────────────────────────

        private void TryAssignGovernor(Town town, Settlement settlement)
        {
            if (!PlayerCanBecomeGovernor(town)) return;

            town.Governor = Hero.MainHero;
            _currentTownPlayerIsGoverning = town;
            InformationManager.DisplayMessage(new InformationMessage(
                $"You are personally overseeing {settlement.Name}.",
                Color.FromUint(0xFF00FF00)));
        }

        private void RemovePlayerAsGovernor(Town town)
        {
            // Uses PlayerIsGovernor not PlayerCanBecomeGovernor — removal must work
            // even when the player can no longer hold the role.
            if (!PlayerIsGovernor(town)) return;

            town.Governor = null;
            _currentTownPlayerIsGoverning = null;

            // Player's HomeSettlement was caching the governed town.
            // Force recalculation now that GovernorOf is null.
            Hero.MainHero?.UpdateHomeSettlement();
        }

        // ── Event handlers ───────────────────────────────────────────────────────────

        private void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
        {
            if (hero != Hero.MainHero) return;
            if (!settlement.IsTown && !settlement.IsCastle) return;

            // Queue rather than assign immediately. OnCheckForIssueEvent fires during
            // settlement entry and iterates all notables — assigning governor mid-chain
            // can interact badly with that scan. Deferring one tick avoids the overlap.
            if (PlayerCanBecomeGovernor(settlement.Town))
                _pendingGovernorSettlement = settlement;
        }

        private void OnSettlementLeft(MobileParty mobileParty, Settlement settlement)
        {
            if (mobileParty == null || !mobileParty.IsMainParty) return;
            if (!settlement.IsTown && !settlement.IsCastle) return;

            _pendingGovernorSettlement = null;
            RemovePlayerAsGovernor(settlement.Town);
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (settlement.Town == null) return;
            if (newOwner?.Clan == Clan.PlayerClan) return;

            // Cancel any pending assignment first.
            if (_pendingGovernorSettlement?.Town == settlement.Town)
                _pendingGovernorSettlement = null;

            // Defensively call RemovePlayerAsGovernor rather than assuming the engine
            // already nulled Governor during the ownership transfer. ChangeClanInternal
            // in Town does not null the governor. PlayerIsGovernor guards the call so
            // it is safe even if the engine already cleared it.
            RemovePlayerAsGovernor(settlement.Town);
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (prisoner != Hero.MainHero) return;

            // Player captured — strip governor and cancel pending assignment.
            // CanBeGovernorOrHavePartyRole now returns false so we bypass it
            // and call RemovePlayerAsGovernor directly (uses PlayerIsGovernor).
            _pendingGovernorSettlement = null;
            RemovePlayerAsGovernor(_currentTownPlayerIsGoverning);
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (_currentTownPlayerIsGoverning == null) return;

            // MapEventSettlement is null for open-field battles — always null-guard it.
            if (mapEvent.MapEventSettlement?.Town == _currentTownPlayerIsGoverning)
                RemovePlayerAsGovernor(_currentTownPlayerIsGoverning);
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            // After a sally out or siege the player may still be inside but no longer
            // governor — re-queue so the hourly tick can restore the role if valid.
            // MapEventSettlement is null for field battles so we null-guard it.
            Settlement eventSettlement = mapEvent.MapEventSettlement;
            if (eventSettlement == null) return;
            if (!eventSettlement.IsTown && !eventSettlement.IsCastle) return;

            if (Settlement.CurrentSettlement != eventSettlement) return;

            if (PlayerCanBecomeGovernor(eventSettlement.Town))
                _pendingGovernorSettlement = eventSettlement;
        }

        private void OnHourlyTick()
        {
            if (_pendingGovernorSettlement == null) return;

            Settlement settlement = _pendingGovernorSettlement;
            _pendingGovernorSettlement = null; // Always clear before any early return

            // Guard against the settlement object being invalidated between the tick
            // that queued it and this tick firing (e.g. ownership change, game reload).
            if (!settlement.IsReady) return;

            TryAssignGovernor(settlement.Town, settlement);
        }

        private void OnBeforeSave()
        {
            _strippedForSave = false;
            if (!PlayerIsGovernor(_currentTownPlayerIsGoverning)) return;

            // Strip before serialisation. The engine rejects saves where the player
            // (a mobile hero) is flagged as settlement governor. Restored in OnSaveOver.
            // _currentTownPlayerIsGoverning is intentionally kept for the restore.
            _currentTownPlayerIsGoverning.Governor = null;
            _strippedForSave = true;
        }

        private void OnSaveOver(bool isSuccessful, string saveName)
        {
            if (!_strippedForSave) return;
            _strippedForSave = false;

            // Full lifecycle guard — Campaign, hero, and the town object itself must
            // all be valid. The community-reported null ref in this method traced to
            // the town or its OwnerClan being in an invalid state during the save tick.
            if (Campaign.Current == null) return;
            if (Hero.MainHero == null || Hero.MainHero.IsDead) return;
            if (_currentTownPlayerIsGoverning == null) return;
            if (!_currentTownPlayerIsGoverning.IsReady) return;

            // Re-validate fully — state may have changed during the save tick.
            if (PlayerCanBecomeGovernor(_currentTownPlayerIsGoverning))
            {
                _currentTownPlayerIsGoverning.Governor = Hero.MainHero;
            }
            else
            {
                _currentTownPlayerIsGoverning = null;
            }
        }

        private void OnGameLoadFinished()
        {
            if (Campaign.Current == null || Hero.MainHero == null) return;

            Settlement current = Settlement.CurrentSettlement;
            if (current == null) return;
            if (!current.IsTown && !current.IsCastle) return;

            // Defer for the same reason as OnSettlementEntered.
            if (PlayerCanBecomeGovernor(current.Town))
                _pendingGovernorSettlement = current;
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
