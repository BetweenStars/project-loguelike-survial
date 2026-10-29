# Unity 연결 및 확장 안내

이 구현은 `GAME_VISION.md`의 기능을 제공한다. 최종 모델, UI 디자인, 프리팹과 Timeline 연출은 사용자가 교체·제작한다. Feel은 필요하지 않으며 컴포넌트의 UnityEvent에 나중에 연결한다.

## 1. 먼저 실행하기

- `Assets/SurvivalSample/Scenes/SurvivalTitle.unity`를 열어 Play한다.
- 샘플이 없다면 메뉴 **Survival → Create Cube Sample**을 실행한다. 기존 샘플이 있으면 덮어쓰지 않는다.
- Start Game → 60초 일반 전투 → 보스 → E로 상점 → Next Stage → 총 3스테이지 → Clear 순서다.
- 조작: WASD 이동, 마우스 시점, 왼쪽 버튼 총, Q 와이어, E 상점, ESC 일시정지.
- 샘플의 `PrototypeDebugUI`는 기능 확인용 IMGUI다. 최종 UI를 연결한 뒤 제거해도 게임 규칙에 영향이 없다.
- 샘플과 원래 `Assets/Scenes`의 씬은 별개다. 원래 씬의 모델·카메라·크로스헤어를 자동으로 교체하지 않는다.
- 샘플의 `SurvivalTitle`/`SurvivalGame`과 실제 `TitleScene`/`GameScene` 이름을 혼동하지 않는다. SceneNavigation 및 GameFlowController의 씬 이름을 함께 변경한다.

## 2. 폴더별 책임

| 폴더 (`Assets/Scripts/Runtime`) | 역할 |
| --- | --- |
| Core | 게임 단계, 씬 전환, 정지 사유 관리 |
| Input | 기존 Input System 입력 읽기 |
| Player | 체력·성장, 이동 담당, 크로스헤어 조준, 무기 보유 목록 |
| Grappling | 연결 대상, 훅 비행, 견인·해제·쿨타임 |
| Weapon | 총·장판·회전·참격, 무기 표시 데이터 |
| Projectile | 진영별 투사체 이동·스윕 충돌·수명 |
| Enemy | 공통 체력·드롭, NavMesh 이동, 드론 행동 |
| Enemy/Boss | 보스 패턴 선택과 개별 미사일·돌진 패턴 |
| Waves | WaveData와 스폰·보스 등장 |
| Progression | 드롭 수집, 보상 후보·적용, 레벨업 선택 |
| Shop | 상점 생성·상호작용·구매·다음 단계 |
| UI | 최종 UI에 연결할 표시 컴포넌트와 샘플 UI |
| Cursor | 게임·UI 상태에 맞는 마우스 잠금 |

기존 `Assets/Scripts/Interaction/IInteractable.cs`, `Runtime/Combat/IDamageable.cs`는 경로와 계약을 보존했다. `Assets/Scripts/Editor`에는 샘플 생성과 레벨 Inspector가 있고, `Assets/Scripts/Tests/Editor`에는 기능 검증 도구가 있다.

## 3. 기존 게임 씬에 연결하기

입력 바인딩은 `GameInputActions.inputactions`에서만 관리한다. InputReader는 바인딩을 덮어쓰지 않는다. 현재 Hook은 Q, Player/Interact는 E, UI/Pause는 ESC다. 에셋 수정 시 Generate C# Class를 활성화하고 Apply해 생성 클래스를 동기화한다.

### 플레이어 루트

기존 CharacterController와 PlayerMovement를 사용한다. 루트에 다음을 추가한다.

- `PlayerStats`: 시작 체력, 기본 이동 속도, 경험치 곡선, 피격 보호 시간.
- `PlayerMovement`: 기존 `Camera Target`을 연결한다. 위치 적용은 이 컴포넌트의 CharacterController.Move 한곳에서 수행한다.
- `PlayerAim`: 게임 카메라와 **현재 씬의 Crosshair RectTransform**을 연결한다. Screen Space Camera UI는 Canvas의 World Camera도 지정한다.
- `GrappleController`: 발사 위치, 선택적인 훅 시각 프리팹·LineRenderer를 연결한다.
- `PlayerWeaponController`: Main Weapon과 Auxiliary Weapons 목록을 연결한다.

플레이어를 프리팹으로 만들 때 카메라·무기를 내부 자식으로 두면 대부분의 참조가 유지된다. 씬 Canvas에 있는 크로스헤어는 프리팹 외부 참조이므로 씬에 플레이어를 배치한 뒤 연결한다. 카메라 시점 로직을 추가해도 플레이어 transform 위치를 별도로 변경하지 않는다.

