using HarmonyLib;
using ConsoleLib.Console;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Reflection;
using XRL;
using XRL.World;
using static XRL.UI.Options;
using UnityEngine;
using System.Numerics;

namespace CombatVisualsImproved
{
    [HarmonyPatch]
    [HasOptionFlagUpdate]
    class FloatingTextImproved
    {
        // Options
        public static bool OptionCondenseDamage;
        public static bool OptionRecolorCombatText;
        public static UnityEngine.Color OptionIncomingColor; //Black,Blue,Cyan,Magenta,Yellow,White,Orange,Red,Green
        public static UnityEngine.Color OptionOutgoingColor; //Black,Blue,Cyan,Magenta,Yellow,White,Orange,Red,Green
        public static bool OptionRandomTextDirection;
        public static bool OptionReduceAnimations;
        public static int OptionFloatingTextSize;
        public static int OptionFloatingTextSpeed;

        public static Dictionary<int, CombatJuiceEntry> damageByID = new Dictionary<int, CombatJuiceEntry>();
        public static Dictionary<ex3DSprite2, CombatJuiceEntry> punchByVec = new Dictionary<ex3DSprite2, CombatJuiceEntry>();
        public static Dictionary<UnityEngine.Vector3, CombatJuiceEntry> prefabByVec = new Dictionary<UnityEngine.Vector3, CombatJuiceEntry>();
        public static List<CombatJuiceEntry> textEntries = new List<CombatJuiceEntry>();
        public static List<CombatJuiceEntry> miscEntries = new List<CombatJuiceEntry>();

        [OptionFlagUpdate]
        public static void UpdateFlags()
        {
            OptionCondenseDamage = GetOptionBool("OptionCVICondenseDamage");
            OptionRecolorCombatText = GetOptionBool("OptionCVIRecolorText");
            OptionIncomingColor = OptionColorToUnityColor(GetOption("OptionCVIPlayerSourceColor", "Red"));
            OptionOutgoingColor = OptionColorToUnityColor(GetOption("OptionCVIOtherSourceColor", "Red"));
            OptionRandomTextDirection = GetOptionBool("OptionCVIRandomTextDirection");
            OptionReduceAnimations = GetOptionBool("OptionCVIReduceMeleeAnimations");
            OptionFloatingTextSize = Convert.ToInt32(GetOption("OptionCVITextSize", "10"));
            OptionFloatingTextSpeed = Convert.ToInt32(GetOption("OptionCVITextSpeed", "10"));
        }

