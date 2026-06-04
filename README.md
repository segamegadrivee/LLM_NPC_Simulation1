# LLM NPC Simulation

Unity project for simulating non-player characters whose dialogue is generated from structured in-game context and a large language model.

The project demonstrates a context pipeline for medieval village NPCs: the player talks to an NPC, the system gathers relevant world/player/NPC data, builds a context snapshot, converts it into a prompt, sends it to an LLM provider, and stores the reply as short-term dialogue memory.

## Main Idea

The NPC response is not selected from predefined dialogue lines. It is generated from:

- the active NPC profile;
- public world state and world events;
- visible player appearance and visible carried items;
- nearby scene objects;
- NPC-specific runtime state;
- retrieved knowledge entries;
- recent dialogue history with the same NPC;
- the player's current message.

Private player discoveries are treated carefully. They are not automatically converted into NPC knowledge. They can influence dialogue only as player claims or after they become public world state/events.

## Project Structure

Important folders:

- `Assets/_Project/Scripts/AI` - context retrieval, context snapshot, prompt building, LLM client code.
- `Assets/_Project/Scripts/NPC` - NPC profiles, NPC state, and interaction entry points.
- `Assets/_Project/Scripts/World` - player state, world state, world events, interactable objects, scene context objects.
- `Assets/_Project/Scripts/Knowledge` - ScriptableObject knowledge base and knowledge entries.
- `Assets/_Project/Scripts/Dialogue` - dialogue manager, runtime chat UI, dialogue memory.
- `Assets/_Project/Scripts/Debug` - runtime inspection tools for context and prompt diagnostics.
- `Assets/_Project/Data` - ScriptableObject assets for NPC profiles, knowledge base, and OpenAI settings.

## Runtime Pipeline

```text
NPCInteraction
  -> DialogueManager.OpenDialogue / SendPlayerMessage
  -> ContextRetriever.BuildSnapshot
  -> PromptBuilder.BuildPrompt
  -> OpenAIClient.SendPrompt
  -> DialogueManager stores and displays the NPC response
```

### 1. NPC Interaction

`NPCInteraction` is attached to NPC objects. When the player is close enough and presses the interaction key, it passes the assigned `NPCProfile` to `DialogueManager`.

### 2. Context Snapshot

`ContextRetriever.BuildSnapshot()` gathers all runtime and authored context into a `ContextSnapshot`.

The snapshot contains:

- `npcProfile`
- `playerState`
- `worldState`
- `npcState`
- `nearbyObjects`
- `recentRelevantEvents`
- `retrievedKnowledge`
- `recentDialogueHistory`
- `playerMessage`
- context availability entries with inclusion/exclusion reasons

### 3. Knowledge Retrieval

Knowledge is stored in a `KnowledgeBase` ScriptableObject as a list of `KnowledgeEntry` objects.

Retrieval is based on access rules, tags, current player message, visible player state, world state, world events, NPC state, nearby scene context, and entry importance. This is an in-memory keyword/tag/state scoring system, not a vector database.

### 4. Prompt Assembly

`PromptBuilder.BuildPrompt(ContextSnapshot snapshot)` creates the final prompt. It combines static behavior rules with dynamic context sections:

- system instruction;
- context usage rules;
- current NPC identity;
- visible player state;
- player-discovered facts as claims;
- NPC state toward the player;
- local environment;
- public world state;
- recent relevant events;
- retrieved knowledge;
- recent memory;
- current player message;
- response rules.

### 5. LLM Request

`OpenAIClient` sends the prompt to the OpenAI Responses API. API settings are stored in `OpenAISettings`.

API keys should be provided through:

- environment variable `OPENAI_API_KEY`;
- local text file path from `OpenAISettings`;
- inspector field only for local testing.

Do not commit API keys.

## Key Data Types

### NPCProfile

ScriptableObject defining:

- `npcId`
- `npcName`
- `role`
- `personality`
- `backstory`
- `speakingStyle`
- `knowledgeTags`
- `knownFacts`
- `relationships`

Example assets are stored in `Assets/_Project/Data/NPCProfiles`.

### WorldState

Runtime MonoBehaviour defining public world state:

- `villageMood`
- `currentEvent`
- `churchBellMissing`
- global facts

### PlayerState

Runtime MonoBehaviour defining player data:

- current role;
- equipped outfit;
- visible held item;
- visible status tags;
- held items;
- completed actions;
- known facts.

Only visible appearance and player claims are exposed to NPC context.

### SceneContextObject

MonoBehaviour attached to important scene objects such as the church, forge, or tavern. It provides:

- object id;
- display name;
- object type;
- description;
- tags;
- state facts.

Nearby scene context is collected by radius around the active NPC.

### KnowledgeEntry

Serializable data object inside `KnowledgeBase`.

Fields:

- `id`
- `title`
- `text`
- `tags`
- `relatedObjectIds`
- `knownByNpcIds`
- `importance`

An empty `knownByNpcIds` list means public knowledge.

## Controls

- `E` - interact with NPCs and world interactables.
- `F2` - toggle the World Status panel.
- `F3` - toggle the Context Debug overlay.
- `I` - toggle the Player Status HUD.

## Debugging

The debug tools are optional but useful for verifying the context pipeline:

- `ContextDebugOverlay` shows current NPC context, retrieved knowledge, world state, prompt preview, and provider status.
- `ContextEvidenceDumper` writes a detailed context report to the persistent data path.
- `ContextPipelineTracer` traces how values move from source data into the final prompt.
- `ValidateScene` checks whether required scene objects and references are configured.

## Notes

- Dialogue memory is stored in memory for the current play session.
- World events are runtime data unless serialized in the scene.
- Knowledge retrieval uses tags and text matching, not embeddings.
- The project uses ScriptableObject assets for NPC profiles, knowledge, and OpenAI settings.
- The system is designed around explainable context inclusion: each context item can be marked as included or excluded with a source and visibility reason.