### Cinemachine 3 Third Person Follow

- Main Camera는 플레이어/Camera Target의 자식으로 두지 않고 독립적으로 배치한다. Camera, AudioListener, CinemachineBrain을 붙인다.
- 별도 Cinemachine Camera에 Position Control을 Third Person Follow로 설정한다.
- Tracking Target에는 플레이어 자식 `Camera Target`을 지정한다. PlayerMovement.Camera Target에도 같은 Transform을 지정한다.
- PlayerMovement가 플레이어 좌우 회전과 Camera Target 상하 회전을 처리한다. 카메라 transform을 별도 추종 스크립트로 동시에 움직이지 않는다.
- 현재 입력 방식에서는 별도 Cinemachine Input Axis Controller를 추가해 같은 마우스 입력을 중복 적용하지 않는다.
- PlayerAim.Aim Camera는 Cinemachine Camera가 아니라 **CinemachineBrain이 붙은 Main Camera**다. 크로스헤어는 기존 RectTransform을 연결한다.
- 거리·어깨 위치·감쇠는 Third Person Follow의 Camera Distance, Shoulder Offset, Damping에서 조정한다. 벽 가림은 Avoid Obstacles와 지형 전용 Collision Filter로 설정한다.
- 원래 GameScene의 기존 Cinemachine 설정은 유지했다. 큐브 샘플과 샘플 생성 도구도 같은 구조를 사용한다.

### 게임 시스템 오브젝트

`Game Systems` 같은 빈 루트에 아래 컴포넌트를 배치한다. UI 패널과 함께 비활성화되는 오브젝트에 두지 않는다.

| 컴포넌트 | Inspector 필수 연결 |
| --- | --- |
| InputReader | 참조 없음. 기존 InputReader와 중복 배치하지 않는다. |
| GamePause | 참조 없음. 한 씬에 하나. |
| CursorManager | 참조 없음. 한 씬에 하나. |
| WaveManager | Player, Spawn Points |
| LevelUpController | Player, Inventory, Pause, 선택적 Reward Settings |
| ShopController | Player, Inventory, Pause, Level Up, Shop Prefab, 선택적 Reward Settings |
| GameFlowController | Player, Inventory, Movement, Wave Manager, Stages, Shop, Level Up, Pause |

`GameFlowController.Stages`는 `Wave1 → Wave2 → Wave3` 순서다. Start에서 플레이어·무기를 초기화하고 오프닝을 진행한다. 게임 씬을 바로 Play해도 작동한다.

### 타이틀 씬

- 빈 오브젝트에 `SceneNavigation`을 붙인다.
- Game Scene을 실제 게임 씬 이름으로 설정한다.
- 시작 버튼의 OnClick에 `SceneNavigation.StartGame()`을 연결한다.
- 필요하면 종료 버튼에 `QuitGame()`을 연결한다.
- 타이틀에서 커서가 잠기지 않도록 CursorManager를 쓰거나, 별도의 UI 커서 정책을 적용한다.
- **Build Profiles → Scene List**에 타이틀과 게임 씬을 모두 추가하고 출시 시 실제 타이틀을 첫 번째로 둔다.

## 4. 웨이브와 맵

Project의 **Create → Survival → Wave Data**로 에셋을 만든다.

- Enemies: 적 프리팹과 Weight. 초기값은 자폭 90, 사격 10.
- Spawn Interval: 1초, Spawn Count: 1, Max Alive Enemies: 100.
- Boss Time: 60초, Boss Prefab: 대형 로봇.
- 새 스테이지는 에셋을 복제하고 GameFlowController.Stages에 추가한다. 마지막 항목은 상점 없이 엔딩으로 진행한다.
- 상한은 일반 적에게만 적용한다. 보스는 별도로 한 번 생성한다. 밀린 일반 적을 나중에 몰아서 생성하지 않는다.
- 보스 시간이 되면 스폰 위치가 일시적으로 없어도 일반 적 생성을 중단하고 보스 위치를 재시도한다.

### NavMesh 설정

