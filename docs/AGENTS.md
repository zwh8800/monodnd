# DOCS KNOWLEDGE BASE

## OVERVIEW

Design corpus for 酒馆与命运 / Tavern & Destiny. 26 markdown files, 30k+ lines total. Zero implementation code exists. All specifications are in 简体中文 with English technical identifiers.

## WHERE TO LOOK

| Need | File | Lines |
|------|------|-------|
| Game canon, mechanics, design decisions | `GDD-v1.md` | 1029 |
| MonoGame vs Unity reasoning (AI safety) | `technical/01-engine-selection.md` | (not scoped) |
| Module architecture, C# code map | `technical/02-overall-architecture.md` | 2360 |
| Dev rules, forbidden ops, review checklist | `technical/03-vibe-coding-conventions.md` | 776 |
| Setup, NuGet, build commands | `technical/04-dev-environment-setup.md` | 1041 |
| 48-week roadmap, task IDs (P0-01 format) | `technical/05-implementation-plan.md` | 1930 |
| Character data models, races, classes | `subsystems/01-character-system.md` | 2461 |
| LLM Gateway, 4 Agents, Schema validation | `subsystems/02-llm-integration.md` | 4213 |
| Items, weapons, armor, economy | `subsystems/03-items-equipment.md` | 2362 |
| Combat engine FSM, DND 5e deviations | `subsystems/04-combat-system.md` | 2400 |
| Node graph, FOV, map generation | `subsystems/05-map-exploration.md` | 2465 |
| Three-layer adventure generation pipeline | `subsystems/06-adventure-generation.md` | 2766 |
| Tavern meta-game, relationships | `subsystems/07-tavern-system.md` | 2190 |
| Settlement, scars, failure consequences | `subsystems/08-failure-growth.md` | 2284 |
| UI screen layouts, Myra widgets | `subsystems/09-ui-ux-design.md` | 1927 |
| Original concept brainstorming | `start.md` | 479 |

## CONVENTIONS

- **Language**: 简体中文 for prose, descriptions, design rationale. English for code identifiers, JSON keys, enum values, file names.
- **Cross-references**: Subsystems reference each other and GDD sections by `§N.M` numbering. Follow the chain. Don't read in isolation.
- **Depth order**: GDD (high-level canon) → technical/ (architecture) → subsystems/ (module deep-dives). Start at GDD, drill down.
- **DND 5e SRD baseline**: Every subsystem documents deviations from standard 5e. If not explicitly listed as a deviation, assume SRD rules apply.
- **LLM = skin layer**: Enforced across all 9 subsystems. Program controls mechanics; LLM generates narrative only. Documented via Event Result Separation Model.
- **Data-driven**: All numeric values belong in JSON/SQLite config, not hardcoded. Subsystems define schema expectations but actual configs go in `Data/`.

## NOTES

- Cross-module dependency chain: Character → Items → Combat → Map → Adventure → Tavern → Failure. Read in order for full context.
- `start.md` is the original ChatGPT/Claude/Kimi brainstorming output. Not a spec. Useful for design intent but not authoritative.
- schema files referenced in subsystems (e.g., adventure_blueprint, narrative_text) will live in `Data/Schemas/`. Not yet created.
- Phase 0 (scaffolding) = first actionable task. See `technical/05-implementation-plan.md` §P0.
- Game text is 简体中文 at runtime even though docs are in Chinese too. No translation needed between docs and code strings.
- 30k+ lines means agents should read selectively: GDD overview first, then relevant subsystem. Never read a subsystem start-to-finish unless implementing it.
