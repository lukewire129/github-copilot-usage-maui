---
name: mauireactor-3
description: "Develop MauiReactor 3.x applications - a declarative UI component framework for .NET MAUI targeting net9.0. Use this skill whenever users create, debug, or optimize MauiReactor 3.x components. Essential for: building stateless/stateful components, property-based animations (WithAnimation), AnimationController sequences, component lifecycle management, navigation, state management patterns, [Prop]/[Param]/[Inject] attributes, [Scaffold] third-party control wrapping, or answering MauiReactor 3.x-specific architecture questions. Always use this skill for any MauiReactor question — even if the user just says 'MauiReactor' without specifying a version, default to this skill unless they explicitly mention version 4+."
---

# MauiReactor 3 Skill

> **Version scope**: MauiReactor **3.x** / **net9.0** only.  
> A separate skill will cover MauiReactor 4.x when available.

## 📚 Reference Index

이 스킬은 주제별 참조 파일로 나뉩니다. 질문 유형에 따라 해당 파일을 읽으세요.

| # | 파일 | 다루는 내용 |
|---|------|------------|
| 1 | `references/01-setup.md` | 프로젝트 생성, hot-reload, Program.cs, 패키지 구조 |
| 2 | `references/02-components.md` | Stateless/Stateful 컴포넌트, Props Class, 생명주기, **Diffing 메커니즘 & 설계 가이드** |
| 3 | `references/03-state-management.md` | `[Prop]` / `[Param]` / `[Inject]` 패턴 비교표 + 예제 |
| 4 | `references/04-animation.md` | WithAnimation() 위치 규칙, AnimationController, easing |
| 5 | `references/05-layout-fluent.md` | VStack/HStack/Grid/Frame, Fluent API 속성 목록 |
| 6 | `references/06-navigation.md` | Basic navigation, Props 전달, NavigationPage |
| 7 | `references/07-scaffold.md` | **[Scaffold] 서드파티 컨트롤 래핑** (Syncfusion, Telerik 등) |
| 8 | `references/08-patterns.md` | Form, List, Loading State, 디버깅 팁 |
| 9 | `references/09-hidden-components.md` | 문서에서 찾기 어려운 컴포넌트 모음 (Timer 등) |

---

## 빠른 판단 가이드

```
질문 유형                          → 읽을 파일
─────────────────────────────────────────────
"프로젝트 만들기/설치/hot-reload"  → 01-setup.md
"컴포넌트 만드는 법/생명주기"      → 02-components.md
"Diffing/성능/Stateful 배치"      → 02-components.md  ← 설계 시 필수!
"Prop vs Param 뭐 써야 해?"       → 03-state-management.md  ← 자주 혼동!
"애니메이션 안 됨/위치 오류"       → 04-animation.md
"VStack/Grid 레이아웃"             → 05-layout-fluent.md
"페이지 이동/뒤로가기"             → 06-navigation.md
"Syncfusion/Telerik 컨트롤 쓰기"  → 07-scaffold.md
"폼 처리/리스트/로딩"              → 08-patterns.md
"Timer/잘 모르는 컴포넌트"        → 09-hidden-components.md
```

---

## Key Concepts (30초 요약)

- **No XAML** — 순수 C# fluent 문법
- **MVU Pattern** — 상태 변경 → UI 자동 재렌더링
- **Component** = Stateless, **Component\<TState\>** = Stateful
- **[Prop]** = 단방향 (부모→자식 읽기 전용)
- **[Param]** = 양방향 (트리 전체 공유, 누구나 쓰기 가능)
- **[Inject]** = DI 서비스 주입 (UI 상태 아님)
- **[Scaffold]** = 서드파티 네이티브 컨트롤 → MauiReactor VisualNode 자동 생성
- **WithAnimation()** 위치가 핵심 — 반드시 애니메이션 속성 **뒤**에 위치

---

## 링크

- 공식 문서: https://adospace.gitbook.io/mauireactor
- 서드파티 통합 예제: https://github.com/adospace/mauireactor-integration
- 샘플 앱: https://github.com/adospace/reactorui-maui-samples