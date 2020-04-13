using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BattleTech;
using System;
using static AbstractedArmorRepair.Logger;

namespace AbstractedArmorRepair
{
    class Helpers
    {
        /* Submits a Mech Lab Work Order to the game's Mech Lab queue to actually be processed */
        public static void SubmitWorkOrder(SimGameState simGame, WorkOrderEntry_MechLab newMechLabWorkOrder)
        {
            try
            {
                LogDebug("Begin SubmitWorkOrder(): ");

                // Now that all WO subentries are added, insert the base MechLab work order to the game's Mech Lab Work Order Queue as the highest priority (index 0)
                simGame.MechLabQueue.Insert(0, newMechLabWorkOrder);
                // Call this to properly Initialize the base Mech Lab WO and make it visible in the Mech Lab queue UI / timeline etc.
                simGame.InitializeMechLabEntry(newMechLabWorkOrder, newMechLabWorkOrder.GetCBillCost());
                // Force an update of the Mech Lab queue, false is to tell it a day isn't passing.
                simGame.UpdateMechLabWorkQueue(false);

                // Simple cost debugging for the log
                foreach (WorkOrderEntry subEntries in newMechLabWorkOrder.SubEntries)
                {
                    LogDebug(subEntries.Description + " Repair Tech Costs: " + subEntries.GetCost());
                }


                // Deduct the total CBill costs of the WO from player inventory. For some reason this isn't done automatically via the HBS WO system.
                simGame.AddFunds(-newMechLabWorkOrder.GetCBillCost(), "ArmorRepair", true);
            }
            catch (Exception ex)
            {
                Error(ex);
            }

        }

        public static WorkOrderEntry_MechLab CreateBaseMechLabOrder(SimGameState __instance, MechDef mech)
        {
            try
            {
                string mechGUID = mech.GUID;
                string mechName = "Unknown";
                mechName = mech.Description.Name;

                Logger.LogDebug("Creating base MechLab work order with params - " +
                    WorkOrderType.MechLabGeneric.ToString() +
                    " | WO String: MechLab-BaseWorkOrder" +
                    " | WO Description: " + string.Format("Modify 'Mech - {0}", mechName) +
                    " | Mech GUID: " + mechGUID +
                    " | Cost: 0" +
                    " | Toast Description: " + string.Format(__instance.Constants.Story.GeneralMechWorkOrderCompletedText, mechName)
                );

                return new WorkOrderEntry_MechLab(
                        WorkOrderType.MechLabGeneric,
                        "MechLab-BaseWorkOrder",
                        string.Format("Modify 'Mech - {0}", mechName),
                        mechGUID,
                        0,
                        string.Format(__instance.Constants.Story.GeneralMechWorkOrderCompletedText, mechName)
                );
            }
            catch (Exception ex)
            {
                Error(ex);
                return null;
            }

        }

        // Evaluates whether a given mech has any destroyed components
        public static bool CheckDestroyedComponents(MechDef mech)
        {
            // Default to not requesting any structure repair
            bool destroyedComponents = false;

            if (mech == null || mech.Inventory == null)
                return false;

            foreach (MechComponentRef mechComponentRef in mech.Inventory)
            {
                if (mechComponentRef == null)
                    continue;

                if (mechComponentRef.DamageLevel == ComponentDamageLevel.Destroyed)
                {
                    Logger.LogDebug(mech.Name + " has destroyed components: " + mechComponentRef.ComponentDefID);
                    destroyedComponents = true;
                    break; // Stop evaluating other components if a destroyed one has already been found
                }
            }

            return destroyedComponents;
        }
    }
}
