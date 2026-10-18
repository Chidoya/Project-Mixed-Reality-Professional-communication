# Architectuur: Professional Communication VR

Dit document beschrijft hoe het Unity-project technisch in elkaar zit. Bij elke stap die iets
opzet of verandert (scenario-data, scoring, mood-systeem, ...) komt hier een korte sectie bij:
wat er is, hoe het werkt en hoe je het uitbreidt. Zie ook `GAME_BRIEF.md` voor het concept.

## 1. Platform en versies

| Onderdeel | Keuze |
| --- | --- |
| Unity | 6000.3.9f1 (Unity 6.3), URP |
| Doeltoestel | Meta Quest 3, standalone |
| Build | Android, ARM64, IL2CPP, min. API 32, Vulkan, Linear color space |
| XR runtime | OpenXR via XR Plug-in Management |

## 2. Packages

Toegevoegd bovenop het lege URP-template (`Packages/manifest.json`):

| Package | Versie | Waarvoor |
| --- | --- | --- |
| `com.unity.xr.interaction.toolkit` | 3.6.1 | Rig, interactors (laserpointer), world-space UI-interactie |
| `com.unity.xr.openxr` | 1.18.0 | OpenXR runtime, Meta Quest feature, controllerprofielen |
| `com.unity.xr.management` | 4.7.0 | XR Plug-in Management (kiest OpenXR als loader) |

Dependencies zoals `com.unity.xr.core-utils` en `com.unity.inputsystem` komen automatisch mee
(zie `Packages/packages-lock.json`). Nieuwe packages enkel na akkoord van het team.

## 3. XR-configuratie

- **Loader:** OpenXR voor Android, "Initialize XR on Startup" aan (`Assets/XR/`).
- **OpenXR features (Android):** Meta Quest Support, Oculus Touch Controller Profile,
  Meta Quest Touch Plus Controller Profile (de controllers van de Quest 3).
- Alle instellingen zijn opnieuw toe te passen via het menu
  **ProCommVR > Setup > Configure Project for Quest 3** (`XRProjectSetup.cs`).
- Het actieve build platform wordt lokaal bewaard (in `Library/`), niet in Git. Na een verse
  clone: **File > Build Profiles > Android > Switch Platform**.

## 4. VR-rig en input

- Basis is de **XR Origin (XR Rig)** uit de XRI-sample *Starter Assets*, ongewijzigd
  geïmporteerd in `Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/`.
- Wij gebruiken een **prefab variant**: `Assets/_Project/Prefabs/XR Origin (ProCommVR).prefab`.
  Pas de rig altijd daar aan, nooit in de sample zelf.
- **Interactie:** laserpointer (Near-Far Interactor) op beide controllers.
  - **Trigger = Select** (kiezen, bijvoorbeeld een dialoogoptie) en **UI Press**.
  - **Grip = Activate.** In de XRI-standaard is dit omgekeerd. Daarom gebruikt de rig een
    eigen kopie van de input actions: `Assets/_Project/Settings/Input/ProCommVR XRI Input Actions.inputactions`.
- **Gaze-select staat uit:** de Gaze Interactor is uitgeschakeld (hij staat in de sample ook
  standaard uit) en gaze assistance wordt niet gebruikt.
- Locomotion uit de Starter Assets (thumbstick bewegen, snap turn, teleport) blijft voorlopig aan.
  De speler kan ook gewoon fysiek rondlopen.

## 5. Mappenstructuur

```
Assets/
  _Project/                 alles van ons
    Editor/                 editor-scripts (setup, tools), niet in de build
    Materials/              materialen
    Prefabs/                prefabs, o.a. de XR rig variant
    Scenes/                 scenes
    Scripts/                runtime-code, namespace ProCommVR
    Settings/Input/         input actions
  Samples/                  geïmporteerde package-samples, niet aanpassen
  XR/                       XR Plug-in Management en OpenXR settings (door Unity beheerd)
  Settings/                 URP-assets van het template
```

Volgens `CLAUDE.md` komen er later nog `Art/`, `Audio/` en `Dialogue/` bij onder `_Project/`.
Lege mappen zetten we niet in Git: maak ze aan zodra er iets in komt.
Third-party assets blijven buiten `_Project/`.

## 6. Testscene

`Assets/_Project/Scenes/XRTest.unity` (scene 0 in de Build Settings) dient enkel om de rig te
testen, het is geen content:

- vloer (20 x 20 m) met een basic URP Simple Lit materiaal
- één directional light zonder schaduwen (mobiele performance)
- de XR rig variant, een EventSystem met XR UI Input Module
- **Ray Test Target:** een kubus die van kleur wisselt als je hem met de laser aanwijst en de
  trigger indrukt (`RayTestTarget.cs`, namespace `ProCommVR.XRTest`)
- **Test Canvas:** een world-space knop die dezelfde kubus van kleur doet wisselen (test voor UI-interactie)

**Testen op de Quest 3:** zet het platform op Android, sluit de headset aan (developer mode),
kies **Build and Run** met XRTest als eerste scene. Werkt het: beide controllers tonen een laser,
trigger op de kubus en op de knop wisselt de kleur, grip doet niets op de kubus.

## 7. Opnieuw opbouwen

De setup is volledig gescript (`Assets/_Project/Editor/`), dus reproduceerbaar:

1. **ProCommVR > Setup > Configure Project for Quest 3**
2. **ProCommVR > Setup > 1. Import XRI Starter Assets**
3. **ProCommVR > Setup > 2. Build XR Test Scene** (overschrijft de testscene en de rig variant)

In batchmode: `-executeMethod ProCommVR.Editor.XRProjectSetup.ConfigureForQuest`, daarna
`ProCommVR.Editor.XRTestSceneBuilder.ImportStarterAssets` en
`ProCommVR.Editor.XRTestSceneBuilder.BuildTestScene` (elk in een aparte run).
