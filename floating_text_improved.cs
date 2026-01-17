using HarmonyLib;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Reflection;
using XRL;
using XRL.World;
using UnityEngine;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;

[HarmonyPatch]
[HarmonyDebug]
class FloatingTextImproved
{
    [HarmonyPatch(typeof(CombatJuiceEntryText))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[] {typeof(UnityEngine.Vector3), typeof(UnityEngine.Vector3), typeof(float), typeof(string), typeof(Color), typeof(XRL.World.GameObject), typeof(float)})]
    static void Postfix(CombatJuiceEntryText __instance, ref float ___floatTime, ref float ___scale, ref UnityEngine.Vector3 ___endPosition)
    {
        //TODO: Map these to options
        ___floatTime = 0.75f;
        //___scale = .75f;
        ___endPosition = ___endPosition + new UnityEngine.Vector3(UnityEngine.Random.Range(-25f,25f), 0f, 0f);
    }

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
        Dictionary<int, CombatJuiceEntry> damageByID = new Dictionary<int, CombatJuiceEntry>();
        Dictionary<ex3DSprite2, CombatJuiceEntry> punchByVec = new Dictionary<ex3DSprite2, CombatJuiceEntry>();
        Dictionary<UnityEngine.Vector3, CombatJuiceEntry> prefabByVec = new Dictionary<UnityEngine.Vector3, CombatJuiceEntry>();
        List<CombatJuiceEntry> textEntries = new List<CombatJuiceEntry>();
        List<CombatJuiceEntry> miscEntries = new List<CombatJuiceEntry>();

        while (___instance.queue.Count > 0)
        {
            CombatJuiceEntry entry = ___instance.queue.Dequeue();
            if (entry.GetType() == typeof(CombatJuiceEntryText))
            {
                CombatJuiceEntryText newEntryText = (CombatJuiceEntryText)entry;
                UnityEngine.Debug.LogError("" + newEntryText.text);
                if (isDamageString(newEntryText.text))
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
                ex3DSprite2 target = newPunch.target;
                punchByVec.TryAdd(target, entry);
            } else if (entry.GetType() == typeof(CombatJuiceEntryPrefabAnimation))
            {
                CombatJuiceEntryPrefabAnimation newPrefab = (CombatJuiceEntryPrefabAnimation)entry;
                if (newPrefab.animation.Contains("CombatJuice"))
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
        foreach (KeyValuePair<ex3DSprite2,CombatJuiceEntry> kvp in punchByVec)
        {
            ___instance.queue.Enqueue(kvp.Value);
        }
        foreach (KeyValuePair<UnityEngine.Vector3,CombatJuiceEntry> kvp in prefabByVec)
        {
            ___instance.queue.Enqueue(kvp.Value);
        }
        for (int i = 0; i < textEntries.Count; i ++)
        {
            ___instance.queue.Enqueue(textEntries[i]);
        }
        for (int i = 0; i < miscEntries.Count; i ++)
        {
            ___instance.queue.Enqueue(miscEntries[i]);
        }
    }


    [HarmonyPatch(typeof(CombatJuiceEntry))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[] {})]
    static void Postfix(CombatJuiceEntry __instance, ref float ___duration)
    {
        //TODO: Map these to options
        ___duration = .075f;
    }
}


[HarmonyPatch]
[HarmonyDebug]
class FloatingTextImprovedSound
{
    [HarmonyPatch(typeof(CombatJuiceEntryWorldSound), nameof(CombatJuiceEntryWorldSound.canStart))]
    static void Postfix(ref bool __result) {
        __result = true;
    }
}