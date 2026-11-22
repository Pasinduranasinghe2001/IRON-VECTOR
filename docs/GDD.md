# IRON VECTOR — Game Design Document (GDD)

## 1. High-Level Concept

**IRON VECTOR** is a modern combined-arms tactical first-person shooter inspired by *Call of Duty: Modern Warfare* gunplay and lightweight *Battlefield*-style vehicle skirmishes.

- **Core Loop**: Select loadout -> Deploy with squad -> Fight over lanes or capture objectives using infantry movement, cover, grenades, and vehicles -> Earn score & eliminate opposing team to win rounds.
- **Target Feel**: Crisp responsiveness, readable silhouettes, decisive headshots, low-clutter HUD, and balanced infantry vs vehicle counters.

---

## 2. Game Modes

| Mode | Objective | Score Limit | Round Time | Map Lanes Used |
| :--- | :--- | :--- | :--- | :--- |
| **Team Deathmatch (TDM)** | First team to reach elimination limit wins | 30 kills | 10 min | All 3 lanes |
| **Domination (DOM)** | Hold points A, B, and C to accumulate points every second | 200 pts | 10 min | North Road (A), Center Deck (B), South Cargo (C) |
| **Search & Destroy (Planned)** | 1-life tactical bomb plant/defuse | Best of 7 | 2 min / round | Infantry lanes |

---

## 3. Arsenal

### Vertical Slice Weapons (Implemented as `WeaponData`)

| Weapon | Category | Role | RPM | Base / Head Damage | Mag / Reserve |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **M4A1 Carbine** | Assault Rifle | Versatile all-range automatic | 720 | 24 / 38.4 | 30 / 120 |
| **MP5K Tactical** | SMG | Fast CQB & tight hipfire | 880 | 19 / 28.5 | 32 / 160 |
| **M24 Sniper** | Bolt Rifle | Long-range overwatch, 1-shot headshot | 52 | 75 / 165 | 5 / 25 |
| **M870 Breacher** | Shotgun | 8-pellet room clearer | 78 | 16 x 8 = 128 | 6 / 36 |
| **P226 Sidearm** | Pistol | Reliable backup finish | 420 | 26 / 45.5 | 15 / 60 |

### Equipment & Gadgets
- **Frag Grenade (Implemented)**: 2 per life, 2.2s fuse, 8.5m blast radius blocked by solid walls.
- **Planned**: Smoke Grenade, EMP/RPG-7 Anti-Armor Launcher, Deployable Ballistic Cover, UAV Radar Sweep.

---

## 4. Vehicles

- **Recon Buggy (Implemented)**: Fast 1-seat flanking & harassment platform (450 HP, raycast spring suspension, roadkill impact damage, 20s respawn timer). Vulnerable to concentrated rifle fire, grenades, and enemy ambush in the South CQB lane.
- **Armored Tactical APC (Planned)**: Driver + 360-degree gunner turret seat, high armor, countered by anti-armor launcher.

---

## 5. Map Design — *Outpost Vector*

A symmetrical 3-lane 120m x 96m compound designed for both 5v5 CQB and light vehicle flanking:

1. **North Lane (Vehicle Avenue — Capture Point A)**: Wide road with jump ramps connecting Alpha & Bravo motor pools.
2. **Center Lane (Control Deck — Capture Point B)**: Elevated steel bridge reached via flanking ramps, rewarding vertical control while remaining exposed to grenades and sniper angles.
3. **South Lane (Cargo Yard — Capture Point C)**: Tight shipping container corridors built for SMGs and shotguns.
