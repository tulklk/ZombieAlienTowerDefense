# AlienDefense3D — Project Setup

Tower Defense 3D, góc nhìn top-down, portrait, target chính Android (iOS thứ yếu). Unity 6 + URP.

## 1. Unity & Packages

- Unity Editor: **6000.3.21f1**
- Render pipeline: **Universal Render Pipeline 17.3.0** (`com.unity.render-pipelines.universal`)
- TextMeshPro: bundled trong `com.unity.ugui` 2.0.0 (Unity 6 không cần package riêng)
- Input: **Input System** 1.20.0 (package mới, không dùng Input Manager cũ)
- Không có package ngoài nào khác được thêm ở Phase 10 (không Ads/IAP/Analytics/Addressables).

## 2. Scene entry point

- Scene chính: `Assets/_Game/Scenes/Levels/Level_01.unity`
- Đã có trong **Build Settings** (enabled) — trước Phase 10 scene này bị thiếu khỏi Build Settings (chỉ có scene demo template cũ, đã disabled), build Android sẽ trống. Đã fix.
- Dựng lại từ đầu qua menu Editor: `AlienDefense/Setup/4. Build Level_01 Scene Skeleton` (chạy các bước 1-9 theo thứ tự nếu là project mới, xem mục 8).

## 3. Cấu trúc thư mục

```
Assets/_Game/
  Scripts/
    Runtime/        // gameplay code, theo namespace = tên thư mục con
      Core/          Composition Root, GameFlow/GameSpeed, Restart
      Data/          LevelDefinition
      Common/        LevelBounds
      Input/         IPlayerInput + UnityInputReader
      CameraSystem/  TopDownCameraController
      Player/        PlayerController/Movement/AutoAttack
      Enemies/       EnemyController/Health/Movement/Pool, EnemyHitFlash
      Combat/        Projectile*, DamageInfo
      Towers/        TowerController, targeting/attack strategies
      Building/      BuildNode, BuildService, TowerSell/UpgradeVfx listener
      Waves/         WaveController, WaveDefinition
      Base/          BaseHealthService
      Economy/       EconomyService
      Vfx/           VfxDefinition, PooledVfx, VfxService (Phase 10)
      Audio/         AudioService (Phase 10)
      UI/            View/Presenter theo từng panel
      DebugTools/    Debug spawner/controls (ContextMenu chỉ chạy trong Editor)
    Editor/          Scaffolder + Prefab/Definition builder cho từng hệ thống
    Tests/EditMode, Tests/PlayMode
  Data/              ScriptableObject assets (Levels, Towers, Enemies, Waves, Projectiles, Vfx)
  Prefabs/           Prefab theo hệ thống (Towers, Enemies, Projectiles, Vfx, BuildNode...)
  Materials/Generated/  Material tạo tự động bởi Editor builder (không chỉnh tay)
  Settings/          URP Pipeline + Renderer asset riêng của project
```

## 4. Kiến trúc tổng quan

- **Composition Root duy nhất**: `LevelCompositionRoot` (`Runtime/Core`) — nơi duy nhất `new` các service thuần C# và gọi `Initialize(...)` lên các MonoBehaviour View/Presenter. Không Singleton, không static mutable state ở đâu trong Runtime.
- **View/Presenter**: View là MonoBehaviour "dumb" (setter public + event click), Presenter nhận dependency qua `Initialize(...)` (không dựa vào `Awake()`), Presenter/Service không đặt logic vào View.
- **Hướng phụ thuộc một chiều**: `Towers` không biết `Building`/`UI`; `Building` biết `Towers` (một chiều); `UI` biết cả hai; `Vfx`/`Audio` là namespace lá (không phụ thuộc gì khác trong project) — `Combat`/`Towers`/`Enemies`/`Building`/`Core` phụ thuộc một chiều vào `Vfx`/`Audio`, không có chiều ngược.
- **Pooling**: `EnemyPool`, `ProjectilePool`, `VfxPool` đều dùng `UnityEngine.Pool.ObjectPool<T>`, `collectionChecks: Debug.isDebugBuild` (chỉ bật ở Editor/Development build), prewarm/max size cấu hình qua Definition asset, `Clear()` khi scene unload (`LevelCompositionRoot.OnDestroy`).

## 5. Tạo Definition asset mới

Menu `AlienDefense/Setup/...` (chạy đúng thứ tự số nếu build project từ đầu):

1. `3. Create Default LevelDefinition Asset`
2. `4. Build Level_01 Scene Skeleton` — gọi toàn bộ các builder bên dưới và dựng scene
3. `6. Create Enemy Definitions And Prefabs` *(nếu tách riêng)*
4. `7. Create Projectile Definition And Prefab`
5. `8. Create Tower Definitions And Prefabs`
6. `9. Create Placeholder Vfx Prefabs And Definitions` — tạo VFX prototype (ParticleSystem đơn giản, không cần asset nghệ thuật) cho muzzle/hit/defeated/build/upgrade/sell

