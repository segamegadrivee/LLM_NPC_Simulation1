# LLM NPC Simulation — MVP Architecture

> Bachelor diploma project: **"Method for simulating an agent in virtual environments using large
> language models."**

This document describes the stabilized MVP architecture. It is intended to be honest enough to cite
directly in the diploma: it states what is actually implemented, what is legacy/optional, and what is
deliberately out of scope.

---

## 1. Final MVP purpose

This is **not** a chatbot and **not** a game. It is a Unity prototype of a *method* for driving a
virtual agent (an NPC) with a large language model. The contribution is the **pipeline**, not the
content:

> Player action / message → structured context is gathered from the live Unity world and
> ScriptableObject data → a **Context Availability Layer** decides what this specific NPC is allowed
> to know → a context snapshot is turned into a prompt → the prompt is sent to the OpenAI API → the
> NPC's reply is shown and stored as short-term memory.

The method's core ideas demonstrated here:

- **Structured context enrichment / RAG-like retrieval** over Unity runtime state and
  ScriptableObject knowledge (keyword/tag/state activation + scoring + access gating). This is a
  lightweight in-memory retrieval, **not** a vector database.
- **Provenance and visibility** for every piece of context, so the system can *explain* why a fact
  was included or excluded for a given NPC, and so private player knowledge cannot silently leak into
  an NPC's mouth.

---

## 2. Runtime pipeline

```
NPCInteraction (player presses E near an NPC)
   └─> DialogueManager.OpenDialogue / SendPlayerMessage
         ├─> ContextRetriever.BuildSnapshot(npc, npcTransform, playerMessage)
         │      ├─ PlayerState (visible appearance + private facts)
         │      ├─ WorldState (public/global facts)
         │      ├─ WorldEventLog (public / targeted events)
         │      ├─ NPCState (this NPC's mood / trust / personal events)
         │      ├─ SceneContextObject(s) within radius (nearby places)
         │      ├─ KnowledgeBase (retrieved, access-gated, activated, scored)
         │      ├─ NPCConversationMemoryStore (recent dialogue with this NPC)
         │      └─ Context Availability Layer  →  included / excluded ContextEntry list
         │            (PlayerState.knownFacts marked PrivateToPlayer = excluded)
         ├─> PromptBuilder.BuildPrompt(snapshot)   → structured, scenario-agnostic prompt
         ├─> OpenAIClient.SendPrompt(prompt, cb)    → OpenAI /v1/responses
         └─> AddNpcResponse
               ├─ RuntimeChatUI (visible chat + provider status)
               ├─ NPCConversationMemoryStore (store reply)
               └─ ContextDebugOverlay (optional: included/excluded context, prompt preview)
```

The same `ContextSnapshot` feeds both the prompt and the debug overlay, so what the diploma shows in
the debug UI is exactly what the model received.

---

## 3. Module map

Scripts already live in sensible folders under `Assets/_Project/Scripts`. We keep the physical
folders as-is (moving `.cs` risks breaking GUID/serialized references for no real benefit) and define
the **logical modules** here.

| Module | Files |
|---|---|
| **Runtime / Core wiring** | `DialogueManager` (orchestrator + singleton), component singletons (`WorldState`, `WorldEventLog`, `NPCStateStore`, `NPCConversationMemoryStore`, `ContextRetriever`). No separate bootstrap object; wiring is via serialized references on a `GameSystems` object + singletons. |
| **State** | `World/PlayerState`, `World/WorldState`, `World/WorldEvent`, `World/WorldEventLog`, `NPC/NPCState`, `NPC/NPCStateStore`, `Dialogue/NPCConversationMemoryStore` |
| **Data (ScriptableObject)** | `NPC/NPCProfile`, `Knowledge/KnowledgeBase`, `Knowledge/KnowledgeEntry`, `World/SceneContextObject`, `World/EvidenceObject` (optional) |
| **Context** | `AI/ContextRetriever`, `AI/ContextSnapshot`, `AI/ContextAvailability` (`ContextEntry`, `ContextVisibility`, `ContextSourceType`) |
| **Prompt / LLM** | `AI/PromptBuilder`, `AI/OpenAIClient`, `AI/OpenAISettings`, `AI/ILLMClient`, `AI/MockLLMClient` (DEV-only) |
| **Dialogue** | `Dialogue/DialogueManager`, `Dialogue/DialogueMessage`, `NPC/NPCInteraction` |
| **Interactions** | `World/BaseInteractable` (shared), `World/OutfitInteractable`, `World/VisibleItemInteractable`, `World/HiddenBellInteractable`, `World/EvidenceInteraction` (optional), `World/ThrowAtNpcInteraction` (DEV/optional) |
| **UI** | `Dialogue/RuntimeChatUI` |
| **Debug (DEV-only)** | `Debug/ContextDebugOverlay` (main debug UI), `Debug/ContextEvidenceDumper`, `Debug/ContextPipelineTracer`, `Debug/DemoWorldStateControls` |
| **Editor** | `Editor/CreateBellScenarioDemoData`, `Editor/CreateOpenAISettingsAsset`, `Editor/CreateLadderEvidenceDemoObject` (optional), `Editor/ValidateMvpScene` |

