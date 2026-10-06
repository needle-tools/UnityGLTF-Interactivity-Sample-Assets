# KHR_interactivity – Invalid Graph / Rejection Cases

Overview of all cases in which the [KHR_interactivity specification](https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_interactivity/Specification.adoc) requires a behavior graph (or the whole extension object) to be rejected. Basis for creating invalid-graph test assets.

Source: `Specification.adoc` on `main`, retrieved 2026-09-28. Section names refer to the spec; the checklist in **Validation (Informative)** at the end of the spec is the primary reference.

## Severities

- **Reject the extension** – the whole `KHR_interactivity` object is unusable. Triggered by structural/schema "assert" failures (marked **†** below) and by an invalid *default* graph.
- **Reject the graph** – only that graph is unusable. *"One invalid graph does not invalidate other elements of the `graphs` array"* (JSON Syntax › General).

---

## A. Extension level (reject extension)

| # | Case | Example |
|---|---|---|
| A1 | `graphs` missing, not an array, or empty | `"graphs": []` |
| A2 | `graph` not a JSON index | `"graph": -1`, `0.5`, `"0"` |
| A3 | `graph` ≥ `graphs.length` | 1 graph, `"graph": 1` |
| A4 | The graph referenced by `graph` is invalid | any B–H case in the default graph |
| A5 | Any structural assert fails inside any graph | see **†** rows |

## B. `types`

| # | Case |
|---|---|
| B1 | Unknown `signature` (case-sensitive: `"Float"`, `"float5"`, `"vec3"`) |
| B2 | Duplicate non-custom signature (even if one entry has `extras`/`extensions`) |
| B3† | `types` empty, entry not an object, `signature` missing or not a string |
| B4 | More types than the implementation limit |

## C. `variables`

| # | Case |
|---|---|
| C1 | `type` ≥ `types.length` |
| C2 | `value` length doesn't match the type (`float3` with `[1,2]`, `float4x4` with 9 values) |
| C3 | `bool` value not a JSON boolean (`[1]`, `["true"]`) |
| C4 | Float-type element not a number (`[null]`, `["1.0"]`) |
| C5 | `int` value not int32 (`[1.5]`, `[2147483648]`) |
| C6 | `ref` value not a string or not a valid JSON Pointer (`["nodes/0"]`, `["/nodes/~2"]`) |
| C7† | `variables` without `types`; empty `variables`; `value: []`; `name` not a string |
| C8† | `type` missing or not a JSON index |

## D. `events`

| # | Case |
|---|---|
| D1 | Two events with the same `id` |
| D2† | `values` contains a socket named `event` |
| D3 | Event value `type` out of range († if missing) |
| D4 | Invalid initial `value` (same rules as C2–C6) |
| D5† | `id` / `name` not a string; `values: {}` |

## E. `declarations`

| # | Case |
|---|---|
| E1† | `op` missing or not a string |
| E2 | Unknown `op` without `extension` (`"math/addd"`, `"Math/Add"`) |
| E3 | Spec op (no `extension`) defines `inputValueSockets` / `outputValueSockets` |
| E4 | Extension op socket `type` out of range († if missing, or `{}`) |
| E5 | Equal declarations, e.g. `{op:"math/add"}` twice – all duplicates are invalid |

## F. Nodes – structure & wiring

| # | Case |
|---|---|
| F1 | `declaration` ≥ `declarations.length`; `nodes` without `declarations` († if missing) |
| F2† | Input value socket has both `node` and `value` |
| F3 | Input value socket `node` ≥ current node index (self/forward reference – no value loops) |
| F4 | `socket` names a non-existent output (incl. implicit `"value"` on a node without a `value` output) |
| F5 | `node` + `type` given and the type doesn't match the referenced output |
| F6 | Inline / type-default socket `type` out of range († if missing) |
| F7 | Invalid inline `value` for its type (C2–C6) |
| F8 | Required input socket missing from `values` (`math/add` without `b`) |
| F9 | Unsupported socket-type combination (`math/add` int + float, `flow/branch` with float `condition`) |
| F10 | `flows[x].node` ≤ current index or ≥ `nodes.length` (flows only point forward) |
| F11† | `flows` entry without `node`, `socket` not a string; `flows` / `values` / `configuration` is `{}` |
| F12† | Configuration property not `{ "value": [ ... ] }` with a non-empty array |
| F13 | Extra (unused) `values` / `flows` entries violating the rules above – extras are ignored but still validated |

## G. Operations without default configuration (missing/invalid config → reject graph)

