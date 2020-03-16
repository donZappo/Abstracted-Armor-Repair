using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BattleTech;
using BattleTech.UI;
using Harmony;
using Localize;
using UnityEngine;
using HBS.Extensions;
using UnityEngine.UI;
using HBS;
using BattleTech.UI.Tooltips;
using TMPro;
using DG.Tweening;
using BattleTech.UI.TMProWrapper;

namespace AbstractedArmorRepair
{
    class Repair_ReArm
    {
        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class Add_Fatigue_To_Pilots_Prefix
        {
            public static void Postfix(AAR_UnitStatusWidget __instance, SimGameState ___simState)
            {
                if (!Core.Settings.RepairRearm)
                    return;

                var sim = UnityGameInstance.BattleTechGame.Simulation;

                UnitResult unitResult = Traverse.Create(__instance).Field("UnitData").GetValue<UnitResult>();
                float armorDamage = unitResult.mech.MechDefCurrentArmor / unitResult.mech.MechDefAssignedArmor;
                string armorDamageTag = "XLRPArmor_" + armorDamage.ToString();

                if (!unitResult.mech.MechTags.Contains("XLRP_R&R") && armorDamage < 1)
                {
                    unitResult.mech.MechTags.Add("XLRP_R&R");
                    unitResult.mech.MechTags.Add(armorDamageTag);
                }
                else if (armorDamage < 1)
                {
                    unitResult.mech.MechTags.Where(tag => tag.StartsWith("XLRPArmor")).Do(x => unitResult.mech.MechTags.Remove(x));
                    unitResult.mech.MechTags.Add(armorDamageTag);
                }
            }
        }

        [HarmonyPatch(typeof(MechValidationRules), "GetMechFieldableWarnings")]
        public static class LanceConfiguratorPanel_ContinueConfirmedClicked_Patch
        {
            public static void Postfix(ref List<Localize.Text> __result, MechDef mechDef)
            {
                if (!Core.Settings.RepairRearm)
                    return;

                if (mechDef != null && mechDef.MechTags.Contains("XLRP_R&R"))
                {
                    Localize.Text RR_Text = new Localize.Text("REPAIR & REARM: 'Mech has armor damage that needs repair", Array.Empty<object>());
                    __result.Add(RR_Text);
                }
            }
        }

        [HarmonyPatch(typeof(MechLabMechInfoWidget), "SetData")]
        public static class MechLabLocationWidget_SetData_Patch
        {
            static void Postfix(MechLabMechInfoWidget __instance, MechDef mechDef)
            {
                if (!Core.Settings.RepairRearm)
                    return;

                if (mechDef != null && mechDef.MechTags.Contains("XLRP_R&R"))
                {
                    Localize.Text RR_Text = new Localize.Text("REPAIR & REARM: 'Mech has armor damage that needs repair", Array.Empty<object>());
                    List<Localize.Text> RR_Label = new List<Localize.Text>();
                    RR_Label.Add(RR_Text);
                    __instance.ToggleGenericAlert(RR_Label);
                }
            }
        }

        [HarmonyPatch(typeof(MechLabPanel), "OnMaxArmor")]
        public static class MechLabPanel_OnMaxArmor_Patch
        {
            public static bool Prefix(MechLabPanel __instance)
            {
                if (!Core.Settings.RepairRearm)
                    return true;

                if (__instance.activeMechDef != null && __instance.activeMechDef.MechTags.Contains("XLRP_R&R"))
                {
                    RepairArmor(__instance);
                    __instance.ValidateLoadout(false);
                    return false;
                }
                else
                    return true;
            }
        }

        [HarmonyPatch(typeof(MechLabPanel), "OnRevertMech")]
        public static class MechLabPanel_OnRevertMech_Patch
        {
            public static void Prefix(MechLabPanel __instance)
            {
                if (!Core.Settings.RepairRearm)
                    return;

                if (__instance.activeMechDef.MechTags.Contains("XLRP_Armor_Repairing"))
                {
                    __instance.Modified = true;
                    __instance.activeMechDef.MechTags.Remove("XLRP_Armor_Repairing");
                }
            }
        }