---

## 4. Class responsibilities (key types)

- **DialogueManager** — opens/closes dialogue, locks player input, builds the snapshot via
  `ContextRetriever`, builds the prompt via `PromptBuilder`, sends it through the resolved
  `ILLMClient`, records the reply into memory and exposes "last prompt / last provider / last
  snapshot" for the UI and debug tools. OpenAI is the only supported runtime path.
- **ContextRetriever** — the heart of the method. Finds the player, nearby scene objects, the NPC's
  state, relevant world events, retrieves & scores knowledge, gathers recent dialogue, and builds the
  **Context Availability Layer** (included/excluded `ContextEntry` records). Knowledge access is gated
  by `knownByNpcIds` (empty = public); activation requires a "strong" source (player message, visible
  state, NPC state, world event, or world state) and a score threshold.
- **ContextSnapshot** — plain data passed to the prompt: typed fields (profile, player/world/NPC
  state, nearby objects, events, knowledge, history) **plus** `includedEntries` / `excludedEntries`
  provenance.
- **ContextAvailability** — `ContextSourceType`, `ContextVisibility`, and `ContextEntry`. Each entry
  records source, visibility, inclusion decision, and (when excluded) the reason.
- **PromptBuilder** — assembles the prompt from named sections (system role, context rules, NPC
  identity, visible player state, player claims, NPC state, location, world state, public events,
  knowledge, memory, output rules). Scenario-agnostic: no hardcoded per-NPC scripts.
- **OpenAIClient** — calls the OpenAI `/v1/responses` endpoint, parses the reply, and reports the
  actual provider used. Key resolution lives in `OpenAISettings` (env var / local file / inspector).
- **PlayerState / WorldState / WorldEventLog / NPCState(Store) / NPCConversationMemoryStore** —
  in-memory runtime state for the play session.

---

## 5. Context Availability Layer (the main architectural improvement)

Every considered piece of context becomes a `ContextEntry` with:

- `sourceType` — `PlayerState`, `WorldState`, `WorldEventLog`, `NPCState`, `DialogueMemory`,
  `SceneContextObject`, `KnowledgeBase`, `NPCProfile`, `Interaction`, `Debug`.
- `visibility` — included kinds: `VisibleOnPlayer`, `NearbySceneContext`, `PublicWorldState`,
  `PublicWorldEvent`, `TargetedEvent`, `NpcPersonalMemory`, `NpcProfileKnowledge`,
  `RetrievedKnowledge`, `PlayerClaim`; excluded kinds: `PrivateToPlayer`, `Excluded`.
- `includedInPrompt` + `exclusionReason`.

**Privacy rule (enforced in `ContextRetriever.BuildContextAvailabilityEntries`):**
`PlayerState.knownFacts`, `completedActions`, and pack `heldItems` are recorded as **PrivateToPlayer
and excluded** — they never become NPC-owned facts automatically. They can reach an NPC only as:

- a **PlayerClaim** (the player states it in dialogue), or
- a **PublicWorldEvent / PublicWorldState** (an actual public event occurs, e.g. the bell is found).

The `PromptBuilder` reinforces this: the "PLAYER DISCOVERED FACTS" section instructs the NPC to treat
those facts as the player's claims only.

---

## 6. Final demo scene

**Recommended source of truth: `Assets/Scenes/SampleScene.unity`.**

Why: it is the **only scene in Build Settings**, contains the full core pipeline
(DialogueManager, RuntimeChatUI, ContextRetriever, OpenAIClient, MockLLMClient, PlayerState,
WorldState, 4 NPCInteractions, 3 SceneContextObjects, 1 EvidenceInteraction), and is **free of the
dead Database scripts**.

Scene comparison:

| Scene | In build | Dead Database scripts | WorldEventLog / NPCStateStore present | Notes |
|---|---|---|---|---|
| `Assets/Scenes/SampleScene.unity` | ✅ yes | none | no (auto-created at runtime) | **Use this.** |
| `Assets/_Recovery/0.unity` | no | none | yes (explicit) + 2 OutfitInteractables | Most complete; good reference for what to add to SampleScene. |
| `Assets/working.unity` | no | DatabaseDebugPanel, KnowledgeDatabaseSeeder, DatabaseManager (missing scripts) | no | Legacy. |
| `Assets/working_mvp.unity` | no | same 3 dead scripts | no | Legacy. |