Muốn tạo thêm loại Enemy/Tower/Projectile/Vfx mới: dùng `CreateAssetMenu` sẵn có trên từng class Definition (chuột phải trong `Project` → `Create/AlienDefense/...`), hoặc mở rộng struct spec trong Editor builder tương ứng rồi chạy lại menu.

- **EnemyDefinition**: `Create/AlienDefense/Enemies/Enemy Definition` — cần gán Prefab, MaxHealth, MoveSpeed, RewardResource, Pool size. `DefeatedVfxDefinition` optional.
- **TowerDefinition**: `Create/AlienDefense/Towers/Tower Definition` — cần Prefab, ProjectileDefinition, ít nhất 1 TowerLevelData. `MuzzleVfxDefinition` optional.
- **WaveDefinition**: `Create/AlienDefense/Waves/Wave Definition` — danh sách EnemySpawnGroup.
- **VfxDefinition** (Phase 10): `Create/AlienDefense/Vfx/Vfx Definition` — cần một `PooledVfx` prefab (ParticleSystem con) + Lifetime + Pool size.

## 6. Tạo level mới

1. Tạo `LevelDefinition` asset mới (`Create/AlienDefense/Data/Level Definition`), gán StartingResource/BaseMaxHealth/TargetFrameRate/Waves.
2. Copy `Level_01.unity` sang scene mới, đổi `LevelCompositionRoot._levelDefinition` sang asset vừa tạo.
3. Thêm scene mới vào Build Settings (enabled, đúng thứ tự nếu cần).

## 7. Layer bắt buộc

`Ground`, `Player`, `Enemy`, `Tower`, `BuildNode`, `Projectile`, `Environment`, `WorldInteractable` (đã cấu hình sẵn trong project, index 8-15). `WorldSelectionController` raycast dựa vào layer `BuildNode`/`Tower`.

## 8. Input Actions bắt buộc

Asset `Assets/InputSystem_Actions.inputactions`, action map **`Player`** (đổi được qua `UnityInputReader._actionMapName`), action **`Move`** (Vector2, dùng cho joystick/WASD). Action map **`UI`** dùng cho `EventSystem`/`InputSystemUIInputModule`.

## 9. Android Build

- **Package Name**: `com.aliendefense3d.game` (placeholder, đổi trước khi phát hành chính thức thật).
- **Version**: 0.1.0, Bundle Version Code 1.
- **Scripting Backend**: IL2CPP. **Target Architectures**: ARM64 (bắt buộc release).
- **Graphics API**: Auto Graphics API (Vulkan ưu tiên, tự fallback OpenGLES3 theo thiết bị).
- **Strip Engine Code**: bật (giảm build size; nếu thêm reflection-heavy code sau này, kiểm tra lại Managed Stripping Level trước khi build release).
- **Color Space**: hiện đang là **Gamma** (mặc định project cũ) — URP khuyến nghị Linear cho lighting chính xác; đây là thay đổi cần xác nhận trực quan trong Editor trước khi đổi (ngoài phạm vi audit code thuần).
- Build Profile: Android, Portrait, scene `Level_01` (đã enabled trong Build Settings).
- Debug signing đủ để test; không tạo keystore release cho tới khi có yêu cầu phát hành thật.

## 10. Testing checklist (thủ công, cần thiết bị/Editor thật — không chạy được trong môi trường chỉnh sửa code này)

- [ ] Build & install Android thành công, Portrait đúng, Safe Area đúng trên máy có notch.
- [ ] Joystick + touch không click xuyên UI (build/upgrade/sell panel khi Pause).
- [ ] FPS ổn định ở wave đông nhất + 30 tower + nhiều projectile cùng lúc (đo bằng Profiler, không đoán).
- [ ] Pause/Resume, app background/foreground (`OnApplicationPause`) không làm kẹt timeScale.
- [ ] Restart level nhiều lần liên tiếp không leak (theo dõi Memory Profiler).
- [ ] Victory/Defeat, âm thanh/VFX tương ứng phát đúng 1 lần.

## 11. Known limitations (xem đầy đủ trong báo cáo Phase 10)

- VFX muzzle/hit/defeated/build/upgrade/sell dùng ParticleSystem placeholder (không phải art thật).
- AudioClip chưa được gán (project chưa có file âm thanh) — hook đã sẵn sàng, chỉ cần kéo AudioClip vào `LevelCompositionRoot` Inspector.
- Chưa có test tích hợp end-to-end cho toàn bộ `LevelCompositionRoot` trên thiết bị thật.
- Chưa profiling thật (không có Unity Editor/thiết bị Android trong môi trường thực hiện thay đổi code này).

## 12. Next development priorities

1. Thay VFX/Audio placeholder bằng asset thật, đo bằng Profiler trước khi tối ưu thêm.
2. Chạy Device Testing checklist (mục 10) trên máy low-end/mid-range thật.
3. Nếu Profiler cho thấy targeting/health-bar là bottleneck ở quy mô lớn hơn (>100 enemy/>30 tower), cân nhắc spatial grid — chưa cần ở quy mô hiện tại.
4. Main Menu, Save System, Ads/IAP — ngoài phạm vi MVP, chưa triển khai.