        [HarmonyPatch(typeof(MechBayPanel), "OnRepairMech")]
        public static class MechBayPanel_OnRepairMech_Patch
        {
            public static bool Prefix(MechBayPanel __instance, MechBayMechUnitElement mechElement)
            {
                if (!Core.Settings.RepairRearm)
                    return true;

                var sim = UnityGameInstance.BattleTechGame.Simulation;
                MechDef mechDef = mechElement.MechDef;
                WorkOrderEntry_MechLab workOrderEntry_MechLab = __instance.Sim.GetWorkOrderEntryForMech(mechDef);
                bool flag = false;
                for (int i = 0; i < mechDef.Inventory.Length; i++)
                {
                    MechComponentRef mechComponentRef = mechDef.Inventory[i];
                    if (mechComponentRef.DamageLevel != ComponentDamageLevel.Functional && mechComponentRef.DamageLevel
                        != ComponentDamageLevel.Installing && !MechValidationRules.MechComponentUnderMaintenance(mechComponentRef, MechValidationLevel.MechLab, workOrderEntry_MechLab))
                    {
                        flag = true;
                        break;
                    }
                }
                if (!mechDef.IsDamaged && !flag)
                {
                    return false;
                }
                List<ChassisLocations> list = new List<ChassisLocations>();
                __instance.pendingWorkOrderNew = false;
                __instance.pendingWorkOrderEntriesToAdd.Clear();
                if (workOrderEntry_MechLab == null)
                {
                    workOrderEntry_MechLab = new WorkOrderEntry_MechLab(WorkOrderType.MechLabGeneric, "MechLab-BaseWorkOrder", Strings.T("Modify 'Mech - {0}", new object[]
                    {
                    mechDef.Description.Name
                    }), mechDef.GUID, 0, Strings.T(__instance.Sim.Constants.Story.GeneralMechWorkOrderCompletedText, new object[]
                    {
                    mechDef.Description.Name
                    }));
                    workOrderEntry_MechLab.SetMechDef(mechDef);
                    __instance.pendingWorkOrderNew = true;
                }
                __instance.pendingWorkOrder = workOrderEntry_MechLab;
                if (!MechValidationRules.MechStructureUnderMaintenance(ChassisLocations.Head, MechValidationLevel.MechLab, workOrderEntry_MechLab) && mechDef.Head.CurrentInternalStructure < mechDef.Chassis.Head.InternalStructure)
                {
                    list.Add(ChassisLocations.Head);
                }
                if (!MechValidationRules.MechStructureUnderMaintenance(ChassisLocations.CenterTorso, MechValidationLevel.MechLab, workOrderEntry_MechLab) && mechDef.CenterTorso.CurrentInternalStructure < mechDef.Chassis.CenterTorso.InternalStructure)
                {
                    list.Add(ChassisLocations.CenterTorso);
                }
                if (!MechValidationRules.MechStructureUnderMaintenance(ChassisLocations.LeftTorso, MechValidationLevel.MechLab, workOrderEntry_MechLab) && mechDef.LeftTorso.CurrentInternalStructure < mechDef.Chassis.LeftTorso.InternalStructure)
                {
                    list.Add(ChassisLocations.LeftTorso);
                }
                if (!MechValidationRules.MechStructureUnderMaintenance(ChassisLocations.RightTorso, MechValidationLevel.MechLab, workOrderEntry_MechLab) && mechDef.RightTorso.CurrentInternalStructure < mechDef.Chassis.RightTorso.InternalStructure)
                {
                    list.Add(ChassisLocations.RightTorso);
                }
                if (!MechValidationRules.MechStructureUnderMaintenance(ChassisLocations.LeftLeg, MechValidationLevel.MechLab, workOrderEntry_MechLab) && mechDef.LeftLeg.CurrentInternalStructure < mechDef.Chassis.LeftLeg.InternalStructure)
                {
                    list.Add(ChassisLocations.LeftLeg);
                }
                if (!MechValidationRules.MechStructureUnderMaintenance(ChassisLocations.RightLeg, MechValidationLevel.MechLab, workOrderEntry_MechLab) && mechDef.RightLeg.CurrentInternalStructure < mechDef.Chassis.RightLeg.InternalStructure)
                {
                    list.Add(ChassisLocations.RightLeg);
                }
                if (!MechValidationRules.MechStructureUnderMaintenance(ChassisLocations.LeftArm, MechValidationLevel.MechLab, workOrderEntry_MechLab) && mechDef.LeftArm.CurrentInternalStructure < mechDef.Chassis.LeftArm.InternalStructure)
                {
                    list.Add(ChassisLocations.LeftArm);
                }
                if (!MechValidationRules.MechStructureUnderMaintenance(ChassisLocations.RightArm, MechValidationLevel.MechLab, workOrderEntry_MechLab) && mechDef.RightArm.CurrentInternalStructure < mechDef.Chassis.RightArm.InternalStructure)
                {
                    list.Add(ChassisLocations.RightArm);
                }
                if (list.Count < 1 && !flag)
                {
                    GenericPopupBuilder.Create("Repair Already Ordered", string.Format("A repair order has already been queued for " +
                        "{0}", mechDef.Name)).AddFader(new UIColorRef?(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill), 0f, true).Render();
                    __instance.OnRepairAllCancelled();
                    return false;
                }
                int num = 0;
                int num2 = 0;
                for (int j = 0; j < list.Count; j++)
                {
                    LocationDef chassisLocationDef = mechDef.GetChassisLocationDef(list[j]);
                    LocationLoadoutDef locationLoadoutDef = mechDef.GetLocationLoadoutDef(list[j]);
                    int structureCount = Mathf.RoundToInt(Mathf.Max(0f, chassisLocationDef.InternalStructure - locationLoadoutDef.CurrentInternalStructure));
                    WorkOrderEntry_RepairMechStructure workOrderEntry_RepairMechStructure = __instance.Sim.CreateMechRepairWorkOrder(mechDef.GUID, list[j], structureCount);
                    __instance.pendingWorkOrderEntriesToAdd.Add(workOrderEntry_RepairMechStructure);
                    num += workOrderEntry_RepairMechStructure.GetCost();
                    num2 += workOrderEntry_RepairMechStructure.GetCBillCost();
                }
                StringBuilder stringBuilder = new StringBuilder();
                int num3 = 0;
                for (int k = 0; k < mechDef.Inventory.Length; k++)
                {
                    MechComponentRef mechComponentRef2 = mechDef.Inventory[k];
                    if (string.IsNullOrEmpty(mechComponentRef2.SimGameUID))
                    {
                        mechComponentRef2.SetSimGameUID(__instance.Sim.GenerateSimGameUID());
                    }
                    if (mechComponentRef2.DamageLevel == ComponentDamageLevel.Destroyed)
                    {
                        if (num3 < 1)
                        {
                            stringBuilder.Append("\n\nThe following components have been Destroyed. If you continue with the Repair, " +
                                "replacement Components will NOT be installed. If you want to replace them with identical or " +
                                "different Components, you must Refit the 'Mech.\n\n");
                        }
                        if (num3 < 5)
                        {
                            stringBuilder.Append(mechComponentRef2.MountedLocation.ToString());
                            stringBuilder.Append(": ");
                            stringBuilder.Append(mechComponentRef2.Def.Description.Name);
                            stringBuilder.Append("\n");
                        }
                        num3++;
                        WorkOrderEntry_InstallComponent workOrderEntry_InstallComponent = sim.CreateComponentInstallWorkOrder(__instance.selectedMech.MechDef.GUID,
                            mechComponentRef2, ChassisLocations.None, mechComponentRef2.MountedLocation);
                        __instance.pendingWorkOrderEntriesToAdd.Insert(0, workOrderEntry_InstallComponent);
                        num += workOrderEntry_InstallComponent.GetCost();
                        num2 += workOrderEntry_InstallComponent.GetCBillCost();
                    }
                    else if (mechComponentRef2.DamageLevel != ComponentDamageLevel.Functional && mechComponentRef2.DamageLevel != ComponentDamageLevel.Installing)
                    {
                        WorkOrderEntry_RepairComponent workOrderEntry_RepairComponent = __instance.Sim.CreateComponentRepairWorkOrder(mechComponentRef2, true);
                        __instance.pendingWorkOrderEntriesToAdd.Add(workOrderEntry_RepairComponent);
                        num += workOrderEntry_RepairComponent.GetCost();
                        num2 += workOrderEntry_RepairComponent.GetCBillCost();
                    }
                }

                float armorLoss = 1;
                bool armorTag = false;
                foreach (var tag in mechDef.MechTags)
                {
                    if (tag.StartsWith($"XLRPArmor"))
                    {
                        armorTag = true;
                        string[] parsedString = tag.Split('_');
                        armorLoss = float.Parse(parsedString[1]);
                    }
                }
                if (armorTag)
                {
                    if (!mechDef.MechTags.Contains("XLRP_Armor_Repairing"))
                        mechDef.MechTags.Add("XLRP_Armor_Repairing");
                    int brokenArmor = (int)((1 - armorLoss) * mechDef.MechDefAssignedArmor);
                    int frontArmor = (int)(mechDef.MechDefAssignedArmor - mechDef.CenterTorso.AssignedRearArmor -
                        mechDef.LeftTorso.AssignedRearArmor - mechDef.RightTorso.AssignedRearArmor);
                    int rearArmor = (int)(mechDef.CenterTorso.AssignedRearArmor +
                        mechDef.LeftTorso.AssignedRearArmor + mechDef.RightTorso.AssignedRearArmor);
                    WorkOrderEntry_ModifyMechArmor subEntry = sim.CreateMechArmorModifyWorkOrder(__instance.selectedMech.MechDef.GUID,
                        ChassisLocations.All, brokenArmor, frontArmor, rearArmor);

                    __instance.pendingWorkOrderEntriesToAdd.Add(subEntry);
                    num += subEntry.GetCost();
                    num2 += subEntry.GetCBillCost();
                }

                num = Mathf.Max(1, Mathf.CeilToInt((float)num / (float)__instance.Sim.MechTechSkill));
                if (num3 > 5)
                {
                    stringBuilder.Append(Strings.T("...\nAnd {0} additional destroyed components.\n", new object[]
                    {
                    num3 - 5
                    }));
                }
                string body = Strings.T("Repairing {0} will cost {1:n0} C-Bills and take {2} Days.{3}\n\nProceed?", new object[]
                {
                mechDef.Name,
                num2,
                num,
                stringBuilder.ToString()
                });
                GenericPopupBuilder.Create("Repair 'Mech?", body).AddButton("Cancel", new Action(__instance.OnRepairAllCancelled), true, null).
                    AddButton("Repair", new Action(__instance.OnRepairAllAccepted), true, null).AddFader(new UIColorRef?(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill), 0f, true).Render();
                return false;

            }
        }

