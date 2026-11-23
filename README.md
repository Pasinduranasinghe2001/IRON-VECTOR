# IRON VECTOR

**IRON VECTOR** is a combined-arms multiplayer tactical first-person shooter inspired by **Call of Duty 4: Modern Warfare**, built with **Unity 6 (`6000.6.10f1`)**, **URP**, and **FishNet 4.7.4R**.

---

## Key Features

- **Call of Duty 4: Modern Warfare–Inspired Arsenal (9 Weapons)**
  - **Primaries**: **M4A1 Carbine**, **AK-47**, **MP5**, **P90** (50-rd), **M40A3** (Bolt Sniper), **W1200** (8-pellet Pump Shotgun), **M249 SAW** (100-rd LMG)
  - **Sidearms**: **M9** (9mm) & **Desert Eagle .50**
  - Crisp **CAD-style multi-part 3D PBR weapon models** (`URP Lit` brushed steel, anodized metal, knurled grip, walnut wood, translucent P90 mag, illuminated Holo reticle & 3-dot iron sights), animated bolt/slide/pump cycling, mouse-look sway, ADS alignment, cylindrical brass shell ejections, and procedural SFX
- **4 Tactical Strategies (Loadout Perks)**
  1. **Assault · Stopping Power** — `+15%` bullet damage
  2. **Recon · Ghost (UAV Jammer)** — Hidden from enemy radar sweeps & tighter moving hipfire
  3. **Rusher · Speed & Sleight of Hand** — `+18%` sprint/move speed & `-35%` reload time
  4. **Support · Juggernaut & Danger Close** — `125` Max HP armor, faster regen & `+25%` frag blast radius/damage
- **4 Game Modes**
  - **Team Deathmatch (TDM)** — 30-kill race
  - **Search & Destroy (S&D)** — Single-life rounds, Alpha plants (`Hold F`) at Site **A/B**, Bravo defuses before 35s detonation, teammate spectating, first to 4 rounds
  - **Domination (DOM)** — Capture & defend Zones **A / B / C** to 200 pts
  - **Free For All (FFA)** — Every Operator solo to 20 kills
- **Playable Maps**
  - **`AdminHall`** — 3-story (**`G` / `1F` / `2F`**) Administration Block inspired by the **Faculty of Engineering, University of Jaffna (Kilinochchi)** featuring cream stucco wings, terracotta sun-shading cornices, blue glazing bands, covered South columned portico, open central skylight atrium, twin stairwells, green campus lawns, and **2 working networked elevators** (**Middle Elevator** + **Corner Elevator**)
  - **`Outpost Vector` (`Arena`)** — Open 3-lane combined-arms compound with drivable **Recon Buggies**
- **Standard Multiplayer Connection Hub (Same Network + Different Networks)**
  - **Same Wi-Fi / Hotspot / Wired LAN**: Automatic UDP discovery on port `47777` (zero IP typing — 1-click **JOIN**)
  - **Different Networks / Friends Online**: 1-click shareable **Join Code (`IV-...`)** selector (Wi-Fi / LAN / VPN Tunnel / Public IP) + optional **Room Password Authentication** (`RoomPasswordAuthenticator`)
  - **Dedicated Headless Server CLI**:
    ```powershell
    "IRON VECTOR.exe" -batchmode -nographics -server -map AdminHall -mode snd -port 7770 -pass secret123
    ```
- **Tactical Pre-Match Lobby UI & Graphics Settings**
  - **Call of Duty–Inspired Pre-Match Lobby**: Edge-anchored frosted-glass overlay (`PLAY`, `WEAPONS`, `STRATEGIES`, `CHALLENGES`, `SETTINGS`), live 3D night helipad backdrop with idling transport helicopter & 3D Tactical Operator holding your live weapon, drag-to-rotate 3D gun inspect, and graphical map selection cards
  - Live **Graphics Quality Presets** (`Low`, `Medium`, `High`, `Ultra` scaling URP renderScale, MSAA, cascades & shadow resolution)
  - FPS Cap (`60 / 120 / 144 / Unlimited`), VSync, FOV, Mouse Sensitivity, FPS Counter
  - **HUD / UI Off Mode**: Press **`H`** anytime (or toggle in Settings/Pause) to hide all combat HUD overlays

---

## Quick Start (Unity 6)

1. Open this project folder (`D:\projects\Shooting Multiplayer Game`) in **Unity Hub** using **Unity 6.6 (`6000.6.10f1`)**.
2. Click **`Iron Vector -> Build Arena & Prefabs (One-Click Setup)`** (or accept the first-open dialog) to generate all weapon assets, prefabs, PBR textures, **`MainMenu.unity`**, **`AdminHall.unity`**, and **`Arena.unity`**.
3. Open `Assets/IronVector/Scenes/MainMenu.unity` and press **Play (`▶`)**.
   - Choose **AdminHall** or **Outpost Vector**, select any of the **4 Game Modes**, customize your COD4 weapon + Strategy under **Loadout**, and click **HOST MATCH**.

### Controls

| Key | Action |
| :--- | :--- |
| `W A S D` | Move / Drive |
| `Shift` | Sprint |
| `C` / `Ctrl` | Crouch (press while sprinting to Slide) |
| `Space` | Jump / Vehicle Handbrake / Next Spectate Target |
| `LMB` / `RMB` | Fire / Aim Down Sights (ADS) |
| `R` | Reload |
| `1` / `2` / `Scroll` | Switch Primary / Sidearm |
| `G` | Throw Frag Grenade |
| `F` | Call/Ride Elevator (`G`/`1F`/`2F`), Plant/Defuse Bomb (S&D), or Enter/Exit Vehicle |
| `H` | Toggle HUD / UI On & Off |
| `Tab` | Scoreboard |
| `Esc` | Pause / Graphics & Loadout Settings / Disconnect |
