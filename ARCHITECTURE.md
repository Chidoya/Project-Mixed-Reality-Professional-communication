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
| XR runtime | OpenXR via XR Plug-in Management (Android voor de headset, Windows voor Play mode via Quest Link) |

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

- **Loader:** OpenXR voor Android en voor Windows (Standalone), telkens met
  "Initialize XR on Startup" aan (`Assets/XR/`).
- **OpenXR features (Android):** Meta Quest Support, Oculus Touch Controller Profile,
  Meta Quest Touch Plus Controller Profile (de controllers van de Quest 3).
- **OpenXR features (Windows):** enkel Oculus Touch Controller Profile en Meta Quest Touch Plus
  Controller Profile. Meta Quest Support bestaat alleen voor Android (de standalone headset).
  Windows dient enkel om in de editor op Play te drukken met de headset via Quest Link; we
  bouwen geen Windows-versie.
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
    Dialogue/Scenarios/     scenario-data (Scenario assets), zie sectie 8
    Editor/                 editor-scripts (setup, tools), niet in de build
    Materials/              materialen
    Prefabs/                prefabs, o.a. de XR rig variant
    Prefabs/UI/             UI-prefabs, o.a. DialogueBubble
    Scenes/                 scenes
    Scripts/                runtime-code, namespace ProCommVR
    Scripts/Scenarios/      scenario-systeem, namespace ProCommVR.Scenarios
    Settings/Input/         input actions
  Samples/                  geïmporteerde package-samples, niet aanpassen
  TextMesh Pro/             TMP Essential Resources (fonts, shaders), niet aanpassen
  XR/                       XR Plug-in Management en OpenXR settings (door Unity beheerd)
  XRI/                      XR Interaction Toolkit settings (door Unity beheerd)
  Settings/                 URP-assets van het template
