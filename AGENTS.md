# Deltempo — agent instructions

## Read this first

`PROJECT_MAP.md` (repository root) is the map: the six projects, the eight layers,
the entry points, the invariants that must not be broken, the gate commands, and
where each knowledge artifact lives. Read it before your first grep.

## Structural questions: query the graph, do not re-derive it

Two graphs, one architecture picture and one memory store describe this repository. Use
them before reading source files; they answer "where" and "what touches what" far
cheaper than search.

| Question | Use |
| --- | --- |
| Where is X, what is in file Y, which layer owns it | `node .ua\ua-query.mjs file <name>` / `layer <name>` |
| What does this symbol/file connect to | `node .ua\ua-query.mjs edges <node id>` |
| Call graph, communities, god nodes, blast radius, PR impact | graphify MCP: `graph_stats`, `query_graph`, `get_node`, `get_neighbors`, `shortest_path`, `get_pr_impact` |
| The whole component topology at a glance | `.archify\architecture-*\deltempo-architecture.html` — open it in a browser; regenerate with the `archify` skill |
| Why is it built this way, what was tried and rejected | megamemory MCP: `understand` before a task, `create_concept` / `update_concept` after |
| What a domain word means here | `CONTEXT.md` |

Check freshness before trusting either graph:

```powershell
node .ua\ua-query.mjs stats        # prints "current" or "STALE" against HEAD
```

If it says `STALE`, refresh before quoting summaries:

```powershell
graphify update . --force          # code graph, no LLM needed
```

`.ua/` is refreshed by the `/understand` skill; it is the only writer of layer and
tour data. `.archify/` is a local, gitignored artifact from the `archify` skill: it
draws components and arrows, so being a few commits behind HEAD only matters when
`src/` changes shape. None of them replaces reading the file you are about to edit.

## Non-negotiables

- **Never weaken the safety layer.** `ProtectionPolicy` is a denylist: every change
  adds a refusal, never an exception. Recursive deletes consult
  `TryGetProtectedDescendant` first.
- **Bump the published test count everywhere at once** (README badge + prose, its 8
  translations, `SECURITY.md`, `docs/ai-catalog.json`, `docs/llms.txt`,
  `docs/llms-full.txt`, `docs/index.html`, changelog current card).
  `DocumentationConsistencyTests` fails on any drift.
- **Gate order:** `dotnet build deltempo.sln -c Release` → `dotnet format
  deltempo.sln --verify-no-changes --no-restore` → build the test project and run
  `Deltempo.Tests.exe` (never `dotnet test`).
- **`docs/` is published to the site.** Notes, drafts and scratch go to `.internal/`.
- Do not commit or push anything unless the user asks for it. Do not rewrite
  published history, version numbers or release claims as a side effect of another
  task.
