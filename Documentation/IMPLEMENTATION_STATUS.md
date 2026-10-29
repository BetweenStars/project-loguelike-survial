# 구현 및 검증 결과

- 기획 기준: GAME_VISION.md.
- 기능 구현, 큐브 샘플, NavMesh, 웨이브 SO 3개, UI 표시·버튼 연결 컴포넌트, 연결 설명서를 제공한다.
- 기존 Assets/Scenes의 씬을 대체하지 않았다. 샘플은 Assets/SurvivalSample 아래에 있다.
- 실행 시작: Assets/SurvivalSample/Scenes/SurvivalTitle.unity에서 Play.
- 연결 설명서: Documentation/UNITY_SETUP.md.
- 기능 검증 소스: Assets/Scripts/Tests/Editor/SurvivalValidation.cs.

## 검증

1. Unity 6000.4.0f1에서 스크립트 컴파일 및 큐브 샘플·NavMesh 생성 성공.
2. 실제 Unity Play Mode 자동 기능 검증 54개 통과. 상세 결과는 FUNCTIONAL_CHECKS.txt.
3. 이후 가독성 정리, 플레이어 상위 계층 변경 시 투사체 소유자 판정 보완, 입력 바인딩 Inspector 노출, 상점 비활성화 시 정지 사유 정리 및 테스트 파일 이동을 수행했다.
4. 최종 런타임·에디터 소스는 설치된 Unity 어셈블리를 참조한 C# 컴파일을 통과했고 포매팅 검증을 통과했다.
5. 최종 정리 뒤 추가 Unity 재실행은 실행 승인이 거절되어 수행하지 않았다. 54개 Play Mode 검증 결과는 3번 정리 이전 실행 결과다.

## 사용자 연결 범위

- 최종 UI 디자인과 프리팹 교체, 실제 Timeline·Feel 연출은 사용자가 연결한다.
- 샘플 IMGUI는 기능 확인용이며 실제 UI는 UI 폴더의 View 컴포넌트로 연결한다.
- 사용자 제작 맵에서는 콜라이더, NavMesh 베이크, Spawn Points를 연결해야 한다.
- 실제 조작감과 최종 연출·시각 품질은 사용자 에셋 연결 후 플레이로 조정한다.

## 2026-09-07 상점 파동 규칙 변경

- 확정 기획: 1·2스테이지 상점 낙하 파동에 닿은 잡몹은 보상을 드롭하며 사망한다. 기존 보상과 새 보상 모두 플레이어가 직접 수집한다.
- 현재 `WaveHitDetector`는 `Enemy.Die()`를 호출한다. `GameFlowController.OnBossDefeated`에는 아직 `Pickup.CollectAll(player)`가 공통 경로에 남아 있어, 1·2스테이지 자동 수집을 제외하는 코드 반영이 필요하다.
- 마지막 스테이지는 상점이 없으며 기존 보상 없는 적 제거·자동 수집·엔딩 우선 규칙을 유지한다. 현재 전체 적 제거 호출이 주석 처리되어 있어 마지막 스테이지 정리 경로도 확인이 필요하다.
- 이번 변경은 문서에만 반영했다. 위의 과거 검증 결과는 변경된 파동·수집 규칙을 검증한 결과가 아니다.
