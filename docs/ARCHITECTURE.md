# IRON VECTOR — Technical & Network Architecture

## 1. Scene Flow & Multi-Map Architecture

- **`MainMenu.unity` (Build Index 0)** holds the persistent `NetworkManager` (`DontDestroyOnLoad`), `NetworkGameServer`, `RoomPasswordAuthenticator`, `LanDiscovery`, `MainMenuUI`, and `GameHUD`.
- When hosting, `NetworkGameServer` issues `SceneManager.LoadGlobalScenes(new SceneLoadData(SelectedMapScene) { ReplaceScenes = ReplaceOption.All })` so Host and all clients automatically synchronize onto **`AdminHall.unity`** or **`Arena.unity`**.
- Each map scene carries a `MapConfig` + `SpawnManager` describing team/FFA spawns, vehicle pads, **elevator spawn points** (`elevatorSpawns` spawning `Elevator.prefab` at runtime), Domination flags (`CapturePoint` with vertical floor filtering), and Search & Destroy Bomb Sites (`A`/`B`).
- **`NetworkElevator.cs` + `PlayerElevatorInteraction.cs`** synchronize 3-floor (`0.0m`, `4.2m`, `8.4m`) elevator cab state (`CurrentFloor`, `TargetFloor`, `IsMoving`) via `SyncVar<T>` and `[ServerRpc]`, while `PlayerMotor` applies `NetworkElevator.FrameDelta` so riders move smoothly across floors.

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
- **`WeaponModelBuilder.cs`** procedurally builds multi-part 3D CAD meshes (45° chamfered receivers, extruded curved magazines, sculpted stocks/grips, MIL-STD-1913 Picatinny rails, illuminated optics/reticles, first-person gloved arms/hands, and `URP Lit` PBR materials) for all 9 weapons.
- **`SoldierVisualBuilder.cs` & `WorldPropsBuilder.cs`** assemble articulated multi-part 3D Tactical Operators (combat helmet, dual NVGs, plate carrier, pouches, radio, stride animation, team uniforms) and clear skyboxes + exterior tropical trees, coconut palms, flowerbeds, and campus flagpoles.
- **`MainMenuUI.cs` + `TacticalGuiTheme.cs` + `LobbyStageController.cs`** render a COD4-inspired frosted-glass tactical pre-match lobby (scanline backing, corner brackets, circular compass radar, circular Mil-Dot scope mask, brightness/ADS-sensitivity/crosshair-color controls) over a well-lit 3D helipad, helicopter, and operator showcase.