        [HarmonyPatch(typeof(MechLabPanel), "OnRepairAll")]
        public static class MechLabPanel_OnRepairAll_Patch
        {
            public static bool Prefix(MechLabPanel __instance)
            {
                if (!Core.Settings.RepairRearm)
                    return true;

                if (!__instance.Initialized || !__instance.IsSimGame)
                {
                    return false;
                }
                if (Traverse.Create(__instance).Field("dragItem").GetValue<MechLabItemSlotElement>() != null)
                {
                    return false;
                }
                __instance.headWidget.RepairAll(true, true);
                __instance.centerTorsoWidget.RepairAll(true, true);
                __instance.leftTorsoWidget.RepairAll(true, true);
                __instance.rightTorsoWidget.RepairAll(true, true);
                __instance.leftArmWidget.RepairAll(true, true);
                __instance.rightArmWidget.RepairAll(true, true);
                __instance.leftLegWidget.RepairAll(true, true);
                __instance.rightLegWidget.RepairAll(true, true);
                RepairArmor(__instance);
                __instance.ValidateLoadout(false);
                return false;
            }
        }

        [HarmonyPatch(typeof(SimGameState), "ML_ModifyArmor")]
        public static class SimGameState_ML_ModifyArmor_Patch
        {
            public static void Prefix(SimGameState __instance, WorkOrderEntry_ModifyMechArmor order)
            {
                if (!Core.Settings.RepairRearm || order.MechLabParent.MechID == null)
                    return;

                MechDef mechDef = __instance.GetMechByID(order.MechLabParent.MechID);
                if (mechDef.MechTags.Contains("XLRP_Armor_Repairing") || order.Location == ChassisLocations.All)
                    order.SetMechLabComplete(true);
            }
        }