```

Volgens `CLAUDE.md` komen er later nog `Art/` en `Audio/` bij onder `_Project/`.
Lege mappen zetten we niet in Git: maak ze aan zodra er iets in komt.
Third-party assets blijven buiten `_Project/`.

## 6. Testscene

`Assets/_Project/Scenes/XRTest.unity` (tweede scene in de Build Settings, na `ScenarioTest`)
dient enkel om de rig te testen, het is geen content:

- vloer (20 x 20 m) met een basic URP Simple Lit materiaal
- één directional light zonder schaduwen (mobiele performance)
- de XR rig variant, een EventSystem met XR UI Input Module
- **Ray Test Target:** een kubus die van kleur wisselt als je hem met de laser aanwijst en de
  trigger indrukt (`RayTestTarget.cs`, namespace `ProCommVR.XRTest`)
- **Test Canvas:** een world-space knop die dezelfde kubus van kleur doet wisselen (test voor UI-interactie)

**Testen via Quest Link (Play mode, sneller):**

1. Installeer op de pc de **Meta Horizon Link**-app (vroeger "Oculus"-app) en log in.
2. In die app: **Settings > General > OpenXR Runtime > Set Meta Horizon Link as active**.
3. Verbind de Quest 3 met een USB-C-kabel (of Air Link) en start **Quest Link** in de headset.
4. Druk in de Unity Editor op **Play**. Het build platform mag op Android blijven staan: de
   editor draait zelf op Windows en gebruikt dus de Windows-instellingen van OpenXR.

Link draait op de pc, dus performance (72 fps) en uitzicht moet je altijd nog op de headset zelf
controleren met een Android-build.

**Testen op de Quest 3 (build):** zet het platform op Android, sluit de headset aan (developer mode),
zet XRTest bovenaan in de Build Settings (of vink ScenarioTest uit) en kies **Build and Run**. Werkt het: beide controllers tonen een laser,
trigger op de kubus en op de knop wisselt de kleur, grip doet niets op de kubus.

## 7. Opnieuw opbouwen

De setup is volledig gescript (`Assets/_Project/Editor/`), dus reproduceerbaar:

1. **ProCommVR > Setup > Configure Project for Quest 3**
2. **ProCommVR > Setup > 1. Import XRI Starter Assets**
3. **ProCommVR > Setup > 2. Build XR Test Scene** (overschrijft de testscene en de rig variant)
4. **ProCommVR > Setup > 3. Import TMP Essentials**
5. **ProCommVR > Setup > 4. Build Dialogue Bubble Prefab**
6. **ProCommVR > Setup > 5. Build Results Screen Prefab**
7. **ProCommVR > Setup > 6. Build Scenario Test Scene** (overschrijft de scene, de placeholder-scenario's
   blijven behouden als ze al bestaan)

In batchmode: `-executeMethod ProCommVR.Editor.XRProjectSetup.ConfigureForQuest`, daarna
`ProCommVR.Editor.XRTestSceneBuilder.ImportStarterAssets` en
`ProCommVR.Editor.XRTestSceneBuilder.BuildTestScene` (elk in een aparte run). Voor het
scenario-systeem: `ProCommVR.Editor.ScenarioTestSceneBuilder.ImportTmpEssentialsAndExit`
(zonder `-quit`, want de package-import is asynchroon), daarna `BuildBubblePrefab`,
`BuildResultsScreenPrefab` en `BuildScenarioTestScene`.

## 8. Scenario's

Scenario's zijn **data**, geen code: elk scenario is een asset (ScriptableObject) in
`Assets/_Project/Dialogue/Scenarios/`. Een nieuw scenario toevoegen vraagt geen nieuwe code.

### Datastructuur (`Assets/_Project/Scripts/Scenarios/`)

| Klasse | Wat |
| --- | --- |
| `Scenario` | Eén gesprek: `Id`, `Title`, `Mood`, `NpcName`, `NpcLine` (openingszin van de NPC) en precies 4 `Options` |
| `DialogueOption` | Eén antwoord van de speler: `Text`, `Score` (verborgen) en `Feedback` (uitleg na de keuze) |
| `Mood` | Sfeer van het scenario: `Neutral`, `Tense`, `Somber`. Voorlopig enkel data; stuurt later licht en muziek aan |

Een scenario heeft altijd precies 4 opties. Zijn het er meer of minder, dan geeft Unity een
waarschuwing en past het de lijst aan naar 4.

**Scores:** de schaal is nog een **voorstel**, af te stemmen met content en de inhoudsexpert:
beste optie `3`, matig `1`, laag `0`, kritische fout `-2`. Per scenario: 1 beste, 2 middelmatige
en 1 duidelijk foute optie (`GAME_BRIEF.md`). De score is nooit zichtbaar tijdens het spel.

### Weergave

- `ScenarioPresenter` (in de scene) toont een `Scenario`: de NPC-zin in een ballon boven de NPC
  en de 4 opties als ballonnen in een 2x2-raster voor de speler, in **willekeurige volgorde**.
- **Opties pas dichtbij:** de NPC-zin is meteen zichtbaar, de 4 opties verschijnen pas als het
  hoofd van de speler binnen **1,5 m** (horizontaal) van de NPC staat (`GAME_BRIEF.md`). Ze
  verschijnen dan tussen de speler en de NPC: 0,7 m voor het hoofd, 25 cm onder ooghoogte, naar
  de speler gericht. Daarna blijven ze staan, ook als de speler terugstapt.
  Instelbaar op de presenter onder **Proximity**: `Npc` (leeg = opties meteen tonen),
  `Options Range`, `Player` (leeg = de main camera, dus de headset), `Place Options In Front Of
  Player` (uit = opties blijven op de `Options Anchor`), `Options Distance` en `Options Height Offset`.
  In `ScenarioTest` staat de NPC (capsule) 2,5 m van het startpunt: stap ongeveer 1 m naar voren
  (of gebruik de linker thumbstick) om de opties te zien.
- De speler kiest met **laser + trigger**. De opties verdwijnen, de feedback verschijnt op hun
  plaats, en de presenter stuurt het event `OptionChosen(scenario, option)`. Scoring en het
  eindscherm kunnen daarop aansluiten (nog niet gebouwd).
- Onder de feedback verschijnt een **verderknop** (ballon). Met een `ScenarioSequence` (sectie 9)
  heet die "Next scenario" of, bij het laatste scenario, "See results". Zonder sequence heet hij
  "Try again" (`Continue Label`) en speelt hij hetzelfde scenario opnieuw.
- `DialogueBubble` (`Assets/_Project/Prefabs/UI/DialogueBubble.prefab`) is één ballon: een
  world-space canvas van 60 x 22 cm met een TextMeshPro-tekst die automatisch kleiner wordt
  bij lange tekst. Wordt hergebruikt voor NPC-zin, opties en feedback.
- De positie van de ballonnen komt van twee lege objecten onder de presenter: `NPC Bubble Anchor`
  en `Options Anchor`. Verschuif die in de scene om de layout aan te passen.

### Een nieuw scenario toevoegen (zonder code)

1. Ga in het Project-venster naar `Assets/_Project/Dialogue/Scenarios/`.
2. Rechtermuisklik > **Create > ProCommVR > Scenario** en geef het een naam, bijvoorbeeld
   `Scenario_DischargedPatient`.
3. Vul in de Inspector in:
   - **Id:** korte unieke naam in kleine letters, bijvoorbeeld `discharged-patient`
   - **Title**, **Mood**, **Npc Name** en **Npc Line** (wat de NPC zegt)
   - **Options:** voor elk van de 4 opties de **Text**, **Score** en **Feedback**
4. Alle tekst in het Engels (`GAME_BRIEF.md`).
5. Testen: open `ScenarioTest`, selecteer **Scenario Sequence** en sleep je nieuwe asset in de
   lijst **Scenarios** (de volgorde van de lijst is de speelvolgorde). Druk op Play met de headset via Quest Link (zie sectie 6) of maak een
   Android-build.
6. Commit het asset samen met zijn `.meta`-bestand.

### Placeholder

`Placeholder_Scenario.asset` ("The waiting patient", mood Neutral) en `Placeholder_Scenario2.asset`
("The upset relative", mood Tense) bevatten **verzonnen** testtekst, enkel om het systeem te testen. Echte scenario's komen van het content-team en de inhoudsexpert.

### Nog niet gebouwd

NPC-reactie (animatie, stem), een eigen omgeving per scenario (kamer, gang: nu speelt alles in
`ScenarioTest` met dezelfde NPC), en het mood-systeem dat licht en muziek aanstuurt.

## 9. Scoring en eindscherm

Volgens `GAME_BRIEF.md` loopt de score verborgen mee en ziet de speler die pas op het eindscherm.
Niets wordt bewaard na het afsluiten.

| Klasse | Wat |
| --- | --- |
| `ScenarioSequence` | Speelt de scenario's uit zijn lijst **Scenarios** na elkaar via de presenter, bewaart elke keuze en toont na het laatste scenario het eindscherm |
| `SessionScore` | De score van één sessie in het geheugen: per scenario een `ScenarioResult` (gekozen optie, score, beste score), plus `Total` en `BestTotal` |
| `ResultsScreen` | Het eindscherm in de wereld (`Assets/_Project/Prefabs/UI/ResultsScreen.prefab`) |

**Verloop:** scenario 1 > keuze > feedback > "Next scenario" > scenario 2 > ... > "See results" >
eindscherm > "Start again" (score terug op 0, eerste scenario).

**Eindscherm:** verschijnt 1,3 m voor de speler, in de kijkrichting. Bovenaan de totaalscore
(bijvoorbeeld `4 / 6`), daaronder per scenario de titel, de score tegenover de beste score, wat
de speler zei, en de feedback bij die keuze. Daaronder een ballon "Start again".

- De beste score per scenario is de hoogste score van zijn 4 opties (`Scenario.BestScore`), dus
  de schaal (nu 3/1/0/-2) kan veranderen zonder code aan te passen.
- Andere scripts kunnen inhaken via `ScenarioPresenter.ScenarioStarted`, `OptionChosen`,
  `Continued` en `ScenarioSequence.Finished`.
- Een scenario toevoegen aan de sessie: sleep het asset in de lijst **Scenarios** van de
  **Scenario Sequence** in de scene.
