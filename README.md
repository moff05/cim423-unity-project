# CIM 423: Day 181 (Unity Virtual Reality)

A virtual reality mystery set on a silent space station, built for CIM 423 at the University of Miami. You dock as a salvage pilot, bring the station's power back, read what the crew left behind, and decide what to do about the thing in the lab. Your ship's AI, ECHO, is not telling you everything.

This is **v2**, the final project. The original six-step version is tagged `v1-final`. What changed and why is in [`AUDIT.md`](AUDIT.md).

- **Build log / writeup:** https://cim423-3d-world.vercel.app
- **Platform:** Meta Quest (OpenXR), with a desktop point-and-click mode for testing in the Editor
- **Engine:** Unity 6000.5.9f1, Universal Render Pipeline

## The Run

Ten steps across six rooms, in order. Each only responds on its turn and glows gold when it is next.

1. Airlock door (twist the wheel)
2. Corridor door (twist the wheel)
3. Engineering hatch (grab the handle, drag it sideways)
4. Power cell (carry it from the rack to the reactor socket)
5. Main bus breaker (pull the lever down); power returns and the crew quarters door opens
6. Terminal (crew roster and the Day 180 journal)
7. Crew-band relay in Crew Quarters (circle the dial to scan; hold still on the door band)
8. Lab door (twist the wheel), in a blackout with only the suit lamp
9. Recorder
10. Final choice: touch the artifact, or pull the containment seal

Five optional crew logs glow blue. Reading three or more changes the ending. There are three endings.

An objective HUD, floating hints and a stuck-hint from ECHO tell the player what to do next.

## Controls

- **Quest:** point a controller ray and pull the trigger to select or grab. Wheels and the dial: grab, then move your hand in a circle. Hatch: grab and drag sideways. Lever: grab and pull down. Power cell: grab, hold and walk it to the socket. The start screen has snap/smooth turn and vignette toggles.
- **Editor (desktop):** mouse look, center-screen crosshair, left click to interact (hold interactions play themselves). Press `F9` to skip straight to the artifact ending (Editor only).

## Project Layout

```
Assets/
  Scenes/SampleScene.unity     the whole game
  Editor/V2Builder.cs          rebuilds every v2 room and prop (menu: Day 181 > Rebuild v2 content)
  Scripts/
    Core/          GameManager, InteractionSequencer, StoryState, StoryDirector,
                   LightingDirector, DialogueUI, AmbienceManager, V2Bootstrap,
                   ComfortSettings, PlatformRigSelector, desktop input proxy
    Interactions/  doors, TwistHandle, HoldInteractable (base) with SlideHatch,
                   PullLever, CarryCell and TuneDial, CrewLog, terminal, recorder,
                   artifact, FloatingHintText
    Cinematic/     IntroSequence, HyperspaceEffect
    UI/            start/end screens, OnScreenKeyboard, ObjectiveHUD,
                   AmbientCaption, WorldSpaceUIFollow
  Resources/V2Audio/           v2 sound effects, loaded by name
  Audio/ Art/ Materials/ StarSparrow/
  CREDITS.txt      third-party asset attribution
AUDIT.md           v1 audit, v2 decisions, what is and is not verified
```

## Key Systems

- **InteractionSequencer** gates the ten steps so objects ignore input out of order, and raises an event when a step completes.
- **HoldInteractable** is the shared base for the grab-and-move interactions. It uses the same held-select mechanism as **TwistHandle**, the pattern already proven on the headset.
- **StoryDirector** runs the beats between interactions: ECHO's lines, the power coming back, the blackout, positional scare sounds and stuck hints.
- **LightingDirector** owns the station's four lighting states, the fog ramp and the suit lamp.
- **StoryState** tracks which crew logs were read; **EndScreenController** picks the ending text from it.
- **AmbientCaption** shows captions that fade without a click, and subtitles every audio-only line.
- **OnScreenKeyboard** is an in-scene QWERTY keyboard, because Quest's system keyboard doesn't work in fully immersive apps.
- **WorldSpaceUIFollow** keeps world-space UI panels in front of the player and re-centers them when they turn away.
- **ArtifactInteractable** runs both final choices. Its coroutines run on a persistent host object so hiding the station can't pause them.

## Building for Quest

Requires Android Build Support, USB debugging enabled on the headset, and `adb` on PATH.

```
# Build the APK from Unity (File > Build Profiles > Android), then:
adb install -r Builds/Android/cim423.apk
adb shell monkey -p com.nmoffett.cim423station -c android.intent.category.LAUNCHER 1
```

Mirror the headset to a laptop with `scrcpy`.

## Packages

XR Interaction Toolkit 3.6.0, OpenXR 1.18.0, Meta OpenXR 2.6.1, Input System 1.20.0, URP 17.5.0, glTFast 6.20.0.

## Credits

See [`Assets/CREDITS.txt`](Assets/CREDITS.txt). Assets from Kenney (CC0), Quaternius (CC0), Poly Haven (CC0), Little Robot Sound Factory (CC-BY 3.0), and Ebal Studios' Star Sparrow spaceship. All v2 sound effects were synthesized for this project; the v2 voice clips were generated with macOS text-to-speech.

## Author

Nicholas Moffett, University of Miami.
