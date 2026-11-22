# IRON VECTOR

**IRON VECTOR** is a combined-arms multiplayer tactical first-person shooter built with **Unity 6 (6000.3 LTS)**, **URP**, and **FishNet 4.7.4**.

Players fight across a 3-lane combat compound featuring infantry CQB, sniper overwatch lanes, frag grenades, objective capture zones, and drivable **Recon Buggies**.

---

## Tech Stack

- **Engine**: Unity 6.3 LTS (`6000.3.0f1`)
- **Render Pipeline**: Universal Render Pipeline (URP 17.3.0) + ACES Post-Processing
- **Networking**: [FishNet 4.7.4](https://github.com/FirstGearGames/FishNet) (Tugboat UDP transport, server-authoritative combat & match rules)
- **Input**: Unified wrapper (`IVInput`) supporting both Unity Input System (`1.11.2`) and Legacy Input Manager

---

## Current Vertical Slice Features

- **Multiplayer Lobby & Modes**
  - Host Match, Join by IP/Port, or launch a **Dedicated Server** (`-server -port 7770 -mode dom|tdm`)
  - **Team Deathmatch** (30-kill target, 10-minute rounds, automatic round restart)
  - **Domination** (3 capture zones `A`, `B`, `C`, contested capture progress, 200-point limit)
- **Player Movement & Gunplay**
  - Walk, sprint, crouch (with synced remote capsule/hitbox shrink), tactical slide (`Shift + C/Ctrl`), jump
  - Recoil & recovery, dynamic hip/ADS spread, sniper scope zoom, procedural 3D weapon viewmodels, spatial procedural gunfire/explosion/hit SFX
  - **5 Weapons** (editable as `WeaponData` ScriptableObjects):
    - **M4A1 Carbine** (Assault Rifle)
    - **MP5K Tactical** (SMG)
    - **M24 Sniper** (Bolt-Action Rifle, 2.2x headshot multiplier)
    - **M870 Breacher** (8-pellet Pump Shotgun)
    - **P226 Sidearm** (Secondary Pistol)
  - **Frag Grenades** (`G`, 2 per life, server-simulated explosion with line-of-sight wall blocking)
- **Vehicles**
  - Drivable **Recon Buggy** with raycast suspension, lateral tire grip, handbrake, chasing camera, roadkill impacts, armor health, smoke damage state, explosion + automatic respawn
  - Seamless FishNet ownership transfer on entry/exit and ownership cleanup if a driver disconnects
- **Combat HUD**
  - Dynamic crosshair / sniper reticle, directional damage indicator, colored hitmarkers, tactical radar, killfeed, `Tab` scoreboard, death countdown & pause menu (`Esc`)

---

## Quick Start (Unity 6)

1. **Install Unity 6.3 LTS** (`6000.3.0f1` or newer `6000.3.x`) via **Unity Hub**.
2. Open this repository folder in Unity Hub (`Add` -> `Add project from disk`).
   - Unity will automatically fetch `FishNet 4.7.4`, `URP`, and `Input System` from `Packages/manifest.json`.
3. When the editor finishes compiling, click the dialog prompt **Build Arena & Prefabs** (or select **`Iron Vector -> Build Arena & Prefabs (One-Click Setup)`** from the top menu bar).
   - This creates the URP pipeline asset, `WeaponData` assets, `Player.prefab`, `ReconBuggy.prefab`, `MatchManager.prefab`, `CapturePoint.prefab`, and `Assets/IronVector/Scenes/Arena.unity`.
4. Open `Assets/IronVector/Scenes/Arena.unity` and press **Play**, then click **HOST MATCH**.

### Testing Multiplayer Locally

- Install **Multiplayer Play Mode** (`com.unity.multiplayer.playmode`) from **Window -> Package Manager -> Unity Registry** to run 2–4 virtual player windows inside one Unity Editor (`Window -> Multiplayer Play Mode`), OR
- Build an executable (`File -> Build Profiles -> Build`):
  - **Dedicated Headless Server**:
    ```powershell
    "IRON VECTOR.exe" -batchmode -nographics -server -port 7770 -mode dom
    ```
  - **Auto-Connect Client**:
    ```powershell
    "IRON VECTOR.exe" -connect 127.0.0.1 -port 7770
    ```

---

## Controls

| Key | Action |
| :--- | :--- |
| `W A S D` | Move / Drive |
| `Shift` | Sprint |
| `C` / `Ctrl` | Crouch (press while sprinting to Slide) |
| `Space` | Jump / Vehicle Handbrake |
| `LMB` / `RMB` | Fire / Aim Down Sights (ADS) |
| `R` | Reload |
| `1` / `2` / `Scroll` | Switch Primary / Sidearm |
| `G` | Throw Frag Grenade |
| `F` | Enter / Exit Vehicle |
| `Tab` | Scoreboard |
| `Esc` | Pause Menu / Settings / Disconnect |

---

## Project Layout

```text
Assets/IronVector/
├── Editor/
│   └── IronVectorProjectSetup.cs      # Generates URP, weapon assets, prefabs & Arena scene
└── Scripts/
    ├── Core/                          # Combat contracts, FX/audio helpers, input, profile, spawn & server bootstrap
    ├── GameMode/                      # MatchManager, CapturePoint (TDM & Domination)
    ├── Player/                        # Identity, Health, Motor, Look, Hitbox, VehicleInteraction
    ├── UI/                            # MainMenuUI, GameHUD
    ├── Vehicles/                      # VehicleController, VehicleHealth
    └── Weapons/                       # WeaponData, WeaponDatabase, WeaponController, GrenadeThrower
docs/
├── ARCHITECTURE.md                    # Networking, ownership & hit detection design
└── GDD.md                             # Full vision, modes, weapons & roadmap
```

---

## Roadmap

- [x] Core FishNet 4.7.4 networking, TDM & Domination match loop
- [x] Movement (sprint, crouch, slide, jump) & 5-weapon arsenal + frag grenades
- [x] Drivable Recon Buggy with suspension & ownership handoff
- [x] One-click URP Arena generator (`Outpost Vector`)
- [ ] Lag-compensated hitbox rollback (`TimeManager` tick rewind)
- [ ] Gunsmith attachment system (optics, barrels, grips, suppressors)
- [ ] APC transport with roof turret & anti-vehicle rocket launcher
- [ ] Squad roles, ping system, UAV & tactical gadgets
- [ ] Custom 3D weapon/character models, PBR materials & sound banks
