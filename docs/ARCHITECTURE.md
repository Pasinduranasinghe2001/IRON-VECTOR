# IRON VECTOR — Technical & Network Architecture

## 1. Authority Model (FishNet 4.7.4)

IRON VECTOR uses **Server-Authoritative Gameplay** paired with **Responsive Client Motion**:

| System | Authority | Sync Mechanism |
| :--- | :--- | :--- |
| **Player Movement & Pitch** | Owning Client | `CharacterController` -> root & `CameraPivot` `NetworkTransform` |
| **Shooting & Hit Detection** | Client cast + Server validation | `ServerFire` validates cooldown, eye origin proximity, distance & target tolerance -> `IDamageable.ServerTakeDamage` |
| **Health, Regen & Respawn** | Server | `PlayerHealth.Health` & `Dead` `SyncVar<T>`, `[TargetRpc]` for hitconfirm, damage angle & respawn teleport |
| **Vehicles** | Dynamic (`IsController`) | Unoccupied: Server holds steady. Occupied: Driver receives `GiveOwnership` and runs physics; `OnPreDestroyClientObjects` reclaims ownership if driver disconnects |
| **Match & Domination Zones** | Server | `MatchManager` & `CapturePoint` use `SyncVar<T>` + `[ObserversRpc]` for killfeed broadcasts |

---

## 2. Core Data Flow

### Shooting Pipeline
1. `WeaponController` on owning client checks ammo, fire interval, and calculates dynamic spread (hip vs ADS + movement + crouch).
2. `CastBullet` runs `Physics.RaycastNonAlloc` sorted by distance:
   - Ignores shooter's own colliders (`IsChildOf(transform)`).
   - Enters player's `CharacterController` and continues up to `0.65m` looking for an interior `Hitbox` (`isHead = true`) trigger so headshots cleanly resolve even through the outer capsule.
3. Client spawns instant local muzzle flash, tracer, recoil, and audio, then sends `HitReport[]` via `[ServerRpc] ServerFire`.
4. Server validates rate limit, distance, and hit proximity before calling `IDamageable.ServerTakeDamage`, then relays visuals to other players via `[ObserversRpc(ExcludeOwner = true)]`.

### Vehicle Ownership & Disconnect Safety
1. Client presses `F` near an empty `VehicleController`.
2. `PlayerVehicleInteraction` calls `[ServerRpc] ServerEnter`.
3. Server assigns `Driver.Value = driver.NetworkObject`, transfers FishNet ownership (`vc.GiveOwnership(driver.Owner)`), and sets `CurrentVehicle.Value`.
4. Non-controlling peers set `Rigidbody.isKinematic = true` and track `NetworkTransform`.
5. On disconnect, `VehicleController.OnPreDestroyClientObjects` clears the driver and calls `RemoveOwnership()` before FishNet tears down client-owned objects so the buggy remains in play.

---

## 3. Extending the Project

- **Add a new weapon**: `Right-Click -> Create -> Iron Vector -> Weapon Data`, configure stats, and append it to `Assets/IronVector/ScriptableObjects/Weapons/WeaponDatabase.asset`.
- **Add spawn points or capture zones**: Place empty `Transform` markers in your scene and wire them into `SpawnManager` (`alphaSpawns`, `bravoSpawns`) and `NetworkGameServer` (`vehicleSpawns`, `capturePoints`).
- **Upgrading hit registration**: Extend `ServerFire` with FishNet collider rollback when adding high player-count dedicated servers.