- AI Navigation 패키지는 기존 설치본을 사용한다.
- 맵에 `NavMeshSurface`를 추가한다. 지면·벽·건물 Collider가 수집되도록 Layer Mask와 Use Geometry를 정하고 Bake한다.
- 플레이어·적·투사체·드롭·UI는 NavMesh를 만드는 지형 수집에서 제외한다.
- 적 프리팹의 NavMeshAgent Agent Type과 Surface의 Agent Type을 맞춘다.
- NavMeshAgent의 Radius·Height를 실제 적 크기에 맞춘다. 이동 가능한 통로 폭을 확보한다.
- Spawn Points에는 빈 Transform들을 연결한다. 지면 가까이, 건물 내부가 아닌 위치에 둔다.
- 스포너는 NavMesh 위치와 플레이어 쪽 경로가 완전한지 확인한다. 유효한 점이 없으면 벽 안에 억지로 생성하지 않는다.
- 플레이어가 와이어로 높이 올라가면 드론은 주변 NavMesh의 지상 위치를 목표로 한다.
- 샘플에서는 Default 레이어 지형을 베이크하고 플레이어 루트는 Ignore Raycast 레이어다. 조준 레이의 자기 자신 제외는 코드로도 수행한다.

## 5. 적·보스 프리팹

### 공통 루트

- Enemy: 체력, Experience Prefab, Money Prefab, 각각의 보상량.
- Collider: 몸체 피격 판정. 자식 Collider도 Enemy까지 부모를 찾아 처리한다.
- NavMeshAgent + EnemyChase: 지상 이동. EnemyChase에 속도·정지 거리를 설정한다.
- 바닥 기준 피벗을 권장한다. 모델만 자식으로 높이를 조절한다.

자폭형은 `ExplodingDrone`을 추가하고 접근 거리·준비 시간·폭발 반경·피해를 설정한다. 폭발 준비 후 실제 폭발 순간의 거리로 피해를 계산한다.

사격형은 `ShootingDrone`을 추가하고 Projectile Prefab·Fire Point·사거리·간격·피해를 설정한다. 사거리 밖이면 접근하고 안이면 정지 사격한다. 별도의 후퇴나 측면 이동은 없다.

보스는 공통 루트에 `BossController`, `BossMissilePattern`, `BossChargePattern`을 붙인다. BossController.Patterns 목록에 패턴을 등록한다. Selection Weight로 등장 비중을, Rest Time으로 패턴 사이 휴식을 조절한다.

새 보스 패턴은 `BossPattern`을 상속하고 `Execute(Enemy boss, EnemyChase movement)` 코루틴을 구현한다. 필요하면 `Cancel()`에서 패턴 소유 효과를 정리한다. 패턴 코드는 플레이어 사망·보스 소멸을 고려하고, 보스 위치 이동에는 NavMesh 이동을 사용한다. `Started`, `Finished`, 돌진의 `Telegraph`에 예고·피드백을 연결한다.

`Enemy.Die()`는 보상을 드롭하는 사망이다. 상점 낙하 파동에 닿은 잡몹은 이 메서드로 처치한다. 자식 Collider는 부모의 Enemy를 찾아 처리한다. `DespawnWithoutReward()`는 보상 없이 제거해야 하는 관리용 처리에 사용한다.

## 6. 무기와 투사체

무기는 플레이어 아래 별도 자식 GameObject로 미리 배치한다. 무기 컴포넌트를 플레이어 루트에 붙이면 미획득 시 플레이어까지 꺼질 수 있으므로 피한다.

- Main Weapon: 총 1개. 슬롯에 포함되지 않으며 시작 시 레벨 1.
- Auxiliary Weapons: 장판·회전·참격 및 추가 무기 후보 전체. 현재 한도는 3개.
- 획득 전에는 비활성, 획득 후 레벨 1로 활성화된다.
- Inspector에 각 강화 배열이 **Level 1~7**로 표시된다.

| 무기 | 연결·설정 | 레벨별 변화 |
| --- | --- | --- |
| MainWeapon | Projectile Prefab, Fire Point, PlayerAim | Damage By Level |
| SlowFieldWeapon | 선택적 Field Visual, Speed Multiplier | Radius By Level |
| OrbitWeapon | OrbitBlade 프리팹, 반경·회전속도·피해·피해 간격 | Count By Level |
| SlashWeapon | Projectile 프리팹, Fire Point, PlayerMovement | Interval By Level |

MainWeapon의 프리팹은 루트에 Projectile이 있어야 한다. 주무기 드론 모델은 무기 자식으로 두면 플레이어를 따라간다. 별도의 장식용 추종·회전 애니메이션은 시각 자식에 추가한다.

