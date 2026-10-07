using System.Collections;
using System.Linq;
using FruktSharedLibrary.Combat;
using FruktSharedLibrary.Core;
using FruktSharedLibrary.Entities;
using FruktSharedLibrary.Gameplay;
using FruktSharedLibrary.UI;
using Il2CppLVA.Creatures;
using Il2CppLVA.NodesHierarchy.Benchmark.Variants;
using UnityEngine;

namespace FruktSharedLibrary.Internal
{
    // Mod guns and bullets, walking people about, and labels over things in the world. The gun is added in
    // OnInitializeMelon like a mod would, then fired with real clicks at people hung in the crosshair.
    internal static partial class SelfTest
    {
        private static ModGun _testGun;
        private static int _testGunShots;

        private static void AddTestGun()
        {
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "Self-test gun model";
            model.transform.localScale = new Vector3(0.05f, 0.08f, 0.3f);
            model.SetActive(false);
            Object.DontDestroyOnLoad(model);
            _testGun = Inventory.AddGun("Self-test gun")
                .WithCooldown(0.5f)
                .WithMuzzle(new Vector3(0f, 0f, 0.5f))
                .OnFire(_ => _testGunShots++);
            _testGun.WithDescription("Added by FruktSharedLibrary's self-test.")
                .WithModel(model, new Vector3(0.25f, -0.2f, 0.5f));
        }

