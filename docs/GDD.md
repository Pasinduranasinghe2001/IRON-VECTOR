# IRON VECTOR — Game Design Document (GDD)

## 1. Vision

**IRON VECTOR** brings *Call of Duty 4: Modern Warfare* tactical infantry gunplay, iconic weapon silhouettes, Search & Destroy round tension, and lightweight combined-arms vehicle combat into a fast, extensible Unity 6 + FishNet multiplayer game.

---

## 2. Game Modes (4 Implemented)

| Mode | Rules | Win Condition | Respawns |
| :--- | :--- | :--- | :--- |
| **Team Deathmatch** | Alpha vs Bravo eliminations | 30 kills | 3.5s auto-respawn |
| **Search & Destroy** | Alpha plants bomb (`F`) at Site A/B; Bravo eliminates or defuses (`F`) within 35s | First to 4 rounds | Eliminated players spectate allies until next round |
| **Domination** | Hold Capture Points A, B, C to accumulate points/sec | 200 points | 3.5s auto-respawn |
| **Free For All** | Solo operators | 20 eliminations | 3.5s auto-respawn farthest from enemies |

---

## 3. COD4-Inspired Arsenal

| Slot | Weapon | Class | RPM | Base / HS Dmg | Mag / Reserve | Role |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| Primary 0 | **M4A1 Carbine** | AR | 780 | 25 / 38.8 | 30 / 150 | Versatile precision rifle |
| Primary 1 | **AK-47** | AR | 650 | 34 / 54.4 | 30 / 120 | High stopping power |
| Primary 2 | **MP5** | SMG | 820 | 24 / 36.0 | 30 / 150 | Fast CQB mobility |
| Primary 3 | **P90** | SMG | 920 | 20 / 30.0 | 50 / 150 | Sustained spray & tight hipfire |
| Primary 4 | **M40A3** | Sniper | 50 | 88 / 193.6 | 5 / 25 | 1-shot headshot quick-scope |
| Primary 5 | **W1200** | Shotgun | 76 | 20x8 (160) | 7 / 35 | Room clearing |
| Primary 6 | **M249 SAW** | LMG | 750 | 28 / 42.0 | 100 / 200 | Lane suppression |
| Sidearm 0 | **M9** | Pistol | 480 | 28 / 49.0 | 15 / 60 | Fast finisher |
| Sidearm 1 | **Desert Eagle** | Pistol | 290 | 52 / 104.0 | 7 / 35 | Heavy .50 cal hand cannon |

---

## 4. 4 Tactical Strategies

1. **Assault · Stopping Power**: `+15%` bullet damage.
2. **Recon · Ghost (UAV Jammer)**: Invisible to enemy radar when firing + reduced moving hipfire bloom.
3. **Rusher · Speed & Sleight of Hand**: `+18%` movement/slide speed, `-35%` reload duration, `+25%` ADS speed.
4. **Support · Juggernaut & Danger Close**: `125` Max HP armor, faster health regen, `+25%` grenade radius & blast damage.

---

## 5. Maps

1. **`AdminHall`** (Kilinochchi Faculty of Engineering–Inspired Admin Block): Three accessible stories (**Ground Floor `G`**, **1st Floor `1F`**, **2nd Floor `2F`**) with South columned portico, cream & terracotta louvered exterior, central skylight atrium, twin stairwells, campus lawns, multi-floor Domination / S&D objectives, and **2 working networked elevators** (**Middle** and **Corner**).
2. **`Outpost Vector` (`Arena`)**: Open 3-lane combined-arms compound featuring drivable Recon Buggies, center warehouse deck, and South CQB cargo yard.