| Op | Rejection cases |
|---|---|
| `variable/get` | `variable` missing, not int, negative, or ≥ variable count |
| `variable/set` | `variables` missing, not an array, or empty; element not a valid variable index; socket `"<index>"` missing or wrong type |
| `variable/interpolate` | Invalid index; target variable is `int` or `bool`; `useSlerp` missing or not bool; `useSlerp: true` on a non-`float4` variable |
| `pointer/get` | `pointer` missing, not a string, or invalid template syntax (below); `type` missing, not int32, negative, or out of range; template parameter socket missing or wrong type (`[x]` → int, `{x}` → ref) |
| `pointer/set` | As `pointer/get`, plus template contains `[value]` / `{value}` |
| `pointer/interpolate` | As `pointer/set`, plus `[duration]`, `[p1]`, `[p2]` (and `{}` forms); `type` is `bool` or `int` |
| `event/receive`, `event/send` | `event` missing, negative, or ≥ event count; `event/send`: event socket missing or wrong type |

**Invalid pointer template syntax** (JSON Pointer Template Parsing – the spec lists ~20 examples):

- `~2` (invalid JSON Pointer escape)
- duplicate parameter: `[index]…[index]`, `{index}…[index]`
- lone bracket segment: `/[/`, `/{/`
- empty parameter: `[]`, `{}`
- unterminated parameter: `[index`, `{index`
- bracket inside parameter: `[i[ndex]`, `[i{ndex]`, `{i}ndex}`, …
- odd number of literal brackets: `[[i[ndex]]`, `{{i{ndex}}`, `[[index]`, `{{index}`

## H. Operations with default configuration that can still reject

| Op | Rejection cases |
|---|---|
| `math/switch` | `selection` or `default` missing; `selection` not int; configured `cases` socket missing or of a different type than `default` |
| `flow/switch` | `selection` not int (F9) |
| `debug/log` | Valid `message` (e.g. `"x={a}"`) but `values` socket `a` missing |
| `flow/switch`, `flow/sequence`, `flow/multiGate`, `math/switch`, `pointer/*`, `variable/set` | Generated sockets/flows exceed an implementation limit (implementation-defined, not portably testable) |

## I. Must NOT be rejected (regular test assets)

These are not part of the invalid set. They are regular test cases with checks (see *Test assets* below).

- Invalid config on an op that has a default → falls back to default: `flow/switch` `cases: [0.5, 1]` or `[-2147483649]`, `multiGate` `isRandom: "yes"`, `debug/log` `message: "{"`.
- Config on a non-configurable op, unknown config properties → ignored.
- Unknown `op` **with** `extension` → no-op, not invalid.
- Flow to a non-existent input flow socket → unconnected.
- Duplicate `cases` entries (`[1, 2, 2]`) → duplicate ignored.
- Invalid non-default graph alongside a valid default graph → asset still works (counterpart of A4).
- Runtime problems (null refs, negative indices, read-only pointers) → `err` / `isValid=false` at runtime, not load-time rejection.

## Spec inconsistencies (to raise with Khronos)

1. The Validation section's allowed `signature` list (Graph Object Validation, step 2) omits `"ref"`, although `ref` is a value type and Inline Value Validation handles it.
2. For missing `op`, missing `declaration`, missing variable `type`, and `node` + `value` together, the normative text says **reject the graph**, while the informative Validation section treats them as asserts (**reject the extension**). The expected outcome for **†** test assets is ambiguous until clarified.

## Test assets

Generated by `Khronos Test Export/InvalidGraphs` (menu **Sample Scenes/Export Khronos Invalid Graph Tests**) as one plain-JSON `.gltf` per case into `<test export path>/invalid/<group>/`, with `invalid-index.json` and `README.md` for the whole set. Case ids follow the rows of this document (sub-cases get a letter suffix; the op-specific rules of G/H are numbered per operation, pointer template syntax cases are `G5-01` … `G5-22`). Implementation-defined limits (B4, H-limits) are not generated since they cannot be tested portably.

Every graph starts with `event/onStart → debug/log → event/send(test/onFailed)`. Pass = the event is never received.

The group I rules are regular test cases, so their result is checked and not only that the graph loads. JSON the UnityGLTF exporter can't write is added to the serialized graph after export (`TestContext.PatchSerializedGraph`, applied by `TestFileExporterPlugin`):

