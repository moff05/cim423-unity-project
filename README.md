# CIM 423: Derelict Station (Unity VR)

A short VR horror story set on an abandoned space station, built for CIM 423 at the University of Miami. You dock with a silent station, work through its doors, read the crew's last journal entry, and trigger the artifact that ended them.

- **Build log / writeup:** https://cim423-3d-world.vercel.app
- **Platform:** Meta Quest (OpenXR), with a desktop point-and-click mode for testing in the Editor
- **Engine:** Unity 6000.5.9f1, Universal Render Pipeline

## The Run

Six interactions, in order. Each only responds on its turn.

1. Airlock door
2. Corridor door
3. Terminal (journal entry)
4. Lab door
5. Recorder
6. Artifact (triggers the ending: room shake, alarm, float in space, reset)

An objective HUD and floating hints tell the player what to do next. The door wheels are grabbed and turned in a circle.

## Controls

- **Quest:** point a controller ray and pull the trigger to select. Grab a door wheel and move your hand in a circle to turn it.
- **Editor (desktop):** mouse look, center-screen crosshair, left click to interact. Press `F9` to skip straight to the ending (Editor only).

## Project Layout

```
Assets/
  Scenes/SampleScene.unity     the whole game
  Scripts/
    Core/          GameManager, InteractionSequencer, DialogueUI, AmbienceManager,
                   PlatformRigSelector, desktop input proxy
    Interactions/  doors, TwistHandle (wheel grab), terminal, recorder, artifact,
                   FloatingHintText
    Cinematic/     IntroSequence, HyperspaceEffect
    UI/            start/end screens, OnScreenKeyboard, ObjectiveHUD, WorldSpaceUIFollow
  Audio/ Art/ Materials/ StarSparrow/
  CREDITS.txt      third-party asset attribution
```

## Key Systems

- **InteractionSequencer** gates the six steps so objects ignore input out of order.
- **TwistHandle** tracks hand angle around a wheel's axis to open doors in VR.
- **OnScreenKeyboard** is an in-scene QWERTY keyboard, because Quest's system keyboard doesn't work in fully immersive apps.
- **WorldSpaceUIFollow** keeps world-space UI panels in front of the player and re-centers them when they turn away.
- **ArtifactInteractable** runs the ending. Its coroutines run on a persistent host object so hiding the station can't pause them.

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

See [`Assets/CREDITS.txt`](Assets/CREDITS.txt). Assets from Kenney (CC0), Freesound (CC0), Little Robot Sound Factory (CC-BY 3.0), and Ebal Studios' Star Sparrow spaceship.

## Author

Nicholas Moffett, University of Miami.