        [HarmonyPatch(typeof(SimGameState), "CompleteWorkOrder")]
        public static class SimGameState_CompleteWorkOrder_Patch
        {
            public static void Postfix(SimGameState __instance, WorkOrderEntry entry)
            {
                if (!Core.Settings.RepairRearm)
                    return;

                if (entry.Type == WorkOrderType.MechLabModifyArmor)
                {
                    WorkOrderEntry_MechLab workOrderEntry_MechLab = entry as WorkOrderEntry_MechLab;
                    MechDef mechBayID = __instance.GetMechByID(workOrderEntry_MechLab.MechLabParent.MechID);
                    if (mechBayID.MechTags.Contains("XLRP_Armor_Repairing"))
                    {
                        mechBayID.MechTags.Remove("XLRP_Armor_Repairing");
                        mechBayID.MechTags.Remove("XLRP_R&R");
                        mechBayID.MechTags.Where(tag => tag.StartsWith("XLRPArmor")).Do(x => mechBayID.MechTags.Remove(x));
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SimGameState), "CancelWorkOrder")]
        public static class SimGameState_CancelWorkOrder_Patch
        {
            public static void Postfix(SimGameState __instance, WorkOrderEntry entry)
            {
                if (!Core.Settings.RepairRearm)
                    return;

                if (entry.Type == WorkOrderType.MechLabModifyArmor)
                {
                    WorkOrderEntry_MechLab workOrderEntry_MechLab = entry as WorkOrderEntry_MechLab;
                    MechDef mechBayID = __instance.GetMechByID(workOrderEntry_MechLab.MechID);
                    if (mechBayID.MechTags.Contains("XLRP_Armor_Repairing"))
                        mechBayID.MechTags.Remove("XLRP_Armor_Repairing");
                }
            }
        }

