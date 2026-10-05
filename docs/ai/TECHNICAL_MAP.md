# Feature, address and data map

[简体中文](TECHNICAL_MAP.zh-CN.md) · [Cold start](COLD_START.md) · [Migration](VERSION_MIGRATION.md)

Runtime baseline v0.1.7. The inventory is generated from public source and requires none of the original author's local paths. A UI switch is not proof of gameplay effect.

## Source navigation

The following files are in [Witcher3Modifier](../../Witcher3Modifier).

| Feature/layer | Files and entry points | Recheck on updates |
|---|---|---|
| Window, settings, sounds, hotkeys | MainForm.cs, MainForm.*, Draft.cs, ToggleSounds.cs | Requested state, busy flags, foreground registration, shutdown and drawing lifetimes |
| Gold and common object chain | GameMoney.cs / WithInventory, Set | Module fingerprint, game/player/inventory vtables, references, entries and currency name |
| Gold multiplier, preservation, internal cleanup | GameInventoryHooks.cs / Set, Read, Rebind | Original add/remove bytes, trampoline, configuration page, default-bolt/appearance NameIds |
| XP multiplier | GameExperience.cs | AddPoints metadata, post-parameter entry, registers/stack, positive delta and settlement |
| Scheduling, addition, deletion, durability/slots | GameItemScheduler.cs / Execute, Give, ExecuteEquipmentNative | IsFocusModeActive trigger, CFunction, page marker, serialized slot, array destructor, native arguments |
| Inventory and multi-select deletion | GameInventory.cs, MainForm.Inventory.cs, MainForm.ItemBatch.cs | Unique ID/name/inventory identity, hidden selection, partial and uncertain results |
| Equipment abilities, values and levels | GameEquipment.cs, GameEquipmentNumbers.cs, GameEquipmentNumbers.Reduction.cs, GameEquipmentLevels.cs | Categories, definitions, base/bonus, removable combinations, final combined readback; level is derived |
| Equip eligibility | GameItemScheduler.EquipmentLevel.cs | HasRequiredLevelToEquipItem bytecode and current-player binding; independent level remains separate |
| Stamina, horse, ammunition, fall protection | GameDebugFacts.cs, GameItemScheduler.cs / ValidateDebugFacts | Fact keys, add/remove registrations, native prefixes and arguments |
| Health, breath, toxicity, kills, durability | GameItemScheduler.Auxiliary.cs | Function metadata, actor references, request/return contract |
| Crafting without materials | GameItemScheduler.Crafting.cs | Dispatch, original return flow, costs and restoration; quantity preservation alone is insufficient |
| Adrenaline | GameItemScheduler.Adrenaline.cs | Attribute/entry, refill and consumption paths |
| Weight bypass | GameWeight.cs | Accessibility/WeightlessItems layer and SetConfigValue; current implementation includes a remote thread, distinct from the scheduler contract |
| Map-marker teleport | GameTeleport.cs, GameTeleport.Code.cs, GameTeleport.StageCode.cs | World, area/marker, navigation, collision surface, request and position confirmation |
| Looting/harvesting | GameItemScheduler.AutoLoot.cs, GameItemScheduler.ActorScript.cs | General-entity query, eligibility, private bytecode, array lifetime, readiness/fatal classification |
| Movement, jump, swimming, enemy speed | GameItemScheduler.PlayerMotion.cs, GameItemScheduler.SwimSpeed.cs, GameItemScheduler.EnemySpeed.cs | Current actor/source ID, animation values, cleanup/restoration; animation rate does not prove linear displacement |
| Gwent, respec, appearance, weather, Cat | GameItemScheduler.Gwent.cs, GameItemScheduler.Respec.cs, GameItemScheduler.Fun.cs | GUI/script type, full VM calls, regional resources, head mounting and return semantics |

## Address inventory and limits

[address-inventory.json](address-inventory.json) contains:

