# Game-version address migration for AI Agents

## What changes

| Data | Lifetime / update risk | Correct treatment |
|---|---|---|
| Process ID and module base | Every launch; ASLR changes the base | Read the live process; `VA = base + RVA` |
| Player, inventory, container, reference and result-array pointers | Spawn, load, scene or process lifetime | Resolve again and validate type/identity; never persist as offsets |
| Native function / vtable / global-variable RVAs | Compilation and game updates | Rediscover each semantic entry, not one global delta |
| Field offsets, layouts, flags, ABI and parameter positions | Potentially every binary revision | Revalidate metadata and machine code, even when an RVA is unchanged |
| Runtime name IDs and CFunction/RTTI addresses | Process/build-dependent | Resolve by stable names and inspect their registered metadata |
| Item/ability names and asset metadata | Often stable, not guaranteed | Validate against installed base/DLC resources and actual object type |
| Hook pages, jobs, request flags and ownership | One injected runtime/process | Check magic/code/owner; never transplant to another process |

ASLR relocation is not a game update. A correct old RVA can be reused only with a verified matching layout/profile; a heap pointer cannot be reconstructed by adding the new module base.

## Actual examples from this code

`GameVersion.cs` maps these legacy-reference RVAs to the current 5.00c profile:

| Meaning / reference | Reference RVA | 5.00c RVA |
|---|---|---|
| FactsAdd entry | `0x1F5CEF0` | `0x1F58700` |
| Native entry | `0x2050950` | `0x204C140` |
| Global data | `0x5CC5440` | `0x5C9E408` |
| Array destructor | `0x26FC970` | `0x26FC970` |

The differences vary; some entries stay fixed. A candidate `0x1F58600` for FactsAdd was a function-interior address rather than the registered entry and caused a crash. Its proximity was not evidence. The corrected address came from actual registration and instruction verification. Preserve that lesson when migrating.

The current hash selects 5.00c; an unknown hash follows the legacy-reference branch. **This is not proof of unknown-version support.** An agent adapting a new build must stop before writes until semantic entries/layouts are independently validated. A player-facing hash mismatch alone need not produce a modal warning; concrete operation validation must still remain intact.

## Migration workflow

1. **Fingerprint:** record executable SHA256, size, architecture, renderer and visible build. Keep a read-only local copy for analysis; do not upload game binaries. Use a saved game and separate analysis artifacts.
2. **Find semantic anchors:** inspect registered native/script names, configuration keys, class/property names and string cross-references. Follow registration to the actual function pointer. Relevant existing anchors include `ResolveName`, `LootGlobalFunction`, `RespecProperty`, `FunTarget` and the metadata readers in the scheduler.
3. **Resolve entry points:** follow verified call/jump thunks and check PE section bounds. Decode the candidate function with a disassembler; confirm prologue/control flow/calling convention and the registered parameter count/types. A nearby string cross-reference or matching byte fragment alone is insufficient.
4. **Use signatures carefully:** build a signature from meaningful instructions, masking relative-call displacements, RIP-relative addresses and relocations. Scan the appropriate executable section, require a unique match, then confirm with an independent registration/type anchor. Zero/multiple matches are unresolved, not permission to choose the first.
5. **Revalidate layouts:** examine CFunction owners, flags, property types, argument offsets, return types and native/script entry selection. Check player/inventory vtables, reference lifetimes and container classes. Old field offsets are hypotheses until verified.
6. **Review hooks:** patch complete instructions only; account for RIP-relative operands and relative branches. Check original bytes, hook magic, ownership, page size, saved registers/stack alignment and the return address. Do not widen a signature just to make a validation pass.
7. **Update a profile:** add explicit mappings and expected byte patterns with evidence for each entry. Keep the old profile and its history. Do not blindly shift all addresses or change a hash to claim compatibility.
8. **Offline verification:** build both languages; test signatures against the local binary without writes, bytecode rewrite invariants, metadata parsers and request-state logic. Verify translated source keeps hex constants, native IDs and resources unchanged.
9. **Read-only live verification:** with permission appropriate to the session, resolve fresh objects, registrations and types. Confirm the process identity remains the same through reads. Readback is not proof that a future call is safe.
10. **One controlled mutation:** obtain explicit player consent and a save, run an isolated feature, inspect completion and the actual effect, restore temporary switches and verify persistence separately when required. Stop and preserve diagnostics on a crash or validation failure.

## Pauses and object lifetime

The scheduler executes jobs on the game thread. Menus/loading can delay execution. Distinguish pending=1, running=2 and completed state; timeout does not imply the job never executed. At the cancellation boundary, inspect completion while threads are stopped. Cancel only a job that has not started. Await an executing job rather than reusing its buffers.

Auto-loot keeps a returned array pending until its destructor completes, finishes cleanup before the next query, and discards ownership only when the originating process identity changes. Scene-readiness errors keep the requested switch enabled; unknown code/type failures stop. Do not replay a completed destructor or transfer a live reference between processes.

## Evidence ledger template

For every migrated entry record:

```text
Feature / semantic name:
Old executable hash / profile / reference RVA:
New executable hash / renderer / RVA:
Registration or type anchor:
Signature / mask / match count / section:
Decoded entry and original bytes:
Owner, ABI, parameters, offsets, return type:
Object/reference lifetime and cleanup:
Checks: offline / read-only / mutation / player observation / save-reload:
Failure and its specific applicability:
Source commit / artifact hashes / next unresolved step:
```

Report addresses as RVAs with a fingerprint, not timeless absolute VAs. Reopen a failed route only with new evidence addressing its failure: corrected registration, valid decoded entry, compatible metadata or demonstrated lifecycle handling.