        [HarmonyPatch(typeof(MechBayPanel), "OnRepairAllCancelled")]
        public static class MEchBayPanel_OnRepairAllCancelled_Patch
        {
            public static void Postfix(MechBayPanel __instance)
            {
                if (!Core.Settings.RepairRearm)
                    return;

                if (__instance.selectedMech.MechDef.MechTags.Contains("XLRP_Armor_Repairing"))
                    __instance.selectedMech.MechDef.MechTags.Remove("XLRP_Armor_Repairing");
            }
        }

        [HarmonyPatch(typeof(MechLabPanel), "ConfirmRevertMech")]
        public static class MechLabPanel_ConfirmRevertMech_Patch
        {
            public static void Postfix(MechLabPanel __instance)
            {
                if (!Core.Settings.RepairRearm)
                    return;

                if (__instance.activeMechDef.MechTags.Contains("XLRP_Armor_Repairing"))
                    __instance.activeMechDef.MechTags.Remove("XLRP_Armor_Repairing");
            }
        }

        [HarmonyPatch(typeof(TooltipPrefab_Mech), "SetData")]
        public static class TooltipPrefab_Mech_SetData_Patch
        {
            // Token: 0x0600001F RID: 31 RVA: 0x00002C48 File Offset: 0x00000E48
            public static void Postfix(object data, TextMeshProUGUI ___DetailsField)
            {
                if (!Core.Settings.RepairRearm)
                    return;

                try
                {
                    MechDef mechDef;
                    bool flag = (mechDef = (data as MechDef)) != null;
                    if (flag)
                    {
                        string armorLossString = "";
                        if (mechDef.MechTags.Contains("XLRP_R&R"))
                        {
                            float armorLoss = 1;
                            foreach (var tag in mechDef.MechTags)
                            {
                                if (tag.StartsWith($"XLRPArmor"))
                                {
                                    string[] parsedString = tag.Split('_');
                                    armorLoss = float.Parse(parsedString[1]);
                                    armorLoss = 100 * (1 - armorLoss);
                                }
                            }
                            armorLossString = "<color=#e40000><b>\n\n'Mech will drop with " +
                                armorLoss.ToString("0.0#") + "%" + " less armor until repaired.</b></color>";
                        }

                        ___DetailsField.SetText(___DetailsField.text + armorLossString);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
        }

        [HarmonyPatch(typeof(MechLabPanel), "LoadMech")]
        public static class MechLabPanel_LoadMech_Patch
        {
            public static void Postfix(MechLabPanel __instance)
            {
                if (!Core.Settings.RepairRearm)
                    return;

                var white = new Color(215f, 215f, 215f, 255f);
                var white2 = new Color(255f, 255f, 255f, 255f);
                var brown = new Color(137 / 255f, 61 / 255f, 18 / 255f, 1);


                if (__instance.activeMechDef.MechTags.Contains("XLRP_R&R"))
                {
                    var fillArmorGO = GameObject.Find("uixPrfBttn_BASE_iconActionButton-MANAGED-fillArmor");
                    var button = GameObject.Find("uixPrfBttn_BASE_iconActionButton-MANAGED-fillArmor");
                    button.FindFirstChildNamed("iconBttn_innerBorder").SetActive(false);
                    var background = button.FindFirstChildNamed("iconBttn_bg").GetComponent<Image>();
                    background.GetComponent<Image>().color = brown;

                    var tweenComponents = button.FindFirstChildNamed("iconBttn_bg").GetComponents<DOTweenAnimation>();
                    tweenComponents[0].endValueColor = brown;
                    tweenComponents[2].endValueColor = brown;

                    var armorText = button.FindFirstChildNamed("iconBtn_highlightLabel").GetComponent<LocalizableText>();
                    armorText.text = "Repair Armor";

                    float armorLoss = 1;
                    foreach (var tag in __instance.activeMechDef.MechTags)
                    {
                        if (tag.StartsWith($"XLRPArmor"))
                        {
                            string[] parsedString = tag.Split('_');
                            armorLoss = 1 - float.Parse(parsedString[1]);
                        }
                    }
                    var armorLossString = armorLoss.ToString("0.0#") + "% Armor Damaged";

                    HBSTooltip tooltip;
                    if (fillArmorGO.GetComponent<HBSTooltip>())
                    {
                        tooltip = fillArmorGO.GetComponent<HBSTooltip>();
                        tooltip.enabled = true;
                    }
                    else
                        tooltip = fillArmorGO.AddComponent<HBSTooltip>();

                    tooltip.SetDefaultStateData(new HBSTooltipStateData
                    {
                        mode = HBSTooltipStateData.Mode.StringValue,
                        stringValue = armorLossString
                    });
                }
                else
                {
                    var fillArmorGO = GameObject.Find("uixPrfBttn_BASE_iconActionButton-MANAGED-fillArmor");
                    var button = GameObject.Find("uixPrfBttn_BASE_iconActionButton-MANAGED-fillArmor");
                    button.FindFirstChildNamed("iconBttn_innerBorder").SetActive(true);
                    var background = button.FindFirstChildNamed("iconBttn_bg").GetComponent<Image>();
                    background.GetComponent<Image>().color = white;

                    var tweenComponents = button.FindFirstChildNamed("iconBttn_bg").GetComponents<DOTweenAnimation>();
                    tweenComponents[0].endValueColor = white;
                    tweenComponents[2].endValueColor = white2;

                    var armorText = button.FindFirstChildNamed("iconBtn_highlightLabel").GetComponent<LocalizableText>();
                    armorText.text = "Max Armor";

                    if (fillArmorGO.GetComponent<HBSTooltip>())
                        fillArmorGO.GetComponent<HBSTooltip>().enabled = false;
                }
            }
        }

        public static void RepairArmor(MechLabPanel mechLabPanel)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var mechDef = mechLabPanel.activeMechDef;
            float armorLoss = 1;
            bool armorRepair = false;

            foreach (var tag in mechDef.MechTags)
            {
                if (tag.StartsWith($"XLRPArmor"))
                {
                    string[] parsedString = tag.Split('_');
                    armorLoss = float.Parse(parsedString[1]);
                    armorRepair = true;
                }
            }
            if (!armorRepair)
                return;

            if (armorLoss != 1 && !mechDef.MechTags.Contains("XLRP_Armor_Repairing"))
                mechDef.MechTags.Add("XLRP_Armor_Repairing");

            int brokenArmor = (int)((1 - armorLoss) * mechDef.MechDefAssignedArmor);
            int frontArmor = (int)(mechDef.MechDefAssignedArmor - mechDef.CenterTorso.AssignedRearArmor -
                mechDef.LeftTorso.AssignedRearArmor - mechDef.RightTorso.AssignedRearArmor);
            int rearArmor = (int)(mechDef.CenterTorso.AssignedRearArmor +
                mechDef.LeftTorso.AssignedRearArmor + mechDef.RightTorso.AssignedRearArmor);
            var foo = sim.GetWorkOrderEntryForMech(mechDef);
            WorkOrderEntry_ModifyMechArmor subEntry = sim.CreateMechArmorModifyWorkOrder(mechLabPanel.activeMechDef.GUID,
                ChassisLocations.All, brokenArmor, frontArmor, rearArmor);
            mechLabPanel.baseWorkOrder.AddSubEntry(subEntry);
        }

        [HarmonyPatch(typeof(Team), "CollectUnitBaseline")]
        public static class Team_CollectUnitBaseline_Patch
        {
            public static void Postfix(Team __instance)
            {
                if (__instance.IsLocalPlayer)
                { 
                    foreach (var actor in __instance.units)
                    {
                        if (!(actor is Mech mech))
                            continue;

                        bool correctArmor = false;
                        var tags = actor.GetTags();
                        float armorLoss = 1;
                        foreach (var tag in tags)
                        {
                            if (tag.StartsWith($"XLRPArmor"))
                            {
                                string[] parsedString = tag.Split('_');
                                armorLoss = float.Parse(parsedString[1]);
                                correctArmor = true;
                            }
                        }
                        if (correctArmor)
                        {
                            var HeadArmor = actor.StatCollection.GetValue<float>("Head.Armor");
                            HeadArmor *= armorLoss;
                            actor.StatCollection.Set<float>("Head.Armor", HeadArmor);

                            var LeftArmArmor = actor.StatCollection.GetValue<float>("LeftArm.Armor");
                            LeftArmArmor *= armorLoss;
                            actor.StatCollection.Set<float>("LeftArm.Armor", LeftArmArmor);

                            var LeftTorsoArmor = actor.StatCollection.GetValue<float>("LeftTorso.Armor");
                            LeftTorsoArmor *= armorLoss;
                            actor.StatCollection.Set<float>("LeftTorso.Armor", LeftTorsoArmor);

                            var CenterTorsoArmor = actor.StatCollection.GetValue<float>("CenterTorso.Armor");
                            CenterTorsoArmor *= armorLoss;
                            actor.StatCollection.Set<float>("CenterTorso.Armor", CenterTorsoArmor);

                            var RightTorsoArmor = actor.StatCollection.GetValue<float>("RightTorso.Armor");
                            RightTorsoArmor *= armorLoss;
                            actor.StatCollection.Set<float>("RightTorso.Armor", RightTorsoArmor);

                            var RightArmArmor = actor.StatCollection.GetValue<float>("RightArm.Armor");
                            RightArmArmor *= armorLoss;
                            actor.StatCollection.Set<float>("RightArm.Armor", RightArmArmor);

                            var LeftLegArmor = actor.StatCollection.GetValue<float>("LeftLeg.Armor");
                            LeftLegArmor *= armorLoss;
                            actor.StatCollection.Set<float>("LeftLeg.Armor", LeftLegArmor);

                            var RightLegArmor = actor.StatCollection.GetValue<float>("RightLeg.Armor");
                            RightLegArmor *= armorLoss;
                            actor.StatCollection.Set<float>("RightLeg.Armor", RightLegArmor);

                            var LeftTorsoRearArmor = actor.StatCollection.GetValue<float>("LeftTorso.RearArmor");
                            LeftTorsoRearArmor *= armorLoss;
                            actor.StatCollection.Set<float>("LeftTorso.RearArmor", LeftTorsoRearArmor);

                            var CenterTorsoRearArmor = actor.StatCollection.GetValue<float>("CenterTorso.RearArmor");
                            CenterTorsoRearArmor *= armorLoss;
                            actor.StatCollection.Set<float>("CenterTorso.RearArmor", CenterTorsoRearArmor);

                            var RightTorsoRearArmor = actor.StatCollection.GetValue<float>("RightTorso.RearArmor");
                            RightTorsoRearArmor *= armorLoss;
                            actor.StatCollection.Set<float>("RightTorso.RearArmor", RightTorsoRearArmor);
                        }
                    }
                }
            }

        }
    }
}