        [HarmonyPatch(typeof(CombatJuiceEntryText))]
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] {typeof(UnityEngine.Vector3), typeof(UnityEngine.Vector3), typeof(float), typeof(string), typeof(Color), typeof(XRL.World.GameObject), typeof(float)})]
        static void Postfix(ref CombatJuiceEntryText __instance, ref float ___floatTime, ref float ___scale, ref UnityEngine.Vector3 ___endPosition, ref UnityEngine.Vector3 ___startPosition)
        {
            //Default is 1.5f
            ___floatTime = OptionFloatingTextSpeed * 0.1f;
            ___scale = ___scale * OptionFloatingTextSize * 0.1f;
            if (OptionRecolorCombatText && isDamageString(__instance.text))
            {
                if (__instance.emittingObject.IsPlayer())
                {
                    __instance.color = OptionIncomingColor;
                } else
                {
                    __instance.color = OptionOutgoingColor;
                }
            }
            if (OptionRandomTextDirection)
            {
                ___endPosition += new UnityEngine.Vector3(UnityEngine.Random.Range(-25f,25f), 0f, 0f);
                ___startPosition +=  new UnityEngine.Vector3(0, UnityEngine.Random.Range(-12f,12f), 0f);
            }
        }

        //TODO: Change this to transpile method calls only from CombatJuiceManager instead of changing for all calls.
        [HarmonyPatch(typeof(CombatJuiceEntryText), nameof(CombatJuiceEntryText.canStart))]
        static void Postfix(ref bool __result) {
            __result = true;
        }

        static int convertDamageStringToNumber(string damage)
        {
            return Int32.Parse(damage.TrimStart('-'));
        }

        static bool isDamageString(string text)
        {
            return text.StartsWith('-') && text.Any(char.IsDigit);
        }

        [HarmonyPatch(typeof(CombatJuiceManager), nameof(CombatJuiceManager.update))]
        static void Prefix(ref CombatJuiceManager ___instance)
        {
            damageByID.Clear();
            punchByVec.Clear();
            prefabByVec.Clear();
            textEntries.Clear();
            miscEntries.Clear();

            lock (___instance?.queue);
            while (___instance?.queue?.Count > 0)
            {
                CombatJuiceEntry entry = ___instance.queue.Dequeue();
                if (entry?.GetType() == typeof(CombatJuiceEntryText))
                {
                    CombatJuiceEntryText newEntryText = (CombatJuiceEntryText)entry;
                    if (isDamageString(newEntryText.text) && OptionCondenseDamage)
                    {
                        int id = newEntryText.emittingObject._BaseID;
                        if (!damageByID.TryAdd(id, entry))
                        {
                            CombatJuiceEntryText oldEntryText = (CombatJuiceEntryText)damageByID[id];
                            int total = convertDamageStringToNumber(oldEntryText.text);
                            int newDamage = convertDamageStringToNumber(newEntryText.text);
                            total += newDamage;
                            oldEntryText.text = "-" + total;
                        } 
                    } else
                    {
                        textEntries.Add(entry);
                    }
                    
                } else if (entry.GetType() == typeof(CombatJuiceEntryPunch))
                {
                    CombatJuiceEntryPunch newPunch = (CombatJuiceEntryPunch)entry;
                    if (OptionReduceAnimations)
                    {
                        ex3DSprite2 target = newPunch.target;
                        punchByVec.TryAdd(target, entry); 
                    } else
                    {
                        miscEntries.Add(entry);
                    }

                } else if (entry.GetType() == typeof(CombatJuiceEntryPrefabAnimation))
                {
                    CombatJuiceEntryPrefabAnimation newPrefab = (CombatJuiceEntryPrefabAnimation)entry;
                    if (newPrefab.animation.Contains("CombatJuice") && OptionReduceAnimations)
                    {
                        prefabByVec.TryAdd(newPrefab.location, entry);
                    } else
                    {
                        miscEntries.Add(entry);
                    }
                } else
                {
                    miscEntries.Add(entry);
                }
            }

            foreach (KeyValuePair<int,CombatJuiceEntry> kvp in damageByID)
            {
                ___instance.queue.Enqueue(kvp.Value);
            }
            for (int i = 0; i < textEntries.Count; i ++)
            {
                ___instance.queue.Enqueue(textEntries[i]);
            }
            foreach (KeyValuePair<ex3DSprite2,CombatJuiceEntry> kvp in punchByVec)
            {
                ___instance.queue.Enqueue(kvp.Value);
            }
            foreach (KeyValuePair<UnityEngine.Vector3,CombatJuiceEntry> kvp in prefabByVec)
            {
                ___instance.queue.Enqueue(kvp.Value);
            }
            for (int i = 0; i < miscEntries.Count; i ++)
            {
                ___instance.queue.Enqueue(miscEntries[i]);
            }
        }
        private static UnityEngine.Color OptionColorToUnityColor(string selectedColor)
        {
            char colorCode;
            switch (selectedColor)
            {
                case "Black":
                    colorCode = 'K';
                    break;
                case "Blue":
                    colorCode = 'B';
                    break;
                case "Cyan":
                    colorCode = 'C';
                    break;
                case "Magenta":
                    colorCode = 'M';
                    break;
                case "Yellow":
                    colorCode = 'W';
                    break;
                case "White":
                    colorCode = 'Y';
                    break;
                case "Orange":
                    colorCode = 'O';
                    break;
                case "Red":
                    colorCode = 'R';
                    break;
                case "Green":
                    colorCode = 'G';
                    break;
                default:
                    colorCode = 'R';
                    break;
            }
            return ConsoleLib.Console.ColorUtility.colorFromChar(colorCode);
        }
    }
}