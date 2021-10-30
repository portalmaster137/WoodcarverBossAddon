using BepInEx;
using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using UnityEngine;
using HarmonyLib;
using DiskCardGame;
using GBC;
using System.Reflection.Emit;

namespace WoodcarverBossAddon
{

    public class WoodcarverFight : Part1BossOpponent
    {
        public EncounterBlueprintData blueprint;
        public EncounterData data;

        protected override string DefeatedPlayerDialogue
        {
            get
            {
                return "Time to get to carving.";
            }
        }
        public override IEnumerator IntroSequence(EncounterData encounter)
        {
            Plugin.Log.LogInfo("Intro Called");
            AudioController.Instance.FadeOutLoop(0.75f, Array.Empty<int>());
            yield return base.IntroSequence(encounter);
            this.SetSceneEffectsShown(true);
            yield return new WaitForSeconds(0.75f);
            AudioController.Instance.SetLoopAndPlay("boss_trappertrader_ambient", 1, true, true);
            Singleton<ViewManager>.Instance.SwitchToView(View.Default, false, false);
            yield return new WaitForSeconds(0.75f);
            //yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("WoodcarverBossPreIntro", TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, null);
            yield return Singleton<TextDisplayer>.Instance.ShowUntilInput("You noticed a familiar woman in the distance.");
            yield return Singleton<TextDisplayer>.Instance.ShowUntilInput("It was the Woodcarver");
            yield return new WaitForSeconds(0.15f);
            AudioController.Instance.SetLoopAndPlay("boss_trappertrader_base", 0, true, true);
            LeshyAnimationController.Instance.PutOnMask(LeshyAnimationController.Mask.Woodcarver, false);
            yield return new WaitForSeconds(1.5f);
            yield return base.FaceZoomSequence();
            //yield return Singleton<TextDisplayer>.Instance.PlayDialogueEvent("WoodcarverBossIntro", TextDisplayer.MessageAdvanceMode.Input, TextDisplayer.EventIntersectMode.Wait, null, null);
            yield return Singleton<TextDisplayer>.Instance.ShowUntilInput("My next totem shall be you.");
            Singleton<ViewManager>.Instance.SwitchToView(View.Default, false, false);
            Singleton<ViewManager>.Instance.Controller.LockState = ViewLockState.Unlocked;
            base.SpawnScenery("TotemsTableEffects");
            yield break;
        }
        public void SetBackgroundEyesLit(bool lit)
        {
            foreach (CompositeTotemPiece compositeTotemPiece in this.sceneryObject.GetComponentsInChildren<CompositeTotemPiece>())
            {
                compositeTotemPiece.EmissionColor = GameColors.Instance.nearWhite;
                compositeTotemPiece.SetEmitting(lit, false);
            }
        }
        protected override void SetSceneEffectsShown(bool showEffects)
		{

			if (showEffects)
			{
				Color nearWhite = GameColors.Instance.nearWhite;
				nearWhite.a = 0.5f;
				Singleton<TableVisualEffectsManager>.Instance.ChangeTableColors(Color.white, GameColors.Instance.marigold, GameColors.Instance.nearWhite, nearWhite, GameColors.Instance.marigold, GameColors.Instance.nearWhite, GameColors.Instance.gray, GameColors.Instance.gray, GameColors.Instance.lightGray);
				Singleton<ExplorableAreaManager>.Instance.SetHangingLightIntensity(3f);
				return;
			}
			Singleton<TableVisualEffectsManager>.Instance.ResetTableColors();
			Singleton<ExplorableAreaManager>.Instance.ResetHangingLightIntensity();
		}
    }



    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "porta.inscryption.woodcarveraddon";
        private const string PluginName = "WoodcarverAddon";
        private const string PluginVersion = "1.0.0";
        internal static ManualLogSource Log;

        private enum Boss
        {
            PROSPECTOR, ANGLER, TRAPPER
        }

        private void Awake()
        {
            Logger.LogInfo($"Loading {PluginName}");
            Plugin.Log = base.Logger;
            Log.LogInfo("Reading Configuration File");
            BepInEx.Configuration.ConfigEntry<string> overwrite = Config.Bind("Woodcarver", "overwrite", "prospector", new BepInEx.Configuration.ConfigDescription("Overwrite the boss specified"));
            Log.LogInfo("Loaded Config");
            Boss OverriddenBoss;
            switch (overwrite.Value)
            {
                case "prospector":
                    OverriddenBoss = Boss.PROSPECTOR;
                    break;
                case "angler":
                    OverriddenBoss = Boss.ANGLER;
                    break;
                case "trapper":
                    OverriddenBoss = Boss.TRAPPER;
                    break;
                default:
                    Log.LogError($"Incorrect Value specified - {overwrite.Value}, Defaulting to Prospector");
                    OverriddenBoss = Boss.PROSPECTOR;
                    break;
            }
            Log.LogInfo($"Boss set to {OverriddenBoss}");
            Log.LogInfo("Commencing Harmony Patch");
            Harmony harmony = new Harmony(PluginGuid);
            switch (OverriddenBoss)
            {
                case Boss.PROSPECTOR:
                    harmony.Patch(typeof(Opponent).GetMethod("SpawnOpponent"), prefix: new HarmonyMethod(typeof(ProspectorPatch).GetMethod("Pre")));
                    break;
                case Boss.ANGLER:
                    harmony.Patch(typeof(Opponent).GetMethod("SpawnOpponent"), postfix: new HarmonyMethod(typeof(AnglerPatch).GetMethod("Pre")));
                    break;
                case Boss.TRAPPER:
                    harmony.Patch(typeof(Opponent).GetMethod("SpawnOpponent"), postfix: new HarmonyMethod(typeof(TrapperPatch).GetMethod("Pre")));
                    break;
                default:
                    break;
            }
            Log.LogInfo("Patching Complete!");
        }

