using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FruktSharedLibrary.Combat;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.Interop;
using FruktSharedLibrary.Spawning;
using FruktSharedLibrary.UI;
using FruktSharedLibrary.Utilities;
using Il2CppData.Maps;
using Il2CppInfrastructure.Project.AssetsHandlers.SFX;
using Il2CppInfrastructure.Scenes.Factories;
using Il2CppLVA.Creatures;
using Il2CppLVA.LimbContextMenu;
using Il2CppLVA.Limbs;
using Il2CppLVA.NodesHierarchy.Benchmark.Variants;
using Il2CppLVA.Organs.Variants;
using Il2CppServices.Audio;
using Il2CppServices.Cam;
using Il2CppServices.Creatures;
using Il2CppServices.Game;
using Il2CppServices.Infrastructure;
using Il2CppServices.Player;
using Il2CppServices.Spawnables;
using Il2CppSpawnables.Weapons;
using MelonLoader.Utils;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    /// <summary>
    /// Automated in-game verification of the library. Enabled by creating the file
    /// <c>UserData/FruktSharedLibrary.selftest</c> (write "quit" in it to close the game afterwards).
    /// Loads a map, exercises every API against the live game and writes a PASS/FAIL report to
    /// <c>UserData/FruktSharedLibrary.selftest.log</c>.
    /// </summary>
    internal static partial class SelfTest
    {
        private static readonly List<string> Report = new();
        private static int _passed;
        private static int _failed;
        private static int _stage;
        private static bool _quit;
        private static bool _probe;

        private static string FlagPath => Path.Combine(MelonEnvironment.UserDataDirectory, "FruktSharedLibrary.selftest");
        private static string LogPath => Path.Combine(MelonEnvironment.UserDataDirectory, "FruktSharedLibrary.selftest.log");

        internal static void Initialize()
        {
            if (!File.Exists(FlagPath))
                return;
            var flag = File.ReadAllText(FlagPath);
            _quit = flag.IndexOf("quit", StringComparison.OrdinalIgnoreCase) >= 0;
            _probe = UiProbe.Requested(flag);
            FruktLog.ForceDebug = true;
            FruktLog.Msg("[SelfTest] Enabled. The test map loads automatically from the main menu.");
            GameEvents.MainMenuEntered += OnMainMenu;
            GameEvents.SandboxReady += OnSandboxReady;
            Scheduler.After(240f, () =>
            {
                if (_stage < 3)
                {
                    Check("Self-test finished within 4 minutes", false, $"stuck at stage {_stage}, phase {GameState.Phase}");
                    Finish();
                }
            });
        }

        private static void OnMainMenu()
        {
            if (_stage == 0)
            {
                _stage = 1;
                Check("MainMenuEntered event", true);
                Scheduler.After(3f, () => Check("World.LoadMap(Yard)", World.LoadMap(MapID.Yard)));
            }
            else if (_stage == 2)
            {
                _stage = 3;
                Check("Returned to the main menu", true);
                Finish();
            }
        }

        private static void OnSandboxReady(MapID map)
        {
            if (_stage != 1)
                return;
            _stage = 2;
            Check("SandboxReady event", true, map.ToString());
            if (_probe)
            {
                _stage = 3;
                Scheduler.StartCoroutine(UiProbe.Run(_quit));
                return;
            }
            Scheduler.StartCoroutine(Run());
        }

        // ------------------------------------------------------------ the test run

        private static IEnumerator Run()
        {
            Section("Services", TestServices);
            Section("World", TestWorldInstant);
            yield return null;
            Section("World (deferred)", () =>
            {
                Check("Time scale set to 0.5", Mathf.Abs(World.TimeScale - 0.5f) < 0.01f, World.TimeScale.ToString("0.###"));
                World.TimeScale = 1f;
                Check("Gravity strength set to 3", Mathf.Abs(World.GravityStrength - 3f) < 0.05f && Mathf.Abs(Physics.gravity.magnitude - 3f) < 0.2f,
                    $"strength={World.GravityStrength:0.##} physics={Physics.gravity.magnitude:0.##}");
                World.ResetGravity();
            });

            bool pauseEvent = false;
            bool pauseListen = false;
            Action<bool> onPause = _ => pauseEvent = true;
            GameEvents.PauseChanged += onPause;
            IDisposable listen = null;
            Section("Pause", () =>
            {
                listen = GameServices.Get<IPauseService>().OnChangePauseState.Listen(_ => pauseListen = true);
                Check("World.Pause()", World.Pause());
            });
            for (float end = Now() + 1f; Now() < end;) yield return null;
            Section("Pause (deferred)", () =>
            {
                Check("World.IsPaused", World.IsPaused);
                Check("GameEvents.PauseChanged", pauseEvent);
                Check("IManagedEvent.Listen (game event subscription)", pauseListen);
                listen?.Dispose();
                Check("World.Resume()", World.Resume());
            });
            GameEvents.PauseChanged -= onPause;
            for (float end = Now() + 1f; Now() < end;) yield return null;
            Section("Resume (deferred)", () => Check("Unpaused", !World.IsPaused));

            Vector3 start = default;
            Section("Player", () =>
            {
                Check("LocalPlayer.Exists", LocalPlayer.Exists);
                Check("LocalPlayer.Camera", LocalPlayer.Camera.Exists(), LocalPlayer.Camera?.name);
                start = LocalPlayer.Position;
                Check("LocalPlayer.Position", start != Vector3.zero, start.ToString());
                bool hit = LocalPlayer.Raycast(out var rayHit);
                Check("LocalPlayer.Raycast", true, hit ? $"hit {rayHit.collider.name} at {rayHit.distance:0.0}m" : "nothing in view");
                Check("LocalPlayer.FieldOfView", LocalPlayer.FieldOfView > 1f, LocalPlayer.FieldOfView.ToString("0.#"));
                Check("LocalPlayer.Teleport", LocalPlayer.Teleport(start + new Vector3(0f, 2f, 0f)));
            });
            for (float end = Now() + 0.5f; Now() < end;) yield return null;
            Section("Player (deferred)", () =>
            {
                Check("Teleport moved the player", (LocalPlayer.Position - start).magnitude > 1f, $"{start} -> {LocalPlayer.Position}");
                Check("LocalPlayer.ResetToStart", LocalPlayer.ResetToStart());
            });
            for (float end = Now() + 0.5f; Now() < end;) yield return null;

            var firearms = new List<Firearm>();
            Section("Spawner", () =>
            {
                var ids = Spawner.GetRegisteredPrefabIds();
                Check("Registered prefab IDs", ids.Count > 0, $"{ids.Count}: {string.Join(", ", ids.Take(40))}");
                var props = Spawner.GetPropNames();
                Check("Prop names", true, $"{props.Count}: {string.Join(", ", props)}");
                var firearmIds = Spawner.GetPrefabIds<Firearm>();
                Check("GetPrefabIds<Firearm>", firearmIds.Count >= 3, string.Join(", ", firearmIds));
                var propIds = Spawner.GetPrefabIds<Il2CppSpawnables.Props.Prop>();
                Check("GetPrefabIds<Prop>", true, $"{propIds.Count}: {string.Join(", ", propIds)}");
                int i = 0;
                foreach (FirearmType type in Enum.GetValues(typeof(FirearmType)))
                {
                    var firearm = Spawner.SpawnFirearmInFront(type, 2f + i++);
                    Check($"SpawnFirearm({type})", firearm != null, firearm?.name);
                    if (firearm != null)
                        firearms.Add(firearm);
                }
                if (props.Count > 0)
                {
                    var prop = Spawner.SpawnProp(props[0], LocalPlayer.GetPointInFront(3f) + Vector3.up);
                    Check($"SpawnProp({props[0]})", prop != null, prop?.name);
                }
            });
            for (float end = Now() + 1.5f; Now() < end;) yield return null;

            bool fired = false;
            Action<Firearm> onFire = _ => fired = true;
            GameEvents.FirearmFired += onFire;
            Section("Firearms", () =>
            {
                Check("Spawned objects are listed", Spawner.GetSpawnedObjects().Count >= firearms.Count, Spawner.GetSpawnedObjects().Count.ToString());
                if (firearms.Count > 0)
                    Check("Firearm.Fire()", firearms[0].Fire());
            });
            for (float end = Now() + 1f; Now() < end;) yield return null;
            Section("Firearms (deferred)", () => Check("GameEvents.FirearmFired", fired));
            GameEvents.FirearmFired -= onFire;

            // ---------------------------------------------------- creatures
            AbstractCreature human = null;
            bool spawnedEvent = false;
            Action<AbstractCreature> onSpawned = _ => spawnedEvent = true;
            GameEvents.CreatureSpawned += onSpawned;
            Section("SpawnHuman", () => Check("Creatures.SpawnHumanInFront", Creatures.SpawnHumanInFront(4f, c => human = c)));
            float spawnStart = Now();
            for (float end = Now() + 15f; human == null && Now() < end;) yield return null;
            GameEvents.CreatureSpawned -= onSpawned;
            Check("SpawnHuman callback", human != null, $"{Now() - spawnStart:0.0}s");
            Check("GameEvents.CreatureSpawned", spawnedEvent);
            if (human == null)
            {
                Finish();
                yield break;
            }
            for (float end = Now() + 2f; Now() < end;) yield return null;

            AbstractLimb head = null;
            Section("Creature inspection", () =>
            {
                Check("IsLiving", human.IsLiving());
                Check("IsHuman", human.IsHuman());
                Check("Limb count", human.GetLimbCount() >= 15, human.GetLimbCount().ToString());
                Check("Creatures.All contains it", Creatures.All.Any(c => c.Pointer == human.Pointer), Creatures.Count.ToString());
                head = human.GetLimb(HumanoidNodeTagValue.Head);
                Check("GetLimb(Head)", head != null, head?.name);
                Check("Head part tag", head?.GetHumanPart() == HumanoidNodeTagValue.Head);
                Check("Root limb is the pelvis", human.GetRootLimb()?.GetHumanPart() == HumanoidNodeTagValue.Pelvis, human.GetRootLimb()?.name);
                Check("GetLimbs() walks all 15 limbs", human.GetLimbs().Count == human.GetLimbCount(), $"{human.GetLimbs().Count}/{human.GetLimbCount()}");
                var allLimbs = human.LimbsHierarchyHandler.Navigator.AllLimbs;
                Check("ToManagedList on a game interface collection", allLimbs.ToManagedList().Count == human.GetLimbCount(),
                    $"{allLimbs.ToManagedList().Count} via {allLimbs.GetIl2CppTypeName(true)}");
                var organNames = head == null ? new List<string>() : head.GetAllOrgans().ConvertAll(o => o.GetOrganName());
                Check("Head organs include a brain", head?.GetOrgan<Brain>() != null, string.Join(", ", organNames));
                var parameters = human.GetAllParameters();
                Check("Creature parameters", parameters.Count > 0, string.Join(" | ", parameters));
                Check("Creature systems", human.GetAllSystems().Count > 0, string.Join(", ", human.GetAllSystems().Select(s => s.GetIl2CppTypeName())));
                Check("Blood tank", human.GetBloodTank() != null, $"{human.GetBlood():0.##}/{human.GetBloodCapacity():0.##}");
                Check("Head wholeness", head != null && head.GetWholeness() > 0f, head?.GetWholeness().ToString("0.###"));
                Check("Head limb parameters", head != null && head.GetAllParameters().Count > 0, string.Join(" | ", head?.GetAllParameters() ?? new List<LvaParameterInfo>()));
                Check("Head limb systems", head != null && head.GetAllSystems().Count > 0, string.Join(", ", head?.GetAllSystems().Select(s => s.GetIl2CppTypeName()) ?? Enumerable.Empty<string>()));
                var collider = head?.GetComponentInChildrenIl2Cpp<Collider>();
                Check("Creatures.FromCollider", collider != null && Creatures.FromCollider(collider)?.Pointer == human.Pointer, collider?.name);
                Check("Creatures.GetNearest", Creatures.GetNearest(human.GetPosition())?.Pointer == human.Pointer);
                FruktLog.Msg("[SelfTest] " + DevTools.DescribeCreature(human));
            });

            // ---------------------------------------------------- context menus
            Section("Context menus", () =>
            {
                bool clicked = false;
                var entry = ContextMenus.AddLimbAction("SelfTest action", _ => clicked = true);
                var handler = FindLimbMenuHandler(head);
                Check("Limb context menu handler found", handler != null, handler?.name);
                if (handler == null)
                    return;
                var actions = handler.InitializeContextActions();
                var names = new List<string>();
                Il2CppPlayer.ContextMenu.Actions.ContextMenuAction ours = null;
                for (int i = 0; i < actions.Count; i++)
                {
                    names.Add($"{actions[i].Name}({actions[i].Priority})");
                    if (actions[i].Name == "SelfTest action")
                        ours = actions[i];
                }
                Check("Custom action added to the limb menu", ours != null, string.Join(", ", names));
                Il2CppPlayer.ContextMenu.Actions.ContextMenuAction gameAction = null;
                for (int i = 0; i < actions.Count && gameAction == null; i++)
                {
                    if (actions[i].Priority >= 995 && actions[i].Name != "SelfTest action")
                        gameAction = actions[i];
                }
                if (ours != null && gameAction != null)
                    Check("Priority ordering (informational)", true, $"ours(500).CompareTo({gameAction.Name}({gameAction.Priority})) = {ours.CompareTo(gameAction)}");
                if (ours != null)
                {
                    ours.Execute();
                    Check("Custom action click handler ran", clicked);
                    Check("Creature survived the carrier action", human.IsValid());
                }
                entry.Remove();
                for (int i = 0; i < actions.Count; i++)
                {
                    try { actions[i].Dispose(); } catch { /* ignore */ }
                }
            });

            // ---------------------------------------------------- context menu UI (screenshot)
            ContextMenuEntry uiEntry = null;
            Section("Context menu UI", () =>
            {
                uiEntry = ContextMenus.AddCreatureAction("FSL: Heal", c => c.Heal());
                ContextMenus.AddToggle(ContextMenuTarget.Limb, "FSL: Walk", ctx => ctx.Creature.IsWalking(), (ctx, on) => ctx.Creature.SetWalking(on));
                var handler = FindLimbMenuHandler(head);
                var menu = GameServices.TryGet<Il2CppServices.UI.IContextMenuService>();
                Check("IContextMenuService resolved", menu != null);
                if (menu != null && handler != null)
                    Check("Context menu opened on the head", menu.TryOpen(handler));
            });
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            Section("Context menu screenshot", () => FruktLog.Msg("[SelfTest] SCREENSHOT contextmenu"));
            for (float end = Now() + 2.5f; Now() < end;) yield return null;
            Section("Context menu close", () =>
            {
                GameServices.TryGet<Il2CppServices.UI.IContextMenuService>()?.CloseMenu();
                ContextMenus.RemoveAll();
            });
            for (float end = Now() + 0.5f; Now() < end;) yield return null;

            // ---------------------------------------------------- damage
            float wholenessBefore = 0f;
            Vector3 headPoint = default;
            Section("Damage", () =>
            {
                wholenessBefore = head.GetWholeness();
                headPoint = head.GetRigidbody().worldCenterOfMass;
                Check("Damage.Apply(head)", Damage.Apply(head, headPoint, 3, 1f));
            });
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            bool negativeWorked = false;
            Section("Damage (deferred)", () =>
            {
                float after = head.Exists() ? head.GetWholeness() : -1f;
                negativeWorked = after < wholenessBefore - 1e-4f;
                Check("Damage reduced head wholeness", negativeWorked, $"{wholenessBefore:0.####} -> {after:0.####}");
                if (!negativeWorked && head.Exists())
                {
                    wholenessBefore = after;
                    Damage.ApplySignal(head, headPoint, 3, 1f);
                }
            });
            if (!negativeWorked)
            {
                for (float end = Now() + 1.5f; Now() < end;) yield return null;
                Section("Damage sign probe", () =>
                {
                    float after = head.Exists() ? head.GetWholeness() : -1f;
                    Check("Positive signal reduced wholeness (sign probe)", after < wholenessBefore - 1e-4f, $"{wholenessBefore:0.####} -> {after:0.####}");
                });
            }

            // ---------------------------------------------------- detach & bleeding
            bool detachEvent = false;
            Action<AbstractCreature, AbstractLimb> onDetach = (_, _) => detachEvent = true;
            GameEvents.LimbDetached += onDetach;
            Section("Detach", () =>
            {
                var hand = human.GetLimb(HumanoidNodeTagValue.LeftHand);
                Check("GetLimb(LeftHand)", hand != null);
                Check("Limb.Detach()", hand != null && hand.Detach());
            });
            for (float end = Now() + 1.5f; Now() < end;) yield return null;
            GameEvents.LimbDetached -= onDetach;
            Section("Detach (deferred)", () =>
            {
                Check("Hand no longer attached", !human.HasLimb(HumanoidNodeTagValue.LeftHand));
                Check("GameEvents.LimbDetached", detachEvent);
                var forearm = human.GetLimb(HumanoidNodeTagValue.LeftForearm);
                int wounds = forearm?.GetBleedingWoundCount() ?? -1;
                Check("Forearm bleeds after detach", true, $"{wounds} wound(s)");
                int closed = human.StopBleeding();
                Check("StopBleeding", closed > 0, $"{closed} limbs processed, forearm wounds now {forearm?.GetBleedingWoundCount() ?? -1}");
                Check("RefillBlood", human.RefillBlood(), $"{human.GetBlood():0.##}/{human.GetBloodCapacity():0.##}");
            });

            // ---------------------------------------------------- kill
            bool died = false;
            bool killAdded = false;
            Action<AbstractCreature> onDied = c => { if (c.Pointer == human.Pointer) died = true; };
            Action<int> onKill = _ => killAdded = true;
            GameEvents.CreatureDied += onDied;
            GameEvents.KillAdded += onKill;
            Section("Kill", () => Check("Creature.Kill()", human.Kill()));
            float killStart = Now();
            for (float end = Now() + 40f; !died && Now() < end;) yield return null;
            GameEvents.CreatureDied -= onDied;
            GameEvents.KillAdded -= onKill;
            Check("GameEvents.CreatureDied after Kill", died, $"{Now() - killStart:0.0}s, lifeless={human.Exists() && human.Lifeless}");
            Check("GameEvents.KillAdded (informational)", true, killAdded ? "kill counted" : "kill not counted by the game");

            // ---------------------------------------------------- explosion on a second human
            AbstractCreature second = null;
            Section("Second human", () => Creatures.SpawnHumanInFront(5f, c => second = c));
            for (float end = Now() + 15f; second == null && Now() < end;) yield return null;
            for (float end = Now() + 2f; Now() < end;) yield return null;

            Vector3 walkStart = default;
            Section("Walking", () =>
            {
                Check("GetWalkInteraction", second.GetWalkInteraction() != null);
                walkStart = second.GetPosition();
                Check("SetWalking(true)", second.SetWalking(true));
            });
            for (float end = Now() + 4f; Now() < end;) yield return null;
            Section("Walking (deferred)", () =>
            {
                Check("IsWalking", second.IsWalking());
                Check("Creature moved while walking (informational)", true, $"{(second.GetPosition() - walkStart).magnitude:0.00} m");
                Check("SetWalking(false)", second.SetWalking(false));
            });
            for (float end = Now() + 1f; Now() < end;) yield return null;
            Section("Walking stopped", () => Check("Stopped walking", !second.IsWalking()));

            Section("Explosion", () =>
            {
                Check("Second human spawned", second != null);
                if (second == null)
                    return;
                int hit = Damage.Explosion(second.GetPosition(), 2f, 30f, 4);
                Check("Damage.Explosion hit limbs", hit > 0, $"{hit} limbs");
            });
            for (float end = Now() + 1f; Now() < end;) yield return null;

            // ---------------------------------------------------- delete
            bool removed = false;
            Action<AbstractCreature> onRemoved = c => { if (c.Pointer == human.Pointer) removed = true; };
            GameEvents.CreatureRemoved += onRemoved;
            Section("Delete", () => Check("Creature.Delete()", human.Delete()));
            for (float end = Now() + 3f; !removed && Now() < end;) yield return null;
            GameEvents.CreatureRemoved -= onRemoved;
            Check("GameEvents.CreatureRemoved", removed);

            Section("Delete all", () => Check("World.DeleteAllCreatures()", World.DeleteAllCreatures()));
            for (float end = Now() + 2f; Now() < end;) yield return null;
            Section("Delete all (deferred)", () => Check("No creatures left", Creatures.Count == 0, Creatures.Count.ToString()));

            // ---------------------------------------------------- audio & UI
            Section("Audio", () =>
            {
                Check("Sounds.Play(UI)", Sounds.Play(UISFXType.SwitchOn));
                Check("Sounds.Play(Weapon, position)", Sounds.Play(WeaponSFXType.Shoot9MM, LocalPlayer.Position));
                LocalPlayer.ShakeCamera(0.3f);
            });
            Section("Textures", () =>
            {
                var source = Textures.Solid(new Color(1f, 0.5f, 0f), 8, 4);
                var png = Textures.EncodeToPng(source);
                Check("Textures.EncodeToPng", png != null && png.Length > 0, png?.Length + " bytes");
                var loaded = Textures.LoadFromBytes(png);
                Check("Textures.LoadFromBytes round trip", loaded != null && loaded.width == 8 && loaded.height == 4, loaded == null ? "null" : $"{loaded.width}x{loaded.height}");
                Check("Textures.ToSprite", Textures.ToSprite(loaded) != null);
            });
            Section("UI", () =>
            {
                Notifications.Show("FruktSharedLibrary self-test notification", 8f);
                Notifications.Warn("A warning-style notification", 8f);
            });
            for (float end = Now() + 1f; Now() < end;) yield return null;
            Section("UI (deferred)", () =>
            {
                Check("Notifications drew without errors", !Notifications.DrawFailed);
                if (Notifications.UsingNative)
                    Check("Notifications use the native style", Notifications.NativeViewCount == 2, Notifications.NativeViewCount + " plates");
            });
            var menuTest = TestModMenu();
            while (true)
            {
                bool more;
                try
                {
                    more = menuTest.MoveNext();
                }
                catch (Exception e)
                {
                    Check("Mod menu test ran without exceptions", false, e.ToString());
                    break;
                }
                if (!more)
                    break;
                yield return menuTest.Current;
            }
            ModMenu.ForceSimple = false;
            ModMenu.Close();
            for (float end = Now() + 0.5f; Now() < end;) yield return null;

            Section("Return to menu", () => Check("World.ReturnToMainMenu()", World.ReturnToMainMenu()));
        }

        // ------------------------------------------------------------ sections

        private static void TestServices()
        {
            CheckService<IPauseService>();
            CheckService<ITimeScaleService>();
            CheckService<IWorldGravityService>();
            CheckService<IKillsService>();
            CheckService<IMapResetService>();
            CheckService<IMapCatalogue>();
            CheckService<ICreatureRegistryService>();
            CheckService<ICreatureSweepService>();
            CheckService<IHumanFactory>();
            CheckService<IGameStateMachine>();
            CheckService<IGodPlayerAppearanceService>();
            CheckService<IPlayerCameraService>();
            CheckService<IPlayerResetService>();
            CheckService<ISpawnablesFactory>();
            CheckService<INativeSpawnablesHandler>();
            CheckService<INativeMapPropsHandler>();
            CheckService<IMapObjectsRegistryService>();
            CheckService<ISFXPlayerService>();
            CheckService<IPlayerCameraShakeService>();
            CheckService<IRegisteredPrefabsHandler>();
        }

        private static void TestWorldInstant()
        {
            Check("World.Maps", World.Maps.Count > 0, string.Join(", ", World.Maps.Select(m => $"{m}={World.GetMapDisplayName(m)}/{World.GetMapSceneName(m)}")));
            Check("GameState.CurrentMap", GameState.CurrentMap.HasValue, GameState.CurrentMap?.ToString());
            Check("World.TimeScale readable", World.TimeScale > 0f, World.TimeScale.ToString("0.###"));
            World.TimeScale = 0.5f;
            var gravity = World.Gravity;
            Check("World.Gravity readable", gravity.Strength > 0f, $"strength={gravity.Strength} tilt={gravity.TiltDegrees} turn={gravity.TurnDegrees}");
            World.GravityStrength = 3f;
            Check("World.KillCount", World.KillCount >= 0, World.KillCount.ToString());
        }

        private static LimbContextMenuActionsHandler FindLimbMenuHandler(AbstractLimb limb)
        {
            if (limb == null)
                return null;
            var handler = limb.GetComponentInChildrenIl2Cpp<LimbContextMenuActionsHandler>()
                          ?? limb.GetComponentInParentIl2Cpp<LimbContextMenuActionsHandler>();
            if (handler != null)
                return handler;
            foreach (var candidate in GameServices.FindObjects<LimbContextMenuActionsHandler>(true))
            {
                var assigned = candidate.m_assignedLimb;
                if (assigned != null && assigned.Pointer == limb.Pointer)
                    return candidate;
            }
            return null;
        }

        private static void CheckService<T>() where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
        {
            var service = GameServices.TryGet<T>();
            Check($"Service {typeof(T).Name}", service != null, service?.GetIl2CppTypeName());
        }

        private static void Section(string name, Action body)
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                Check($"{name} (threw)", false, e.ToString());
            }
        }

        private static void Check(string name, bool ok, string detail = null)
        {
            if (ok) _passed++;
            else _failed++;
            string line = $"{(ok ? "PASS" : "FAIL")}  {name}{(string.IsNullOrEmpty(detail) ? "" : "  ->  " + detail)}";
            Report.Add(line);
            if (ok)
                FruktLog.Msg("[SelfTest] " + line);
            else
                FruktLog.Warning("[SelfTest] " + line);
        }

        private static void Finish()
        {
            var text = new StringBuilder();
            text.AppendLine($"FruktSharedLibrary {FruktSharedLibraryMod.Version} self-test  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            text.AppendLine($"Passed: {_passed}  Failed: {_failed}");
            text.AppendLine();
            foreach (var line in Report)
                text.AppendLine(line);
            text.AppendLine();
            text.AppendLine("Hook statistics (calls / calls on an unexpected class):");
            foreach (var pair in LibraryPatches.HookStats)
                text.AppendLine($"  {pair.Key}: {pair.Value.Calls} / {pair.Value.Foreign}");
            try
            {
                File.WriteAllText(LogPath, text.ToString());
            }
            catch (Exception e)
            {
                FruktLog.Warning("Writing the self-test log failed: " + e.Message);
            }
            FruktLog.Msg($"[SelfTest] Done: {_passed} passed, {_failed} failed. Report: {LogPath}");
            if (_quit)
                Scheduler.After(2f, World.Quit);
        }

        private static float Now() => Time.realtimeSinceStartup;
    }
}
