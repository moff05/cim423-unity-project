# Day 181 v2: audit and decisions (2026-11-11)

Written before and during the v2 build, as the plan asked. Everything under "measured" was read from the live scene or the code. Nothing here was tested on the Quest: no headset was attached during this session (see "Not verified").

## v1 baseline (tag `v1-final`, commit 2cfe8f6)

Measured from the scene and scripts:

| | v1 |
|---|---|
| Rooms | 4 (Docking Bay, Corridor, Control Room, Lab) |
| Required steps | 6 |
| Interaction types | 2 (point and trigger, twist a wheel) |
| Optional content | none |
| Endings | 1 |
| Station lights | 7 real-time point lights, no baked lightmaps (`LightmapSettings.lightmaps` = 0) |
| Renderers under Station | about 305 |
| Fog | off |

Problems found, ranked by how much they hurt a first-time player:

1. **Half the "interact with this" glow never rendered.** The gold pulse is written to `_EmissionColor`, but `Mat_Artifact`, `Mat_Recorder` and the terminal's FBX materials had emission compiled out. Only the doors glowed. This matches the old playtest note "no indicators on what to interact with"; the earlier fix (a bloom multiplier) could not have worked on those three objects.
2. **The objective HUD disappeared after Restart.** It was installed with `RuntimeInitializeOnLoadMethod`, which runs once per app launch, so a second playthrough had no HUD.
3. **Dialogue text overflowed its bar.** The body was set to Overflow in an 82 px tall box, so long lines hung out of the bottom of the panel.
4. **The mystery had one clue and one reveal.** Nothing to find, nothing ECHO said was ever contradicted, and the ending was the same whatever the player did.
5. **Every beat was a click-through dialogue box.** 9 of the 9 story beats waited on a click.
6. **Two things to do with your hands**, three of the six steps being the same wheel.
7. **Fear came only from volume.** Lights never changed; only two ambience loops got louder.
8. **Leftovers on screen.** The cut keycard, keypad and socket were still visible, and the supply locker floated in open space behind the start screen because it sat at the scene root.
9. **A shadow pass for nothing.** The 0.05-intensity directional light still cast soft shadows.
10. No comfort options, no hint when stuck.

## What v2 does about it

| | v1 | v2 |
|---|---|---|
| Rooms | 4 | 6 (adds Crew Quarters and Engineering) |
| Required steps | 6 | 10 |
| Interaction types | 2 | 6 (adds slide a hatch, carry and seat a power cell, pull a lever, tune a dial) |
| Optional content | 0 | 5 crew logs, one per crew member |
| Endings | 1 | 3, with 4 end-screen texts |
| Lighting states | 1 | 4 (emergency, powered, blackout with suit lamp, lab only) |
| Captions that need no click | 0 | PA, radio voices, float whispers, stuck hints |
| Station lights | 7 | 9, plus one suit lamp |
| Renderers under Station | about 305 | 369 |

Decisions made without asking, as the plan allowed:

- **Order of the run:** airlock, corridor door, Engineering hatch, power cell, breaker, terminal, relay dial, lab door, recorder, final choice. Engineering sits next to the control room and Crew Quarters off the corridor, so the only backtrack is one short walk, and that walk is the blackout.
- **The player restores the power.** Reyes' log says he pulled the reactor cell to stop the test. ECHO has the player put it back. Found before or after, the log turns a chore into the story.
- **ECHO lies by softening.** "Standard evacuation", "thermal contraction", "old recordings", "equipment noise". Each is contradicted by a log or by what the player just heard.
- **Final choice:** touch the artifact or pull the containment lever. Three or more logs read changes what the float sounds like, what ECHO admits, and the end text.
- **No monsters, no silhouettes.** Kept from v1. Scares are sound direction, light and absence.
- **Carry is not physics.** The cell follows the hand while held and drifts back to its rack if dropped, so it cannot be thrown out of reach. The dial only sweeps forward and wraps. Neither can soft-lock.
- **New hand interactions reuse the wheel's mechanism** (an `XRSimpleInteractable` held while the hand position is read), because that is the one grab pattern already proven on the headset.
- **Kept the Kenney-and-primitives look** and built the new rooms from the same wall, floor and ceiling textures instead of mixing in a second art style.
- **Lighting stays real-time.** Baking was skipped: the room shells are unwrapped primitives, and the new lighting states change every light at run time.
- **Vignette defaults to off** because it could not be checked on the headset.

## Checked in the Editor (desktop rig, automated playthrough)

- All 10 steps complete in order; each new interaction finishes and fires its story beat.
- All three endings reach the end screen with the right text; logs tally is correct.
- Restart reloads cleanly; HUD, captions and lighting come back; story state resets.
- No errors or exceptions in the console across three runs.
- Editor render stats in the start screen: 32 draw calls. In-station numbers were not captured.

## Not verified (needs the Quest)

- Every new grab interaction with real controllers: hatch, cell, lever, dial.
- Frame rate with fog, 9 lights and the suit lamp.
- Caption and HUD placement and text size in the headset.
- Snap/smooth turn toggle and the tunneling vignette.
- The float ending's player freeze and restore (code path unchanged from v1, but re-test it).
- A timed first-time playthrough. v2 has not been played start to finish by a person yet.
