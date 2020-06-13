using System;
using System.Linq;
using BattleTech;
using BattleTech.UI;
using Harmony;
using UnityEngine;
using System.Collections.Generic;
using static AbstractedArmorRepair.Logger;

namespace AbstractedArmorRepair
{
    [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
    public static class SimGameState_ResolveCompleteContract_Patch
    {
        public static void Prefix(SimGameState __instance)
        {
        }

        // Run after completion of contracts and queue up any orders in the temp queue into the game's Mech Lab queue 
        public static void Postfix(SimGameState __instance)
        {
            try
            {
                foreach (var mechDef in Core.CombatMechs)
                {
                    if (mechDef.MechDefCurrentStructure < mechDef.MechDefMaxStructure)
                        continue;

                    bool componentDamage = false;
                    for (int i = 0; i < mechDef.inventory.Length; i++)
                    {
                        if (mechDef.inventory[i].DamageLevel == ComponentDamageLevel.Destroyed ||
                            mechDef.inventory[i].DamageLevel == ComponentDamageLevel.NonFunctional)
                        {
                            componentDamage = true;
                        }
                    }
                    if (componentDamage)
                        continue;

                    var tempWO = Helpers.CreateBaseMechLabOrder(__instance, mechDef);
                    tempWO.AddSubEntry(Repair_ReArm.RepairArmorMechDef(mechDef));
                    Core.tempMechLabQueue.Add(tempWO);
                }


                // If there are any work orders in the temporary queue, prompt the player
                if (Core.tempMechLabQueue.Count > 0)
                {
                    Logger.LogDebug("Processing temp Mech Lab queue orders.");

                    int cbills = 0;
                    int techCost = 0;
                    int mechRepairCount = 0;
                    int skipMechCount = 0;
                    string mechRepairCountDisplayed = String.Empty;
                    string skipMechCountDisplayed = String.Empty;
                    string skipMechMessage = String.Empty;
                    string finalMessage = String.Empty;

                    //int Counter = Core.tempMechLabQueue.Count;
                    //for (int index = Counter - 1; index < 0; index--)
                    //{
                    //    if (Counter == 0)
                    //        break;

                    //    WorkOrderEntry_MechLab order = Core.tempMechLabQueue[index];

                    //    LogDebug("Checking for destroyed components.");
                    //    bool destroyedComponents = false;
                    //    MechDef mech = __instance.GetMechByID(order.MechID);
                    //    if (mech != null)
                    //        destroyedComponents = Helpers.CheckDestroyedComponents(mech);
                    //    else
                    //        destroyedComponents = true;

                    //    if (destroyedComponents)
                    //    {
                    //        // Remove this work order from the temp mech lab queue if the mech has destroyed components and move to next iteration
                    //        Logger.LogDebug("Removing " + mech.Name + " order from temp queue due to destroyed components and mod settings.");
                    //        Core.tempMechLabQueue.Remove(order);
                    //        destroyedComponents = false;
                    //        skipMechCount++;
                    //    }
                    //}

                    // Calculate summary of total repair costs from the temp work order queue
                    for (int index = 0; index < Core.tempMechLabQueue.Count; index++)
                    {
                        if (Core.tempMechLabQueue.Count == 0)
                            break;

                        WorkOrderEntry_MechLab order = Core.tempMechLabQueue[index];
                        MechDef mech = __instance.GetMechByID(order.MechID);
                        LogDebug("Adding " + mech.Name + " to RepairCount.");
                        cbills += order.GetCBillCost();
                        techCost += order.GetCost();
                        mechRepairCount++;
                    }

                    mechRepairCount = Mathf.Clamp(mechRepairCount, 0, 6);

                    // If Yang's Auto Repair prompt is enabled, build a message prompt dialog for the player
                    if (true)
                    {

                        // Calculate a friendly techCost of the work order in days, based on number of current mechtechs in the player's game.
                        if (techCost != 0 && __instance.MechTechSkill != 0)
                        {
                            techCost = Mathf.CeilToInt((float)techCost / (float)__instance.MechTechSkill);
                        }
                        else
                        {
                            techCost = 1; // Safety in case of weird div/0
                        }

                        // Generate a quick friendly description of how many mechs were damaged in battle
                        switch (mechRepairCount)
                        {
                            case 0: { Logger.LogDebug("mechRepairCount was 0."); break; }
                            case 1: { mechRepairCountDisplayed = "one of our 'Mechs had only its armor"; break; }
                            case 2: { mechRepairCountDisplayed = "a couple of the 'Mechs had only their armor"; break; }
                            case 3: { mechRepairCountDisplayed = "three of our 'Mechs had only their armor"; break; }
                            case 4: { mechRepairCountDisplayed = "an entire lance of ours had only their armor"; break; }
                            case 5: { mechRepairCountDisplayed = "five of our 'Mechs had only their armor"; break; }
                            case 6: { mechRepairCountDisplayed = "every 'Mech we dropped with had only their armor"; break; }
                            default: { mechRepairCountDisplayed = "more than six 'Mechs had only their armor"; break; }
                        }
                        // Generate a friendly description of how many mechs were damaged but had components destroyed
                        switch (skipMechCount)
                        {
                            case 0: { Logger.LogDebug("skipMechCount was 0."); break; }
                            case 1: { skipMechCountDisplayed = "one of the 'Mechs is damaged but has"; break; }
                            case 2: { skipMechCountDisplayed = "two of the 'Mechs are damaged but have"; break; }
                            case 3: { skipMechCountDisplayed = "three of the 'Mechs are damaged but have"; break; }
                            case 4: { skipMechCountDisplayed = "the whole lance is damaged but has"; break; }
                            case 5: { skipMechCountDisplayed = "five of our 'Mechs are damaged but have"; break; }
                            case 6: { skipMechCountDisplayed = "every 'Mech we dropped with is damaged but has"; break; }
                            default: { skipMechCountDisplayed = "more than six 'Mechs are damaged but have"; break; }
                        }

                        // Check if there are any mechs to process
                        if (mechRepairCount > 0 || skipMechCount > 0)
                        {
                            Logger.LogDebug("mechRepairCount is " + mechRepairCount + " skipMechCount is " + skipMechCount);

                            // Setup the notification for mechs with damaged components that we might want to skip
                            if (skipMechCount > 0 && mechRepairCount == 0)
                            {
                                skipMechMessage = String.Format("{0} destroyed components. I'll leave the repairs for you to review.", skipMechCountDisplayed);
                            }
                            else
                            {
                                skipMechMessage = String.Format("{0} destroyed components, so I'll leave those repairs to you.", skipMechCountDisplayed);
                            }

                            Logger.LogDebug("Firing Yang's UI notification.");
                            SimGameInterruptManager notificationQueue = __instance.GetInterruptQueue();

                            // If all of the mechs needing repairs have damaged components and should be skipped from auto-repair, change the message notification structure to make more sense (e.g. just have an OK button)
                            if (skipMechCount > 0 && mechRepairCount == 0)
                            {
                                finalMessage = String.Format(
                                    "Boss, {0} \n\n",
                                    skipMechMessage
                                );

                                // Queue Notification
                                notificationQueue.QueuePauseNotification(
                                    "'Mech Armor Repairs Needed",
                                    finalMessage,
                                    __instance.GetCrewPortrait(SimGameCrew.Crew_Yang),
                                    string.Empty,
                                    delegate
                                    {
                                        Logger.LogDebug("[PROMPT] All damaged mechs had destroyed components and won't be queued for repair.");
                                    },
                                    "OK"
                                );
                            }
                            else
                            {
                                if (skipMechCount > 0)
                                {
                                    finalMessage = String.Format(
                                        "Boss, {0} damaged. It'll cost <color=#DE6729>{1}{2:n0}</color> and {3} days for these repairs. Want my crew to get started?\n\nAlso, {4}\n\n",
                                        mechRepairCountDisplayed,
                                        '¢', cbills.ToString(),
                                        techCost.ToString(),
                                        skipMechMessage
                                    );
                                }
                                else
                                {
                                    finalMessage = String.Format(
                                        "Boss, {0} damaged on the last engagement. It'll cost <color=#DE6729>{1}{2:n0}</color> and {3} days for the repairs.\n\nWant my crew to get started?",
                                        mechRepairCountDisplayed,
                                        '¢', cbills.ToString(),
                                        techCost.ToString()
                                    );
                                }


                                // Queue up Yang's notification
                                notificationQueue.QueuePauseNotification(
                                    "'Mech Armor Repairs Needed",
                                    finalMessage,
                                    __instance.GetCrewPortrait(SimGameCrew.Crew_Yang),
                                    string.Empty,
                                    delegate
                                    {
                                        Logger.LogDebug("[PROMPT] Moving work orders from temp queue to Mech Lab queue: " + Core.tempMechLabQueue.Count + " work orders");
                                        foreach (WorkOrderEntry_MechLab workOrder in Core.tempMechLabQueue.ToList())
                                        {
                                            Helpers.SubmitWorkOrder(__instance, workOrder);
                                            Core.tempMechLabQueue.Remove(workOrder);
                                        }
                                    },
                                    "Yes",
                                    delegate
                                    {
                                        foreach (WorkOrderEntry_MechLab workOrder in Core.tempMechLabQueue.ToList())
                                        {
                                            Core.tempMechLabQueue.Remove(workOrder);
                                        }
                                    },
                                    "No"
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Core.tempMechLabQueue.Clear();
                Error(ex);
            }
        }
    }

    
}