OrbitBlade는 실제 Collider와 Kinematic Rigidbody를 사용한다. 이 Collider가 적과 접촉해야 피해가 들어가며 같은 적의 여러 Collider는 피해 간격으로 중복 차단한다. 회전 오브젝트 크기를 바꾸면 판정도 바뀐다.

Projectile은 이동 구간을 SphereCast하고 시작 겹침도 검사한다. 물리 Rigidbody로 또 이동시키지 않는다. 시각 모델만 있는 프리팹으로도 작동하며 Hit Radius로 판정을 정한다. Collision Mask에 적·플레이어·벽 레이어를 포함한다. 일반 총알은 첫 타격에 제거되고 참격은 적을 관통하되 같은 적은 한 번만 맞히며 벽이나 수명 종료로 제거된다.

`Create → Survival → Weapon Display Data`에서 이름·설명·아이콘·획득 가격·강화 가격을 만들고 무기에 연결한다. 실제 레벨별 전투 수치는 무기 컴포넌트에 있다.

새 무기는 Weapon 상속 → Attack과 필요한 ApplyLevel 구현 → 프리팹 자식 배치 → Auxiliary Weapons 등록 순서다. 보유·상점·레벨업 후보 처리를 새로 작성할 필요는 없다. 기존 CanAttack 조건을 사용해 비활성·정지·컷씬 중 공격을 막는다.

## 7. 와이어 조절

GrappleController의 그룹별 설정:

- Connection: Launch Point, Obstacle Mask, Grapple Layers, 거리·도착 거리.
- Hook Flight: 훅 시각 프리팹, Rope, Flight Duration(0.25초), Flight Curve, Attachment Delay.
- Pull / Ground Drag / Lift: Pull Speed, Acceleration Time, Ground Drag Time(0.15초), Lift Time·Curve, Release Inertia.
- Protection / Cooldown: 견인 전체 무적, 초기 보호 시간(대체 모드), Cooldown(2초).

벽·건물에는 GrappleTarget을 붙이거나 Grapple Layers에 포함시킨다. Obstacle Mask에는 가로막는 벽도 포함한다. 적은 연결 대상에서 제외된다.

훅이 날아갈 때는 몸이 움직이지 않고, 기존 WASD·중력이 작동한다. 훅 부착 후 짧은 지연을 거쳐 견인한다. 높은 목표에 지상에서 시작하면 먼저 바닥을 따라 끌리고 점차 위로 향한다. 공중이거나 낮은 목표라면 불필요한 지상 구간을 생략한다.

견인 중 WASD·일반 중력은 중단하고 시점·공격을 허용한다. 버튼을 놓으면 약한 관성만 남고 일반 이동으로 복귀한다. 목표 도착·막힌 충돌·대상 소실에도 해제된다. 자동으로 옥상 위로 올라타거나 진자 스윙을 하지는 않는다.

무적은 실제 견인 구간만이다. 훅 비행·해제 관성은 무적이 아니다. PlayerStats의 Hit Protection Duration(0.5초)은 별도의 피격 후 보호이며 두 보호가 서로를 해제하지 않는다.

훅·로프에는 플레이어 이동을 방해하는 Collider를 넣지 않는다. 보이는 로프는 LineRenderer로 대체 가능하다. Fired, Attached, PullStarted, Released와 CooldownChanged에 Feel·음향·UI를 연결한다.

## 8. 드롭·보상·경제

Pickup 프리팹에 시각 모델과 Pickup 컴포넌트를 둔다. 물리 Collider는 필수가 아니다. 경험치와 돈은 각각 다른 프리팹을 Enemy에 연결한다. 종류·양·수집 대상은 적이 생성 시 전달한다.

`Reward Settings` SO에서 속도 퍼센트·최대 체력 증가량·가격 기본값을 수정한다. 무기의 Display Data가 있으면 해당 무기 가격이 우선한다.

- 속도는 기본값 × (1 + 누적 퍼센트 / 100).
- 최대 체력 증가 시 현재 체력 유지.
- 상품별 동일 비중, 화면 내 중복 없음.
- 상점은 각 칸 1회씩 모두 구매 가능. 닫았다 열어도 상품과 구매 상태 유지.
- 레벨업은 3개 이하 중 1개 선택. 새 무기 없음.
- 보유 한도·강화 한도·재화를 적용 직전에 확인한다.
- 1·2스테이지는 보스 처치 시 경험치·돈을 자동 수집하지 않는다. 상점 낙하 파동으로 죽은 잡몹도 보상을 드롭하며, 기존 드롭과 함께 플레이어가 직접 접근해 수집한다. 수집으로 레벨업 선택이 발생하면 선택을 먼저 마친 뒤 상점을 연다. 상점이 없는 마지막 스테이지는 기존 정리·자동 수집과 엔딩 우선 규칙을 유지한다.

