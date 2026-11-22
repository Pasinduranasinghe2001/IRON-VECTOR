# IRON VECTOR — Technical & Network Architecture

## 1. Scene Flow & Multi-Map Architecture

- **`MainMenu.unity` (Build Index 0)** holds the persistent `NetworkManager` (`DontDestroyOnLoad`), `NetworkGameServer`, `RoomPasswordAuthenticator`, `LanDiscovery`, `MainMenuUI`, and `GameHUD`.
- When hosting, `NetworkGameServer` issues `SceneManager.LoadGlobalScenes(new SceneLoadData(SelectedMapScene) { ReplaceScenes = ReplaceOption.All })` so Host and all clients automatically synchronize onto **`AdminHall.unity`** or **`Arena.unity`**.
- Each map scene carries a `MapConfig` + `SpawnManager` describing team/FFA spawns, vehicle pads, Domination flags, and Search & Destroy Bomb Sites (`A`/`B`).

---

## 2. Standard Multiplayer Connectivity

| Scenario | Mechanism | Implementation |
| :--- | :--- | :--- |
| **Same Network (Wi-Fi / Hotspot / LAN)** | UDP Broadcast Beacon (`47777`) | `LanDiscovery.cs` advertises host name, map, mode, player count, port, and password flag |
| **Different Networks (VPN / Public Internet)** | Shareable Invite Code + Port `7770` | `NetworkAddressHelper.cs` inspects active LAN/VPN/Public endpoints and encodes `IV-...` join codes |
| **Room Access Control** | FishNet Pre-Spawn Authentication | `RoomPasswordAuthenticator.cs` (`HostAuthenticator`) verifies room passwords via `IBroadcast` before spawning players |

---

## 3. Runtime Graphics & UI Controls

- **`GraphicsSettingsManager.cs`** dynamically updates URP `UniversalRenderPipelineAsset` (`renderScale`, `msaaSampleCount`, `mainLightShadowmapResolution`, `shadowDistance`, `shadowCascadeCount`), `QualitySettings.vSyncCount`, and `Application.targetFrameRate` across 4 presets (`Low`, `Medium`, `High`, `Ultra`).
- **`LocalProfile.ShowHud`** (`H` hotkey or Pause/Settings toggle) disables all combat HUD overlays cleanly (`UI Off`) while keeping Pause (`Esc`) available.