Run **Tools ▸ AI NPC ▸ Validate MVP Scene** to check the active scene against the required pipeline.
It reports problems only and never modifies the scene.

Recommended manual additions to SampleScene (see §10):
add persistent `WorldEventLog`, `NPCStateStore`, `NPCConversationMemoryStore` to GameSystems; and the
appearance/event interactables (`OutfitInteractable`, `VisibleItemInteractable`,
`HiddenBellInteractable`) for the demo flow.

---

## 7. Demo flow (what to show)

1. **Baseline** — talk to an NPC; it knows the public missing-bell situation from `WorldState`.
2. **Visible player state** — equip guard armor / dark cloak via `OutfitInteractable`; the NPC reacts
   because it is *visible*.
3. **Location context** — talk near a Forge / Church / Tavern `SceneContextObject`; the reply shifts
   with place.
4. **Public world event** — trigger `HiddenBellInteractable`; `WorldState` + `WorldEventLog` update;
   NPCs can now reference the public event.
5. **(Optional) Private vs public** — collect ladder/evidence; show in the debug overlay that it is
   `PrivateToPlayer` (excluded) until the player *states* it (then it is a `PlayerClaim`).

The **ContextDebugOverlay** ([DEBUG CONTEXT] button in the chat header) is the screen to show
included vs excluded context and the prompt preview alongside the answer.

---

## 8. Legacy / optional / DEV-only

- **Dead Database components** — `DatabaseDebugPanel`, `KnowledgeDatabaseSeeder`, `DatabaseManager`
  exist **only** as missing-script references in `working.unity` / `working_mvp.unity`. No such C#
  exists in the project. **No database is implemented.** Not used by the final scene.
- **MockLLMClient** — DEV-only fallback with scripted answers. Not part of MVP behavior. OpenAI is the
  runtime path; `useMockOnFailure` defaults to off so failures surface as a clear error. When the
  fallback is used it is labelled `Mock (fallback)` / `Mock (DEV)` in the UI.
- **Ladder / evidence** (`EvidenceObject`, `EvidenceInteraction`, `CreateLadderEvidenceDemoObject`) —
  optional. Demonstrates *private player discovery* (excluded context), not the central scenario.
- **ContextEvidenceDumper / ContextPipelineTracer / DemoWorldStateControls** — DEV diagnostic tools.
  Now hidden behind `RuntimeChatUI.showDebugControls`; not in the normal MVP UI.
- **ThrowAtNpcInteraction** — optional/DEV aggression trigger.

---

## 9. What must NOT be claimed in the diploma

- ❌ No vector database, no embeddings. Retrieval is keyword/tag/state activation + scoring.
- ❌ No SQL database, no full JSON persistence. State is ScriptableObjects + in-memory runtime state
  for the session only.
- ❌ No autonomous NPC planning, goals, or self-directed behavior. NPCs respond to the player.
- ❌ No full emotional / trust / relationship simulation. `NPCState` holds simple mood/trust strings
  used as context hints, not a modelled system.
- ✅ Honest claim: a Unity method/pipeline that builds an **explainable, access-controlled,
  structured context snapshot** from live world state and ScriptableObject knowledge, and uses the
  OpenAI API to generate in-character NPC dialogue (RAG-like context enrichment over Unity state).

---

## 10. Known manual setup / remaining risks

- **New scripts need a Unity import** to generate `.meta`/GUIDs: `ContextAvailability.cs`,
  `BaseInteractable.cs`, `Editor/ValidateMvpScene.cs`. Open the project once in Unity.
- **SampleScene config (one click each):** on the OpenAIClient component, uncheck **Use Mock On
  Failure**; ensure DialogueManager **Use OpenAI** is on and OpenAIClient/settings are assigned. The
  validator flags these.
- **SampleScene content:** add persistent `WorldEventLog`, `NPCStateStore`,
  `NPCConversationMemoryStore` (otherwise they are auto-created with a console warning), and add the
  appearance/event interactables for the full demo flow.
- **OpenAI key:** unchanged. Provided via env var / local file / `OpenAISettings`. Never logged or
  committed.
- **DEV tracer accuracy:** `ContextPipelineTracer` still expects some prompt fields (e.g. aggression)
  that the slimmer prompt no longer prints verbatim; it is a DEV tool and does not affect runtime.