## 9. 최종 UI 연결

### 보상 카드와 패널

1. 카드 프리팹에 RewardChoiceView를 붙이고 TMP Title, TMP Price, Image Icon, Button을 연결한다.
2. 상점용·레벨업용 패널에 카드 3개씩 배치한다.
3. 항상 활성 상태인 UI 관리 루트에 RewardPanelView 두 개를 두고 각 Panel Root와 Choices를 연결한다.
4. 상점용은 Show Shop 활성화, 레벨업용은 비활성화한다.
5. 각각 Shop, Level Up, Player, Inventory를 연결한다.
6. 카드의 Button은 RewardChoiceView가 리스너를 등록하므로 같은 구매 함수를 중복 연결하지 않는다.

직접 UI를 구현하려면 ShopController.Offers / LevelUpController.Offers와 Changed 이벤트를 사용하고 `Buy(index)` / `Choose(index)`를 호출한다.

### 버튼 연결표

| 버튼 | 대상 메서드 |
| --- | --- |
| 타이틀 시작 | SceneNavigation.StartGame |
| 상점 닫기 | ShopController.Close |
| 다음 스테이지 | ShopController.NextStage |
| ESC 메뉴 계속 | GamePause.Resume |
| 타이틀 복귀·클리어 복귀 | GameFlowController.ReturnToTitle |

PlayerHudView에 필요한 TMP_Text·Slider만 연결한다. 체력, 경험치, 레벨, 돈, 스테이지, 시간, 보유 무기, 보스 체력, 와이어 쿨타임을 지원한다. 안 쓰는 표시 참조는 비워도 된다. 기존 HP Bar·Exp View·Money Text·Crosshair를 재사용할 수 있다.

GamePanelsView는 일시정지·클리어·HUD 패널 표시를 관리한다. 이 컴포넌트를 자신이 꺼야 하는 패널 안에 두지 않는다. 레벨업·상점 정지는 GamePause가 사유별로 관리하므로 UI에서 Time.timeScale을 직접 1로 바꾸지 않는다.

## 10. Timeline과 Feel 확장

- GameFlowController에 Opening / Ending PlayableDirector를 연결한다.
- 실제 Timeline은 사용자 제작. 재생 완료 시 다음 단계로 넘어간다.
- 아직 없으면 Advance Without Timeline을 켜 기능 검증을 진행한다.
- 정지가 Timeline에도 적용되도록 GameTime으로 재생한다.
- 현재 스킵 기능은 없다. 특별한 연출 시스템과 연결할 때 CompleteOpening / CompleteEnding을 사용할 수 있다.
- UI 연결에는 UnityEvent, 코드 구독에는 C# 이벤트를 사용한다. 같은 결과를 두 경로로 중복 실행하지 않는다.
- 적 피격·사망, 무기 발사·획득·강화, 보스 예고, 상점 등장, 와이어 각 단계에 피드백을 붙일 수 있다.
- 상점 낙하 파동은 퍼지면서 닿은 잡몹을 `Enemy.Die()`로 순차 처치하고 보상을 드롭하도록 연결한다. 1·2스테이지의 `GameFlowController.OnBossDefeated`에서는 즉시 전체 적 제거와 `Pickup.CollectAll(player)` 호출을 사용하지 않는다. 파티클 크기에 맞춘 판정 범위와 충돌 설정을 확인한다. 마지막 스테이지는 상점 파동을 생성하지 않으므로 기존 정리·자동 수집을 별도로 처리한다.

## 11. 검증 실행

- 메뉴 **Survival → Run Functional Checks**는 큐브 샘플에서 Play Mode로 실제 기능을 검증한다.
- 검증 중에는 실제 게임을 조작하지 않는다. 완료되면 Play Mode를 종료한다.
- 결과는 `Documentation/FUNCTIONAL_CHECKS.txt`에 저장된다.
- Unity 배치 실행: `-batchmode -nographics -projectPath <프로젝트> -executeMethod SurvivalValidation.RunAll -logFile <로그>`.
- RunAll에는 `-quit`를 붙이지 않는다. 비동기 Play Mode 검증이 끝나면 도구가 종료 코드를 반환한다.
- 수치 감각, 최종 아트·UI 레이아웃, 실제 Timeline 연출은 사용자 에셋 연결 후 직접 확인한다.