| Rule | Test | Sub-test |
|---|---|---|
| Fractional / out-of-int32 `cases` → default configuration | `flow/switch` | Cases [0.5, 1] / [-2147483649, 0] use default configuration |
| Duplicate `cases` are ignored | `flow/switch` | Duplicate cases [1, 2, 2] |
| Fractional `cases` → default configuration | `math/switch` | Cases [0.5, 1] use default configuration |
| Non-boolean `isRandom` → default configuration | `flow/multiGate` | isRandom "yes" uses default configuration (in order) |
| `inputFlows` > 64 → default configuration | `flow/waitAll` | [inputFlows] 65 uses default configuration |
| Duplicate variable indices | `variable/setMultiple` | Duplicate index [var4, var4] |
| `useSlerp` on a float4 variable | `variable/interpolate` | useSlerp on float4, value at 100% |
| I01 `debug/log` message `"{"` → default configuration | `graph/ignored configuration` | debug/log invalid message: [out] |
| I02 Configuration on a non-configurable op → ignored | `graph/ignored configuration` | math/add with configuration (= 3) |
| I03 Unknown configuration property → ignored | `graph/ignored configuration` | flow/switch unknown property: case [1], [default] not activated |
| I04 Op of an unsupported extension → no-op | `graph/unsupported operations and graphs` | unsupported op: output flow not activated, type-default output (int 0) |
| I05 Flow to a non-existent input flow socket → unconnected | `graph/extra and unknown sockets` | flow to unknown socket: next output runs, target not run |
| I06 Additional input value socket → ignored | `graph/extra and unknown sockets` | math/add with extra input c (= 3) |
| I07 Output flow id the op doesn't define → never activated | `graph/extra and unknown sockets` | flow/branch extra flow: [true], target not run |
| I08 Invalid non-default graph → default graph still runs | `graph/unsupported operations and graphs` | invalid graph 1: graph 0 runs (graph 1 would send `test/onFailed`) |
| I09 Doubled brackets in a pointer template are literal | `graph/json syntax` | pointer with doubled brackets: isValid false |
| I10 Integers written as float literals (`5.0`, `2.0`, variable index `N.0`) | `graph/json syntax` | integers written as 5.0 and 2.0 (= 7) |
| I11 Type-default input value, node reference with explicit type | `graph/extra and unknown sockets` | type-default float input is NaN; node reference with type (`abs(E)`) |
| I12 Several types with signature `custom` | `graph/json syntax` | two custom types |
| I13 Internal events without id | `graph/json syntax` | event without id: received, other event not received |
| I14 Variables with the same name | `graph/json syntax` | same variable name: first unchanged, second set |

## Exporter validation

`UnityGLTF/Runtime/Scripts/Interactivity/Export/Helpers/GraphSpecValidator.cs` checks the serialized extension object on every export (`Validator.ValidateSpecification`) and logs the violations grouped by rule. It covers the structural rules (A–F) and the configuration-dependent rules of `variable/*`, `pointer/*`, `event/receive` and `event/send` (G). Not covered: rules that need the socket and type definitions of each operation (unknown ops, output socket names, input types, math/switch and flow/switch sockets, debug/log placeholders).

## Existing test assets violate the current spec

All 164 `.glb` files in `glTF-Test-Assets-Interactivity` (checked 2026-09-28) break rules listed above:

- **F10** – output flows pointing to earlier nodes (~7,000 occurrences, every file).
- **F11†** – empty `values` / `flows` / `configuration` objects on nodes (every file); **D5†** – empty event `values` (151 files); empty `events` / `variables` arrays (11 files).

- **C4** – float variable initialized with the string `"-1"` (`math/random` test, 32 occurrences in 10 files; fixed in `AdditionalMathTests.cs`).
- **C4 / inline value validation** – NaN and infinity written as the strings `"NaN"` / `"Infinity"` in float values (variables of `variable/set and get`; inputs of `pointer/interpolate`, `variable/interpolate`, `math/inverse`, `math/length`, `math/matDecompose`, `math/normalize`). JSON has no such numbers, and the in-memory validation missed them because Newtonsoft only turns them into strings when writing. Fixed in the exporter: all-NaN values are written as type-default (`type` without `value`), other inline values with NaN/infinity components are built with `math/combineN` from `math/nan` / `math/inf` (/ `math/neg`) nodes, and `GraphSpecValidator` now rejects non-finite float elements.
- **E4d†** – extension declarations with `inputValueSockets: {}` (16 occurrences in 14 files, e.g. `event/onSelect`).
- **F12†** – `flow/switch` with `cases: { "value": [] }` (2 files). Cause: `Flow_SwitchNode` and `Math_SwitchNode` default `cases` to `new int[] {}`; the `flow/switch` test used it for its "empty cases" sub-test (fixed: the property is now omitted).

A conformant implementation has to reject all of them. Causes in the UnityGLTF interactivity exporter:

- ~~`TopologicalSort()` only follows value connections~~ – fixed: the final sort (`TopologicalSort(includeFlows: true)`) orders by value and flow connections; a node that triggers a node whose output it reads is routed through `event/send`/`event/receive` (with a warning, the target then runs asynchronously). None of the current test graphs needs this.
- ~~Serialization writes empty objects/arrays~~ – fixed: `GltfInteractivityNode`, `CustomEvent`, `Declaration` and `GltfInteractivityGraph` omit empty `configuration` / `values` / `flows` / socket objects and arrays, unset and empty-array configuration values (= default configuration), `"socket": null` on flows and empty event ids; type-default inputs are written with their `type`.