        [HarmonyPatch(typeof(Opponent), "SpawnOpponent")]
        class ProspectorPatch
        { 
            [HarmonyPrefix]
            public static bool Pre(EncounterData encounterData, ref Opponent __result)
            {
                if (encounterData.opponentType == Opponent.Type.ProspectorBoss)
                {
                    GameObject gameObject = new GameObject();
                    gameObject.name = "Opponent";
                    Opponent.Type opponentType = (!ProgressionData.LearnedMechanic(MechanicsConcept.OpponentQueue)) ? Opponent.Type.NoPlayQueue : encounterData.opponentType;
                    Opponent opponent;
                    opponent = gameObject.AddComponent<WoodcarverFight>();
                    string text = encounterData.aiId;
                    if (string.IsNullOrEmpty(text))
                    {
                        text = "AI";
                    }
                    opponent.AI = (Activator.CreateInstance(CustomType.GetType("DiskCardGame", text)) as AI);
                    opponent.NumLives = opponent.StartingLives;
                    opponent.OpponentType = opponentType;
                    opponent.TurnPlan = opponent.ModifyTurnPlan(encounterData.opponentTurnPlan);
                    opponent.Blueprint = encounterData.Blueprint;
                    opponent.Difficulty = encounterData.Difficulty;
                    opponent.ExtraTurnsToSurrender = SeededRandom.Range(0, 3, SaveManager.SaveFile.GetCurrentRandomSeed());
                    __result = opponent;
                    return false;
                } else
                {
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Opponent), "SpawnOpponent")]
        class AnglerPatch
        {
            [HarmonyPrefix]
            public static bool Pre(EncounterData encounterData, ref Opponent __result)
            {
                if (encounterData.opponentType == Opponent.Type.AnglerBoss)
                {
                    GameObject gameObject = new GameObject();
                    gameObject.name = "Opponent";
                    Opponent.Type opponentType = (!ProgressionData.LearnedMechanic(MechanicsConcept.OpponentQueue)) ? Opponent.Type.NoPlayQueue : encounterData.opponentType;
                    Opponent opponent;
                    opponent = gameObject.AddComponent<WoodcarverFight>();
                    string text = encounterData.aiId;
                    if (string.IsNullOrEmpty(text))
                    {
                        text = "AI";
                    }
                    opponent.AI = (Activator.CreateInstance(CustomType.GetType("DiskCardGame", text)) as AI);
                    opponent.NumLives = opponent.StartingLives;
                    opponent.OpponentType = opponentType;
                    opponent.TurnPlan = opponent.ModifyTurnPlan(encounterData.opponentTurnPlan);
                    opponent.Blueprint = encounterData.Blueprint;
                    opponent.Difficulty = encounterData.Difficulty;
                    opponent.ExtraTurnsToSurrender = SeededRandom.Range(0, 3, SaveManager.SaveFile.GetCurrentRandomSeed());
                    __result = opponent;
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Opponent), "SpawnOpponent")]
        class TrapperPatch
        {
            [HarmonyPostfix]
            public static bool Pre(EncounterData encounterData, ref Opponent __result)
            {
                if (encounterData.opponentType == Opponent.Type.TrapperTraderBoss)
                {
                    GameObject gameObject = new GameObject();
                    gameObject.name = "Opponent";
                    Opponent.Type opponentType = (!ProgressionData.LearnedMechanic(MechanicsConcept.OpponentQueue)) ? Opponent.Type.NoPlayQueue : encounterData.opponentType;
                    Opponent opponent;
                    opponent = gameObject.AddComponent<WoodcarverFight>();
                    string text = encounterData.aiId;
                    if (string.IsNullOrEmpty(text))
                    {
                        text = "AI";
                    }
                    opponent.AI = (Activator.CreateInstance(CustomType.GetType("DiskCardGame", text)) as AI);
                    opponent.NumLives = opponent.StartingLives;
                    opponent.OpponentType = opponentType;
                    opponent.TurnPlan = opponent.ModifyTurnPlan(encounterData.opponentTurnPlan);
                    opponent.Blueprint = encounterData.Blueprint;
                    opponent.Difficulty = encounterData.Difficulty;
                    opponent.ExtraTurnsToSurrender = SeededRandom.Range(0, 3, SaveManager.SaveFile.GetCurrentRandomSeed());
                    __result = opponent;
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }


    }
}