        private static IEnumerator TestGunsAndPeople()
        {
            var gun = _testGun;
            Section("Mod guns", () =>
            {
                Check("Inventory.AddGun registered the gun", gun.Registered && !gun.Failed);
                Check("It's under Weapons", gun.Item?.CategoryName == "Weapons", gun.Item?.CategoryName);
                Check("It's a mod tool too", Inventory.ModTools.Contains(gun));
            });
            Creatures.DeleteAll();
            yield return Wait(1f);

            AbstractCreature first = null, second = null;
            Creatures.SpawnHuman(LocalPlayer.GetPointInFront(4f), LocalPlayer.RotationFacingPlayer(LocalPlayer.GetPointInFront(4f)), c => first = c);
            for (float end = Now() + 15f; first == null && Now() < end;) yield return null;
            Creatures.SpawnHuman(LocalPlayer.GetPointInFront(7f), LocalPlayer.RotationFacingPlayer(LocalPlayer.GetPointInFront(7f)), c => second = c);
            for (float end = Now() + 15f; second == null && Now() < end;) yield return null;
            Check("Two people spawned for the gun test", first != null && second != null);
            if (first == null || second == null)
                yield break;
            yield return Wait(2.5f);

            // People walking where they're told, and finding the nearest one with a filter.
            Section("Walking people about", () =>
            {
                Check("GetNearest with a filter skips what it rejects",
                    Creatures.GetNearest(first.GetPosition(), c => c.Pointer != first.Pointer && c.IsLiving() && c.IsHuman())?.Pointer == second.Pointer);
                Check("GetNearest without a filter still works", Creatures.GetNearest(first.GetPosition(), 0.5f, livingOnly: true)?.Pointer == first.Pointer);
                Check("GetFacing reads which way they face", first.GetFacing().HasValue);
            });
            var right = LocalPlayer.CameraRotation * Vector3.right;
            right.y = 0f;
            var goal = first.GetPosition() + right.normalized * 6f;
            float startDistance = Flat(goal - first.GetPosition());
            Check("WalkTowards starts them walking", first.WalkTowards(goal) && first.IsWalking());
            for (float end = Now() + 6f; Now() < end && Flat(goal - first.GetPosition()) > 1.5f;)
            {
                first.WalkTowards(goal);
                yield return null;
            }
            float walked = startDistance - Flat(goal - first.GetPosition());
            first.SetWalking(false);
            Check("They walk towards the point", walked > 2f, $"{walked:0.0} m closer");
            Check("They face where they walked", first.GetFacing().HasValue &&
                Mathf.Abs(Mathf.DeltaAngle(first.GetFacing().Value, Mathf.Atan2(right.x, right.z) * Mathf.Rad2Deg)) < 40f, first.GetFacing()?.ToString("0"));
            Shot("walked");
            yield return Wait(1.5f);

            // Labels over things.
            var head = first.GetLimb(HumanoidNodeTagValue.Head);
            Check("A limb's GetPosition follows the body after it walks", head != null && Vector3.Distance(head.GetPosition(), first.GetPosition()) < 1f,
                head == null ? "no head" : $"{Vector3.Distance(head.GetPosition(), first.GetPosition()):0.00} m from the hips");
            var label = head != null ? WorldLabels.Add(head.GetMovingTransform(), "Test label", Color.green, Vector3.up * 0.4f) : null;
            var temporary = new GameObject("Self-test label target");
            var orphan = WorldLabels.Add(temporary.transform, "Gone soon");
            yield return Wait(0.5f);
            Check("A label over a head shows on screen", label != null && label.OnScreen && label.Exists);
            Shot("world-label");
            yield return Wait(1.5f);
            Object.Destroy(temporary);
            yield return Wait(0.3f);
            Check("A label goes away with its object", !orphan.Exists && !WorldLabels.All.Contains(orphan));
            label?.Remove();
            Check("Remove takes a label away", label != null && !label.Exists && !WorldLabels.All.Contains(label));

            // The gun, with real clicks.
            int slot = Enumerable.Range(0, Toolbar.SlotCount).Where(s => Toolbar.CanChange(s) && Toolbar.IsEmpty(s)).DefaultIfEmpty(Toolbar.SlotCount - 1).First();
            Toolbar.Put(gun.Item, slot);
            Toolbar.Select(slot);
            yield return Wait(1.2f);
            Check("The player holds the gun", gun.IsHeld);
            Check("The muzzle follows the model in the hand", Vector3.Distance(gun.Muzzle, LocalPlayer.CameraPosition) < 2f);
            int before = _testGunShots;
            FruktLog.Msg("[SelfTest] CLICK gun-click 0.5000 0.5000");
            for (float end = Now() + 2f; _testGunShots == before && Now() < end;) yield return null;
            Check("A real click fires the gun", _testGunShots == before + 1 && gun.ShotsFired >= 1, $"{_testGunShots - before} shots");
            Check("It won't fire again before its cooldown", !gun.TryFire());
            yield return Wait(0.6f);
            Check("It fires again after the cooldown", gun.TryFire());
            gun.WithFireRate(20f, automatic: true);
            before = _testGunShots;
            FruktLog.Msg("[SelfTest] MOUSEDOWN gun-hold");
            yield return Wait(1f);
            FruktLog.Msg("[SelfTest] MOUSEUP gun-hold");
            yield return Wait(0.3f);
            Check("An automatic gun fires while the button is held", _testGunShots - before >= 8, $"{_testGunShots - before} shots in a second");
            gun.WithCooldown(0.5f);
            Toolbar.Clear(slot);
            Toolbar.Select(Toolbar.CursorSlot);
            yield return Wait(0.5f);

            // Bullets at people hung in the crosshair.
            Hang(first, 4f);
            Hang(second, 6.5f);
            yield return Wait(0.2f);
            Hang(first, 4f);
            Hang(second, 6.5f);
            float hurtFirst = Hurt(first), hurtSecond = Hurt(second);
            Check("AimPoint is on the person in the crosshair", Vector3.Distance(Bullets.AimPoint(), LocalPlayer.CameraPosition) < 4.5f);
            var shot = Bullets.Fire(LocalPlayer.AimRay, radiusVoxels: 3);
            Check("Bullets.Fire hits the person", shot.Hit && shot.Creature?.Pointer == first.Pointer && shot.Limb != null);
            Bullets.Tracer(LocalPlayer.CameraPosition + Vector3.down * 0.2f, shot.End, Color.yellow, 0.02f, 0.3f);
            Check("Tracers show", Bullets.TracerCount >= 1);
            yield return Wait(0.5f);
            Check("A bullet wounds them", Hurt(first) > hurtFirst, $"{hurtFirst:0.000} -> {Hurt(first):0.000}");
            Check("Tracers fade away", Bullets.TracerCount == 0);
            Hang(first, 4f);
            Hang(second, 6.5f);
            hurtFirst = Hurt(first);
            var pierced = Bullets.Pierce(LocalPlayer.AimRay, radiusVoxels: 3);
            var people = pierced.Where(h => h.Creature != null).Select(h => h.Creature.Pointer).Distinct().ToList();
            Check("Bullets.Pierce goes through both people", people.Contains(first.Pointer) && people.Contains(second.Pointer), $"{pierced.Count} hits");
            yield return Wait(0.5f);
            Check("Both are wounded", Hurt(first) > hurtFirst && Hurt(second) > hurtSecond);
            var forward = LocalPlayer.Forward;
            Check("Spread stays inside its cone",
                Enumerable.Range(0, 200).All(_ => Vector3.Angle(forward, Bullets.Spread(forward, 5f)) <= 5.01f));
            Shot("bullets");
            yield return Wait(1.5f);
            Creatures.DeleteAll();
        }

        private static void Hang(AbstractCreature creature, float distance)
        {
            creature.SetFrozen(true);
            creature.TeleportTo(LocalPlayer.CameraPosition + LocalPlayer.Forward * distance);
            Physics.SyncTransforms();
        }

        private static float Hurt(AbstractCreature creature)
            => creature.IsValid() ? creature.GetLimbs().Sum(l => 1f - l.GetWholeness()) : 0f;

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