- `profile_selector_sha256`: the 5.00c game fingerprint selected by code, not the trainer artifact hash.
- `profile_mappings`: every reference RVA to 5.00c RVA pair from GameVersion. Equal values still need verification on a new version.
- `literal_sites`: literal arguments passed to GameVersion.Rva, ExecuteEquipmentNative and FunCheck, with source context.
- `direct_module_literals`: hexadecimal candidates added directly to module/baseAddress and potentially outside the mapping; manually determine their role.
- `source_sha256`: source fingerprints used to detect stale inventory, calculated as UTF-8 text without BOM and with LF newlines so Git checkout conversions do not cause false failures.

```powershell
python scripts/check_handoff.py
python scripts/check_handoff.py --update-address-inventory
```

The first performs read-only relative-link, bilingual-entry and inventory checks. The second refreshes the inventory offline, then checks it; neither connects to the game. Review inventory changes alongside migration evidence before committing. This lexical index cannot resolve all variable arguments, immediates inside machine-code templates, RIP-relative targets or virtual calls. It does not validate the game executable.

## Migrate shared roots before leaf features

1. GameMoney.WithInventory resolves GamePointer, then player and inventory through handles and +8. Validate each object's current type and identity. `0xFD60`, `0x1B0`, `0x140/0x148` are field offsets, not RVAs; they can also change.
2. ResolveName currently traverses an FNV-style name hash, pool buckets and nodes. Verify pool layout, hash contract and text together; do not cache NameIds across processes. Currency's current constant 51313 is also a version assumption, so moving functions alone is insufficient.
3. Registration/reflection: FindTick, FindAmmoFunction, FindAuxiliaryFunction and LootGlobalFunction resolve names/owners/flags; RespecProperty/RespecFindField/RespecField resolve field storage from metadata. Metadata vtables and offsets themselves require migration.
4. Entries and ABI: GameVersion selects RVAs; feature implementations check prefixes/types. Long hex templates contain instructions, return targets, replacement markers and type parameters. Do not replace every number that resembles an address.
5. Requests and ownership: RequestLock serializes bridge requests with pending/running/completion and temporary arrays. Validate FindPage and the actual code/marker before reuse. Game arrays follow the game allocator/destructor contract; never give a VirtualAllocEx buffer to its destructor.

An unknown hash currently selects the reference branch. That is code behavior, not universal compatibility. A new profile requires selection changes, affected mappings, original bytes and layouts. Adding only a hash or a global delta is insufficient. Scheduler hook sites are specific instruction positions; not every RVA is a callable function entry.

## Data inputs and regeneration

| Asset | Current role | New-version maintenance |
|---|---|---|
| ItemCatalog.json | Search labels, internal names and types | Check base/DLC definitions and current name pool; a matching label does not prove loaded availability |
| EquipmentLevels.json | Reference search levels | Instance level may vary with generation/attributes; reference levels are not live instance levels |
| Research/equipment_presets.json | Curated abilities, enchantments and numeric candidates | Check category, units, real effects and fixed floors |
| localization/en.json / scripts/localize.py | Generate English from canonical Chinese | Preserve native names, hex, machine code and resource identity; run verify_localization |
| DesignPreview/assets/wolf-school-head-repaired.png | Embedded image | See NOTICE; the source MIT license does not establish image rights |

Not all original full-resource scanning/candidate-generation scripts are public. Included JSON is sufficient to build. Updating catalogs or ability candidates requires analysis of the maintainer's own resources, with base/DLC coverage and parse failures recorded. A prior XML file failed due to local duplicate attributes and omitted abilities; fragment recovery needs counts of successes/failures, and an empty result does not establish absence.

## New-feature investigation order

Identify shared dependencies here, consult the failure IDs, trace target definitions/registrations and real consumption, establish offline contracts, then perform authorized read-only checks, isolated writes, effect checks and any required save/reload test. Record the breakpoint at a failed layer rather than skipping it to expand writes.
