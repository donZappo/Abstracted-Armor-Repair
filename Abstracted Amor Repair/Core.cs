﻿using System;
using System.Collections;
using System.Reflection;
using Harmony;
using Newtonsoft.Json;
using static AbstractedArmorRepair.Logger;
using BattleTech;
using BattleTech.UI;
using System.Collections.Generic;

namespace AbstractedArmorRepair
{
    public static class Core
    {
       
        #region Init

        public static void Init(string modDir, string settings)
        {
            var harmony = HarmonyInstance.Create("AbstractedArmorRepari.donZappo");
            // read settings
            try
            {
                Settings = JsonConvert.DeserializeObject<ModSettings>(settings);
                Settings.modDirectory = modDir;
            }
            catch (Exception)
            {
                Settings = new ModSettings();
            }

            // blank the logfile
            Clear();
            PrintObjectFields(Settings, "Settings");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        // logs out all the settings and their values at runtime
        internal static void PrintObjectFields(object obj, string name)
        {
            LogDebug($"[START {name}]");

            var settingsFields = typeof(ModSettings)
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (var field in settingsFields)
            {
                if (field.GetValue(obj) is IEnumerable &&
                    !(field.GetValue(obj) is string))
                {
                    LogDebug(field.Name);
                    foreach (var item in (IEnumerable)field.GetValue(obj))
                    {
                        LogDebug("\t" + item);
                    }
                }
                else
                {
                    LogDebug($"{field.Name,-30}: {field.GetValue(obj)}");
                }
            }

            LogDebug($"[END {name}]");
        }

        #endregion

        internal static ModSettings Settings;
        public static List<MechDef> CombatMechs = new List<MechDef>();
        public static List<WorkOrderEntry_MechLab> tempMechLabQueue = new List<WorkOrderEntry_MechLab>();
    }
}