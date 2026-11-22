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
    Audio/Placeholder/      placeholder-geluiden per mood en per emotie (patient view) en het tikken van de countdown (gegenereerde tonen), zie sectie 10, 12 en 18
    Dialogue/Scenarios/     scenario-data (Scenario assets), zie sectie 8
    Dialogue/Shifts/        shift-plannen (ShiftPlan assets), zie sectie 12
    Editor/                 editor-scripts (setup, tools), niet in de build
    Editor/Testing/         Play mode-tests (batchmode), zie sectie 13
    Materials/              materialen
    Materials/PatientRoom/  URP-materialen voor de patiëntenkamer (scenario 1), zie sectie 11
    Materials/Emotions/     deeltjesmaterialen van de kamer-effecten (vuur, regen, druppels, stof) en hun textuur SoftDot.png, zie sectie 18
    Materials/UI/           HUD-materialen die altijd bovenop tekenen (HudOverlay, HudOverlay_Text), zie sectie 17
    Materials/Characters/   kopieën van kledingmaterialen van Liam's personages in andere kleuren (bezoekers: de dochter van scenario 2, de zoon van scenario 3, de vrouw van scenario 4)
    Prefabs/                prefabs, o.a. de XR rig variant
    Prefabs/UI/             UI-prefabs, o.a. DialogueBubble
    Shaders/                eigen shaders: ProCommVR/UI Overlay (UI altijd bovenop, voor de emotie-HUD, sectie 17) en ProCommVR/Particle (deeltjes van de kamer-effecten, sectie 18)
    Prefabs/Player/         de avatar van de speler (prefab variant van Liam's Nurse_Female_Scrubs), zie sectie 15
    Scenes/                 scenes
    Scripts/                runtime-code, namespace ProCommVR
    Scripts/Scenarios/      scenario-systeem, namespace ProCommVR.Scenarios
    Scripts/Atmosphere/     mood-systeem, namespace ProCommVR.Atmosphere
    Scripts/Emotions/       emoties van de patiënt (patient view), namespace ProCommVR.Emotions, zie sectie 14
    Scripts/Player/         de speler: avatar (patient view), namespace ProCommVR.Player, zie sectie 15
    Scripts/Pov/            POV-switch naar de patiënt, namespace ProCommVR.Pov, zie sectie 16
    Scripts/Shift/          gameloop van een shift, namespace ProCommVR.Shift
    Scripts/Testing/        testbediening die enkel in testscenes zit (PatientViewTestControls), namespace ProCommVR.Testing, zie sectie 19
    Scripts/Navigation/     verplaatsen: lift en objecten langs een pad (brancard, NPC), namespace ProCommVR.Navigation
    Settings/Input/         input actions
    Settings/Moods/         mood-profielen (kleur, licht, ambient, muziek per mood), zie sectie 10
    Settings/Pov/           PovSwitchSettings (limieten van de POV-switch), zie sectie 16
    Settings/Emotions/      EmotionWorld_<emotie> (hoe de kamer reageert per emotie) en EmotionWorldVolume (URP-volume), zie sectie 18
  Samples/                  geïmporteerde package-samples, niet aanpassen
  TextMesh Pro/             TMP Essential Resources (fonts, shaders), niet aanpassen
  XR/                       XR Plug-in Management en OpenXR settings (door Unity beheerd)
  XRI/                      XR Interaction Toolkit settings (door Unity beheerd)
  Settings/                 URP-assets van het template
```

Volgens `CLAUDE.md` komt er later nog `Art/` bij onder `_Project/`.
Lege mappen zetten we niet in Git: maak ze aan zodra er iets in komt.
Third-party assets blijven buiten `_Project/`. De personages van Liam staan in `Assets/Character/`
(14 low-poly personages, Mixamo-animaties, zijn eigen setup-script `ProCommVR > Characters > Setup`).
Liam's ziekenhuis staat in `Assets/Hospital/` (zijn builder `ProCommVR > Hospital > Build`, de kamer-prefab, zijn
`PatientCase`-data) en `Assets/Scenes/liam_test_scene.unity`. Dat is Liam's werk: wij passen het niet aan, de shift-scene
gebruikt het enkel als sjabloon (sectie 12).

## 6. Testscene

`Assets/_Project/Scenes/XRTest.unity` (vierde scene in de Build Settings, na `ShiftHospital`, `Scenario01_PatientRoom` en `ScenarioTest`;
daarna komen nog `SampleScene` en `PatientViewTest`)
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
zet XRTest bovenaan in de Build Settings (of vink de andere scenes uit) en kies **Build and Run**. Werkt het: beide controllers tonen een laser,
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
8. **ProCommVR > Setup > 7. Build Scenario 01 Patient Room** (overschrijft `Scenario01_PatientRoom`, zie sectie 11)
9. **ProCommVR > Setup > 8. Build Shift Hospital** (overschrijft `ShiftHospital`, zie sectie 12; het shift-plan blijft
   behouden als het al bestaat). Vereist Liam's ziekenhuis-scene: heeft Liam die opnieuw gebouwd (**ProCommVR > Hospital >
   Build**), draai deze stap dan ook opnieuw.
10. **ProCommVR > Setup > 11. Build Patient View Test Scene** (overschrijft `PatientViewTest`, zie sectie 19; zet de
    scene achteraan in de Build Settings, niet bovenaan)
11. **ProCommVR > Setup > 12. Build Prototype 1 (AI patient, U-ward)** (overschrijft `Prototype1`, zie sectie 20; vereist
    Liam's `liam_u_afdeling.unity`; de scene komt achteraan in de Build Settings)

In batchmode: `-executeMethod ProCommVR.Editor.XRProjectSetup.ConfigureForQuest`, daarna
`ProCommVR.Editor.XRTestSceneBuilder.ImportStarterAssets` en
`ProCommVR.Editor.XRTestSceneBuilder.BuildTestScene` (elk in een aparte run). Voor het
scenario-systeem: `ProCommVR.Editor.ScenarioTestSceneBuilder.ImportTmpEssentialsAndExit`
(zonder `-quit`, want de package-import is asynchroon), daarna `BuildBubblePrefab`,
`BuildResultsScreenPrefab` en `BuildScenarioTestScene`. Voor de patiëntenkamer:
`ProCommVR.Editor.Scenario01SceneBuilder.BuildScene`, voor de testscene van de patient view
`ProCommVR.Editor.Scenario01SceneBuilder.BuildPatientViewTestScene`, voor Prototype1 `ProCommVR.Editor.Prototype1SceneBuilder.BuildScene`. Voor de shift: `ProCommVR.Editor.ShiftHospitalSceneBuilder.BuildScene`.
Elke builder zet zijn scene **eerst** in de Build Settings, behalve die van `PatientViewTest` (achteraan). Bouw je ze allemaal opnieuw, doe `ShiftHospital` dan als
laatste, zodat een headset-build met de shift start.

## 8. Scenario's

Scenario's zijn **data**, geen code: elk scenario is een asset (ScriptableObject) in
`Assets/_Project/Dialogue/Scenarios/`. Een nieuw scenario toevoegen vraagt geen nieuwe code.

### Datastructuur (`Assets/_Project/Scripts/Scenarios/`)

| Klasse | Wat |
| --- | --- |
| `Scenario` | Eén gesprek: `Id`, `Title`, `Mood`, `NpcName`, `NpcLine` (openingszin van de NPC) en precies 4 `Options`. Optioneel: een **locatiekeuze** (`Location Line` en `Locations`), een **tijdslimiet** (`Time Limit` in seconden, `Time Limit Label`, `Time Up Line`, `Time Up Feedback`), een **drukke NPC** (`Busy Line`, wat de NPC zegt zolang hij geen tijd heeft) en een **toeschouwer** (`Bystander Name`, `Bystander Line`: een tweede personage dat meeleeft of meeluistert), zie hieronder |
| `ConversationLocation` | Eén plek waar het gesprek kan doorgaan: `Id` (bijvoorbeeld `consultation-room`), `Name` (op het eindscherm), `Choice Text` (wat de speler zegt), `Score` (verborgen, nu overal 0) en optioneel `Feedback` |
| `DialogueOption` | Eén antwoord van de speler: `Text`, `Score` (verborgen), `Feedback` (uitleg, enkel op het eindscherm) en de NPC-reactie: `Npc Reaction` (korte zin), `Npc Pose` en optioneel `Npc Voice` (audioclip). Optioneel: `Changes Mood` + `Mood After` (de mood van de kamer na deze keuze, bijvoorbeeld `Happy` als het probleem opgelost is), `Fails Task` (dit antwoord laat de taak mislukken, sectie 12) en de reactie van de toeschouwer (`Bystander Reaction`, `Bystander Pose`) |
| `NpcPose` | Lichaamstaal van de NPC na een keuze: `Unchanged`, `Engaged`, `TurnsAway`, `Withdrawn` |
| `Mood` | Sfeer van het scenario (of humeur van een patiënt): `Neutral`, `Tense`, `Somber`, `Happy`, `Alarm` (een taak is mislukt), `Distressed` (zwaar, emotioneel: zacht paars en gedimd). Zie het mood-systeem (sectie 10) |

Een scenario heeft altijd precies 4 opties. Zijn het er meer of minder, dan geeft Unity een
waarschuwing en past het de lijst aan naar 4.

**Scores:** de schaal is nog een **voorstel**, af te stemmen met content en de inhoudsexpert:
beste optie `3`, matig `1`, laag `0`, kritische fout `-2`. Per scenario: 1 beste, 2 middelmatige
en 1 duidelijk foute optie (`GAME_BRIEF.md`). De score is nooit zichtbaar tijdens het spel.

### Weergave

- `ScenarioPresenter` (in de scene) toont een `Scenario`: de NPC-zin in een ballon boven de NPC
  en de 4 opties als ballonnen in een 2x2-raster voor de speler, in **willekeurige volgorde**.
- **Opties enkel dichtbij:** de NPC-zin is meteen zichtbaar, de 4 opties zijn enkel zichtbaar
  zolang het hoofd van de speler binnen **1,5 m** (horizontaal) van de NPC staat (`GAME_BRIEF.md`) **en** op dezelfde
  verdieping: het hoofd moet tussen 0,5 m onder en 2,5 m boven het pivot-punt van de NPC zitten (`Head Height Range`).
  Zonder die grens toonde een patiënt recht boven of onder de speler (kamer 2A boven 1A) ook zijn opties.
  Ze verschijnen tussen de speler en de NPC: 0,7 m voor het hoofd, 25 cm onder ooghoogte, naar
  de speler gericht. Stapt de speler weg (verder dan 1,5 m + 10 cm marge), dan verdwijnen ze weer;
  komt hij terug, dan verschijnen ze opnieuw voor hem, in dezelfde volgorde. De marge voorkomt
  geflikker als de speler net op de grens staat.
  Instelbaar op de presenter onder **Proximity**: `Npc` (leeg = opties meteen tonen),
  `Options Range`, `Options Hide Margin`, `Head Height Range`, `Player` (leeg = de main camera, dus de headset), `Place Options In Front Of
  Player` (uit = opties blijven op de `Options Anchor`), `Options Distance` en `Options Height Offset`.
  In `ScenarioTest` staat de NPC (capsule) 2,5 m van het startpunt: stap ongeveer 1 m naar voren
  (of gebruik de linker thumbstick) om de opties te zien.
- **Locatiekeuze (optioneel):** heeft het scenario `Locations`, dan toont de NPC-ballon eerst de `Location Line` en
  verschijnt er per plek een ballon (in de volgorde van de data, niet geschud), met dezelfde 1,5 m-regel. Na de keuze
  (event `LocationChosen`, `ChosenLocation`) zegt de NPC de `Npc Line` en volgen de 4 opties, weer binnen 1,5 m van
  de NPC. Een `ConversationLocationMover` op de NPC laat hem naar de gekozen plek lopen (een `PathMover` per plek,
  sectie 12); zolang hij loopt verschijnen er geen opties (`HoldChoices`). Een plek zonder pad houdt de NPC waar hij staat.
  Elke plek heeft een `Score` die bij de score van de optie wordt opgeteld. Die staat nu overal op 0: de bron zegt niet
  dat de plek meetelt, maar zo kan het later zonder code. `Interrupt(line)` beëindigt het gesprek zonder keuze
  (bijvoorbeeld als de tijd op is): de keuzes verdwijnen en de NPC-ballon toont de zin.
- De speler kiest met **laser + trigger**. De opties verdwijnen en de speler ziet **enkel de reactie
  van de NPC**: de ballon boven de NPC toont de `Npc Reaction` (cursief) en de `NpcReactor` zet de NPC
  in de `Npc Pose` en speelt de `Npc Voice` af (zie hieronder). Er verschijnt **geen** "goed/fout"-uitleg
  meer direct na de keuze: die staat enkel op het eindscherm (sectie 9). De presenter stuurt het
  event `OptionChosen(scenario, option)`, waar scoring en de NPC-reactie op inhaken.
- Op de plaats van de opties verschijnt een **verderknop** (ballon). Met een `ScenarioSequence` (sectie 9)
  heet die "Next scenario" of, bij het laatste scenario, "See results". Zonder sequence heet hij
  "Try again" (`Continue Label`) en speelt hij hetzelfde scenario opnieuw.
- `DialogueBubble` (`Assets/_Project/Prefabs/UI/DialogueBubble.prefab`) is één ballon: een
  world-space canvas van 60 x 22 cm met een TextMeshPro-tekst die automatisch kleiner wordt
  bij lange tekst. Wordt hergebruikt voor NPC-zin, opties en feedback.
- De positie van de ballonnen komt van twee lege objecten onder de presenter: `NPC Bubble Anchor`
  en `Options Anchor`. Verschuif die in de scene om de layout aan te passen.

### NPC-reactie (`NpcReactor`)

Staat in de scene naast de NPC en luistert naar de presenter. Na een keuze:

- **Houding:** de NPC beweegt in 0,8 s naar het doel-object van de gekozen `Npc Pose` (lijst **Poses**:
  per pose een leeg object in de scene met de positie en rotatie van die houding). Een pose zonder doel
  laat de NPC staan. Bij de start van een scenario springt de NPC terug naar zijn beginhouding.
- **Animatie:** is er een **Animator** ingevuld en heeft de pose een `Animator State` (bijvoorbeeld `Talking`,
  `Angry` of `Sad`, de states van Liam's personages), dan speelt die state. Zonder state krijgt de Animator een trigger
  met de naam van de pose (`Engaged`, `TurnsAway`, `Withdrawn`), maar enkel als de controller die trigger heeft.
- De beginhouding wordt bewaard **ten opzichte van de ouder** van de NPC, en de pose-doelen mogen kinderen van die ouder
  zijn. Zo kan de NPC met alles erop en eraan naar een andere plek lopen (scenario 2).
- **Stem:** is er een **Voice**-AudioSource en heeft de optie een `Npc Voice`-clip, dan wordt die afgespeeld.

Zolang er geen NPC-model is, is de NPC een capsule en zijn de poses enkel verschuivingen en rotaties.

Animaties wisselen met een vaste overgang van 0,25 s (`CrossFadeInFixedTime`). Een overgang relatief aan de cliplengte
duurde bij Liam's lange Idle-clips enkele seconden, waardoor een reactie pas laat zichtbaar werd.

### Toeschouwer, aandacht en een drukke NPC

Bouwstenen voor scenario's met meer dan één personage (vanaf Scenario 3). Allemaal optioneel, en de tekst staat in de data:

| Klasse | Wat |
| --- | --- |
| `NpcAttention` | Wat een personage doet als de speler dichtbij komt (`Range`, standaard 2,5 m, zelfde verdieping), vóór een keuze: naar de `Attention Pose` bewegen (of met `Face Player` naar de speler draaien), een Animator-state spelen en objecten tonen of verbergen (`Show`, `Hide`). Gaat terug bij de start van een scenario. Event `Attended` |
| `BystanderSpeech` | De tekstballon van de **toeschouwer** van het scenario: toont de `Bystander Line` zodra hij aandacht heeft (zijn `NpcAttention`) en na de keuze de `Bystander Reaction` van de optie, meteen of pas als de speler terug bij hem staat (`React When Near`, om de familie de uitkomst te vertellen). De ballon draait naar de speler. Op hetzelfde moment toont zijn `NpcReactor` (met `React As Bystander`) de `Bystander Pose` |
| `BusyNpc` | Een NPC die enkel op het juiste moment aanspreekbaar is. Zolang hij bezig is loopt hij een **ronde** (`Round`, waypoints in een lus), houdt de presenter de opties tegen (`HoldChoices`) en zegt zijn ballon de `Busy Line`; komt de speler binnen 1,8 m, dan stopt hij even, draait naar de speler en gebaart (`Talking`). Na `Busy Time` (14 s) loopt hij naar zijn **bureau** (`To Desk`, het laatste punt is waar hij staat): nu is hij bereikbaar, de ballon toont de `Npc Line` en de opties verschijnen binnen 1,5 m. Komt de speler niet binnen `Desk Time` (20 s), dan vertrekt hij weer. Zodra de opties zichtbaar zijn blijft hij (`Engaged`) en draait hij naar de speler. Geen navigatie-AI: rechte lijnen tussen waypoints. `State`: `Idle`, `Rushing`, `GoingToDesk`, `AtDesk`, `LeavingDesk`, `Engaged` |
| `PrivacyCurtain` | Een privacygordijn tussen twee bedden, dichtgetrokken door een plek-keuze van het scenario (`Location Id`, standaard `curtain`). Het gordijn rekt uit vanaf zijn rail aan de achtermuur: open is het samengeschoven (12%), dicht over de hele rail (1,5 s, de opties wachten zolang). Gaat weer open bij de start van een scenario. Scenario 5 |
| `VitalSignsMonitor` | Een patiëntenmonitor in de wereld met schommelende hartslag, saturatie en bloeddruk (willekeurig rond een basiswaarde, enkel decor, medisch betekenisloos). Scenario 4 |
| `FacePlayer` | Draait een object (enkel rond de verticale as) zodat een ballon eronder leesbaar is vanaf de plek van de speler. Op de ballon van een NPC die rondloopt |

De presenter kreeg daarvoor `Say(line)` (een andere zin in de NPC-ballon tijdens het gesprek) en `OptionsVisible`.

### Een nieuw scenario toevoegen (zonder code)

1. Ga in het Project-venster naar `Assets/_Project/Dialogue/Scenarios/`.
2. Rechtermuisklik > **Create > ProCommVR > Scenario** en geef het een naam, bijvoorbeeld
   `Scenario_DischargedPatient`.
3. Vul in de Inspector in:
   - **Id:** korte unieke naam in kleine letters, bijvoorbeeld `discharged-patient`
   - **Title**, **Mood**, **Npc Name** en **Npc Line** (wat de NPC zegt)
   - **Options:** voor elk van de 4 opties de **Text**, **Score** en **Feedback**, en de NPC-reactie:
     **Npc Reaction** (korte zin), **Npc Pose** en eventueel **Npc Voice**
   - Optioneel **Where to talk:** de `Location Line` en een of meer `Locations` (elk een unieke `Id`). De scene moet
     dan per `Id` weten waar die plek is (`ConversationLocationMover`).
   - Optioneel **Time limit:** `Time Limit` in seconden (0 = geen), het label boven de countdown, de zin van de NPC als
     de tijd op is en de feedback voor het eindscherm. In de shift heeft de taak dan een `TaskCountdown` nodig (sectie 12).
4. Alle tekst in het Engels (`GAME_BRIEF.md`).
5. Testen: open `ScenarioTest`, selecteer **Scenario Sequence** en sleep je nieuwe asset in de
   lijst **Scenarios** (de volgorde van de lijst is de speelvolgorde). Druk op Play met de headset via Quest Link (zie sectie 6) of maak een
   Android-build.
6. Commit het asset samen met zijn `.meta`-bestand.

### Scenario 1: de teleurgestelde patiënt

`Scenario01_DisappointedPatient.asset` ("A Disappointed Patient Who Wants to Go Home", mood `Somber`) bevat
de echte tekst uit het brondocument `10 Scenarios.docx`: NPC-zin, de 4 opties en hun feedback, letterlijk
overgenomen. Scores: A = 3 (beste), D = 1, B = 0, C = -2 (kritische fout).

**Let op:** de `Npc Reaction`-zinnen staan **niet** in het brondocument. Het zijn concepten, afgeleid uit
de "Impact"-regels (A: stemt in om op te staan, B: draait zich weg, C: blij met de valse belofte, D: blijft
alleen en somber). Te laten nakijken door content en de inhoudsexpert. Aanpassen kan in de Inspector.

### Scenario 2: de boze familie

`Scenario02_AngryFamilyMember.asset` ("The Angry Family Member and the Surgery Countdown", mood `Tense`), uit Task 2 van
`Immersive Storytelling Scenario 2.pdf`. NPC: "Patient X's daughter". Verloop: de dochter blokkeert de gang en klaagt dat ze
al dagen geen arts kon spreken (`Location Line`), de speler kiest **"Let's talk right here."** (`corridor`) of **"Let's
step into the consultation room, where we can talk in private."** (`consultation-room`), daarna zegt ze "You don't care
about your patients at all!" (letterlijk uit de bron) en volgen de 4 opties. Tijdslimiet: **90 s** vanaf het moment
dat de speler haar tegenkomt ("Surgical gurney arrives in").

De bron geeft enkel de thema's van de 4 opties. **Tekst, scores, feedback en reacties zijn door Claude geschreven en
nog niet nagekeken door content of de inhoudsexpert:**

| Thema | Score | Reactie (houding, animatie) |
| --- | --- | --- |
| Concrete oplossing (contactgegevens in het dossier, de chirurg belt vandaag, eerlijk over de tijdsdruk) | 3 | "A call today? All right..." (`Engaged`, `Talking`) |
| Professioneel (eigen bevoegdheid, "I'll pass it on") | 1 | "Pass it on. That's what everyone says." (`Unchanged`, blijft `Angry`) |
| Verwijzen naar het artsenschema ("they'll get to you") | 0 | "So my father just has to wait his turn." (`Withdrawn`, `Sad`) |
| Kalmeren ("Please calm down and lower your voice") | -1 | "Calm down? Nobody in this hospital listens to me!" (`TurnsAway`, `Angry`) |

Ook verzonnen: de `Location Line`, de `Time Up Line` ("There goes the gurney, and you still haven't told me anything!")
en de `Time Up Feedback` (met "Replay this case on another day", uit Image 7 van de bron). Beide plekken hebben score 0.

### Scenario 3: de arts en de familie

`Scenario03_DoctorAndFamily.asset` ("Two-Way Communication Between a Doctor and a Family Member", mood `Neutral`), uit Task 3
van `Immersive Storytelling Scenario 2.pdf`. NPC: de arts, "Dr. Maes". Toeschouwer: "Patient X's son". Verloop: de zoon wacht
aan de verpleegpost en zegt "You've been making promises for days but the doctor never shows up!" (letterlijk uit de bron,
`Bystander Line`). De arts rent tussen de kamers en zegt "I don't have time right now." (letterlijk, `Busy Line`). Aan zijn
bureau, met papierwerk, is hij bereikbaar: "Yes? Make it quick, please. I still have two patients to see before noon." (`Npc
Line`) en de 4 opties zijn wat de verpleegkundige tegen de arts zegt. Daarna gaat de speler terug naar de zoon, die reageert
op de uitkomst (bron: Image 7).

De bron geeft enkel het doel (respectvol tegenover de collega, opkomen voor de familie, een concreet tijdstip). **Tekst,
scores, feedback, reacties en de naam van de arts zijn door Claude geschreven en nog niet nagekeken door content of de
inhoudsexpert:**

| Thema | Score | Arts (houding, animatie) | Mood daarna | Zoon (houding, animatie) |
| --- | --- | --- | --- | --- |
| Kort, respectvol, waarom het dringend is, vraagt een concreet tijdstip | 3 | "Four o'clock, in the family room." (`Engaged`, `Talking`) | `Happy` (opgelost) | "Four o'clock with the doctor? ...Thank you." (`Engaged`, `Talking`) |
| Beleefd maar vaag ("when you have a moment") | 1 | "I'll try, but I can't promise anything today." (`Unchanged`) | blijft `Neutral` | "That's what you said yesterday." (`Unchanged`, `Angry`) |
| Conflict vermijden, de familie uitstellen tot morgen | 0 | "Fine. Tomorrow, then." (`TurnsAway`, terug naar zijn papierwerk) | blijft `Neutral` | "Tomorrow? Again?" (`Withdrawn`, `Sad`) |
| Verwijtend en bevelend tegenover de collega | -2 | "Don't tell me how to do my job." (`TurnsAway`) | `Tense` | "So now the nurse and the doctor are arguing?" (`TurnsAway`, `Angry`) |

De bron geeft geen kleuren voor deze taak: de mood is `Neutral` tot de uitkomst en `Happy` (groen, "calm/stable" volgens de
kleurcode uit de bron) als het opgelost is. Het informeren van de zoon telt niet mee in de score: "Finish" bij de arts sluit
de taak af, de reactie van de zoon volgt wanneer de speler terug bij hem komt.

### Scenario 4: de familie van een patiënt met een slechte prognose

`Scenario04_PoorPrognosis.asset` ("Family of a Patient with a Poor Prognosis", mood `Distressed`), uit Task 4 van
`Immersive Storytelling Scenario 2.pdf`. NPC: "Patient X's wife". De patiënt slaapt in bed, zijn vrouw waakt op de stoel
ernaast, de kamer is gedimd en zacht paars, op de monitor schommelen de vitale functies (bron: Image 1 en 2). Komt de
speler dichtbij, dan staat ze op, reikt naar de verpleegkundige en vraagt "He is going to get better, isn't he? ...We
really want to take him home." (de twee zinnen uit de bron, met "he"). Daarna de 4 opties.

De bron geeft het verloop van de goede uitkomst (Image 5 tot 7). **Tekst, scores, feedback en reacties zijn door Claude
geschreven en nog niet nagekeken door content of de inhoudsexpert:**

| Thema | Score | Reactie (houding, animatie) | Mood daarna |
| --- | --- | --- | --- |
| Emotie erkennen, eerlijk zonder valse hoop, de arts vandaag laten komen, even blijven | 3 | "Yes. Please ask the doctor to come." Ze gaat weer zitten (`Engaged`, `Sitting`) | `Somber` (blauw, "a space of acceptance") |
| Juist over de eigen bevoegdheid, maar kil en passief | 1 | "Not allowed... So I just sit here and wonder." (`Withdrawn`, naar haar man, `Sad`) | blijft `Distressed` |
| Vage geruststelling ("let's stay positive") | 0 | "Everything you can... but what does that mean?" (`TurnsAway`, naar het raam, `Crying`) | blijft `Distressed` |
| Valse hoop ("of course he'll get better") | -2 | "Really? Oh, thank God..." Ze blijft staan (`Unchanged`, `Talking`) | blijft `Distressed` |

Niet gebouwd: het glas water uit Image 7 (de verpleegkundige biedt het aan bij het weggaan) en een piepend monitorgeluid.

### Scenario 5: de verbaal agressieve patiënt en privacy

`Scenario05_AggressivePatient.asset` ("Verbally Aggressive Patient and Privacy in a Shared Room", mood `Tense`), uit Task 5
van `Immersive Storytelling Scenario 2.pdf`. NPC: "Patient X" (vermoeden van hiv). Toeschouwer: "Patient in the next bed".
Verloop: bij het binnenkomen komt Patient X roepend op de verpleegkundige af: "You aren't doing anything, I'm discharging
myself!" (letterlijk uit de bron, `Location Line`), en de buurman legt zijn krant neer en luistert (Image 2). De speler kiest
eerst: een stap terug, stil praten en het privacygordijn dichttrekken (`curtain`, score 1, Image 4), of meteen antwoorden in
de open kamer (`open-room`, 0). Daarna "Three days of tests and nobody tells me anything. And now you want even more blood?
Forget it!" en de 4 opties over de extra bloedtest (Image 5). Hier telt de plek wel mee: de bron maakt het gordijn deel van
de goede aanpak. Beste score: 3 + 1 = 4.

**Tekst, scores, feedback en reacties zijn door Claude geschreven (behalve de zin uit de bron) en nog niet nagekeken door
content of de inhoudsexpert:**

| Thema | Score | Patient X | Buurman | Mood daarna | Uitkomst |
| --- | --- | --- | --- | --- | --- |
| Frustratie erkennen, diagnose aan de arts laten, de arts komt vandaag, uitleggen waarom de test helpt | 3 | "Fine. One more test. But then I want answers." (`Engaged`, rustiger, `Idle`) | blijft liggen, zegt niets | `Neutral` | verzorgd |
| Juist maar afwerend ("standard procedure") | 1 | "That's all I ever hear in here." (`Unchanged`, `Angry`) | blijft liggen | blijft `Tense` | verzorgd |
| Waarschuwen en druk zetten ("against medical advice") | 0 | weigert de test, wacht op de arts (`TurnsAway`, naar de deur) | blijft liggen | `Alarm` | **mislukt** |
| De diagnose hardop noemen ("to confirm the HIV result") | -2 | "You just said that out loud?! ... I'm out of here!" (`TurnsAway`, naar de deur) | "HIV...? Is that contagious? ..." en hij stapt overstuur uit bed (`Withdrawn`, `Sad`) | `Alarm` | **mislukt** |

De twee mislukte uitkomsten volgen Image 6 en 7 van de bron: de patiënt wordt luider en wil weg, de buurman heeft het
gehoord, rode knipperlichten en een waarschuwingsgeluid, en de speler moet het "op een andere dag" opnieuw proberen.

### Placeholder

`Placeholder_Scenario.asset` ("The waiting patient", mood Neutral) en `Placeholder_Scenario2.asset`
("The upset relative", mood Tense) bevatten **verzonnen** testtekst, enkel om het systeem te testen. Echte scenario's komen van het content-team en de inhoudsexpert.

### Nog niet gebouwd

Een echt NPC-model in `ScenarioTest` en `Scenario01_PatientRoom` (daar nog een capsule; de shift-scene in sectie 12
gebruikt al de personages van Liam), opgenomen stemmen, en echte muziek of geluiden voor de moods, het tikken van
de countdown en het waarschuwingsgeluid (de infrastructuur staat, nu met placeholder-tonen, sectie 10). Scenario 2: de
patiënt die naar de OK moet (de speler helpt hem in de bron met zijn operatieschort) zit niet in de scene, en "opnieuw
spelen op een andere dag" bestaat nog niet (er is maar één shift); het eindscherm zegt het enkel.

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
de speler zei, en de feedback bij die keuze. Dit is de **enige plek** waar de feedback (waarom een
keuze goed of fout was) getoond wordt; tijdens het spel ziet de speler enkel de NPC-reactie. Daaronder een ballon "Start again".

- De beste score per scenario is de hoogste score van zijn 4 opties (`Scenario.BestScore`), dus
  de schaal (nu 3/1/0/-2) kan veranderen zonder code aan te passen.
- Andere scripts kunnen inhaken via `ScenarioPresenter.ScenarioStarted`, `OptionChosen`,
  `Continued` en `ScenarioSequence.Finished`.
- Een scenario toevoegen aan de sessie: sleep het asset in de lijst **Scenarios** van de
  **Scenario Sequence** in de scene.

## 10. Mood-systeem

Een mood is één gegeven met een kleur: de sfeer van een scenario, of het humeur van een patiënt in een kamer.
Het systeem is opgesplitst in **data** (wat is de mood nu) en **lezers** (wat doet de scene ermee), zodat de data
niets weet van licht, muziek, UI of bordjes.

| Klasse | Wat |
| --- | --- |
| `Mood` (enum) | `Neutral`, `Tense`, `Somber`, `Happy`, `Alarm` |
| `MoodProfile` | Asset per mood: `Mood`, `Color` (de kleur van de mood: deurbordje en kamerlicht), `Flash Rate` (knipperen van het kamerlicht per seconde, 0 = vast licht), `Room Light Intensity` (helderheid van het kamerlicht als veelvoud van zijn eigen intensiteit, 1 = ongewijzigd), `Light Color`, `Light Intensity`, `Ambient Color`, `Sound` (loopend geluid: muziek of ambiance, leeg = stilte) en `Sound Volume` |
| `MoodController` | **Enkel data.** Houdt de huidige mood van één scene, kamer of patiënt bij (`CurrentMood`, `CurrentProfile`, `CurrentColor`) en meldt elke wijziging via het event `MoodChanged`. De mood komt van code (`ApplyMood`), van een gekoppelde `ScenarioPresenter` (volgt de mood van elk scenario dat start) of van `Initial Mood` bij de start |
| `MoodAtmosphere` | Lezer: zet het hoofdlicht en het ambient licht in 1,5 s op het profiel van de mood |
| `MoodAudio` | Lezer: speelt de `Sound` van de mood op de AudioSource van zijn object en **kruisfadet** (2 s) naar het nieuwe geluid als de mood verandert. Maakt daarvoor zelf een kopie van die AudioSource, zodat oud en nieuw geluid even overlappen |
| `MoodRoomLight` | Lezer: geeft één licht in een kamer de kleur van de mood (`Strength`: 0 = wit, 1 = de volle kleur) en de `Room Light Intensity` van het profiel. Heeft het profiel een `Flash Rate`, dan knippert het licht tussen 2 keer (`Flash High`) en 0,1 keer (`Flash Low`) zijn eigen intensiteit |
| `RoomSign` | Lezer (sectie 12): het deurbordje toont de kleur van de mood naast de patiënt |

- De profielen staan in `Assets/_Project/Settings/Moods/` (`Mood_Neutral` grijs, `Mood_Tense` oranje, `Mood_Somber`
  blauw, `Mood_Happy` groen, `Mood_Alarm` rood en knipperend, 1,5 keer per seconde, `Mood_Distressed` paars en het
  kamerlicht op 80%; paars voor "distressed/challenging" komt uit de kleurcode van de bron). Het zijn **placeholders**: kleur,
  licht en een placeholder-geluid.
- **Mood na een keuze:** een optie met `Changes Mood` zet de mood van de kamer (`PatientRoom`) of de scene (een `MoodController`
  met een presenter) op `Mood After`, bijvoorbeeld `Happy` als het probleem in Scenario 3 opgelost is. Geen extra systeem:
  bordje, lichten en geluid volgen zoals altijd.
- **Alarm** is de faalstatus van een taak (sectie 12): er komt geen apart systeem voor rode lichten en een
  waarschuwingsgeluid, de taak zet gewoon zijn mood op `Alarm` en het bordje, de lichten en het geluid volgen.
- **Placeholder-geluiden:** `Assets/_Project/Audio/Placeholder/Mood_<mood>_Placeholder.wav`, zachte gegenereerde tonen
  van 6 s die naadloos loopen (Happy: majeurakkoord, Somber: laag mineurakkoord, Tense: dissonant met tremolo, Neutral:
  zachte brom, Distressed: lage, zware brom met een trage zweving), volume 0,25. `Mood_Alarm_Placeholder.wav` is een tweetonige pieptoon (880 en 660 Hz, 2 s) als
  waarschuwingsgeluid; `Countdown_Tick_Placeholder.wav` is één tik per seconde voor de countdown (sectie 12). Geen
  echte muziek, geen licentie nodig. De Setup-builders vullen deze clip enkel in bij een
  profiel waarvan `Sound` nog leeg is; een gekozen geluid wordt nooit overschreven.
- In `ScenarioTest` en `Scenario01_PatientRoom` staat een **Mood Controller** (gekoppeld aan de presenter) met een
  `MoodAtmosphere`, een `MoodAudio` en een AudioSource (2D, loop). Het tweede placeholder-scenario staat op `Tense`, dus in
  `ScenarioTest` veranderen licht en geluid merkbaar bij "Next scenario".
- Het ambient licht wordt op **Flat** gezet zodra een `MoodAtmosphere` een mood toepast.
- Een mood zonder profiel laat de mood ongewijzigd en geeft een waarschuwing in de Console.

**Waarom het effect in Scenario 1 niet opviel (onderzocht op 2026-10-19):** het systeem werkt wel. Een Play mode-meting
(`RunScenario01Mood`, sectie 13) toont dat het hoofdlicht in 1,5 s van wit/intensiteit 1,0 naar blauwig/0,6 gaat en het
ambient licht naar het Somber-profiel. Maar Scenario 1 is het enige scenario in die scene, dus de mood wordt één keer
toegepast, in de eerste 1,5 s na het laden (vaak nog tijdens het opzetten van de headset of de Link-overgang). Er is
dus nooit een zichtbare overgang, enkel een iets donkerder, blauwere kamer zonder iets om mee te vergelijken. Daarbij
gaat het ambient licht van Somber (0,25/0,28/0,36) zelfs iets **omhoog** tegenover de start van de scene (0,21/0,23/0,26),
wat het dimmen van het hoofdlicht deels tenietdoet. Wil je de mood duidelijker: maak het Somber-profiel sterker
(lagere `Light Intensity`, donkerder `Ambient Color`) of gebruik een gekleurd kamerlicht met `MoodRoomLight`, zoals in
de shift-scene.

**Geluid toevoegen of vervangen:** zet het audiobestand in `Assets/_Project/Audio/`, selecteer het profiel in
`Settings/Moods/` en sleep de clip in **Sound**. Stel **Sound Volume** in. Geen code nodig; elke `MoodAudio` die deze mood
toont speelt het nieuwe geluid. De clip hoeft in de importinstellingen niet op Loop te staan: `MoodAudio` loopt zelf.

**Een nieuwe mood toevoegen** (bijvoorbeeld `Anxious`): voeg de waarde toe **achteraan** in
`Mood.cs` (achteraan, anders schuiven de bestaande scenario's van mood), maak via
**Create > ProCommVR > Mood Profile** een profiel, kies de nieuwe mood, zijn kleur en zijn geluid, en sleep het profiel in
de lijst **Profiles** van elke Mood Controller die de mood moet kennen. De lezers (bordje, kamerlicht,
atmosfeer, geluid) hoeven niet aangepast te worden.

**Performance (Quest 3):** de mood verandert enkel kleur en intensiteit van bestaande lichten,
er komen geen extra lichten of schaduwen bij. Als de scenes later baked lighting gebruiken,
werkt de lichtkleur van de mood enkel op realtime of mixed lichten; dan kan een mood ook via
post-processing (color grading) of light probes werken. Dat beslissen we samen met de echte
omgevingen.

## 11. Scene: Scenario 1, patiëntenkamer

`Assets/_Project/Scenes/Scenario01_PatientRoom.unity` speelt `Scenario01_DisappointedPatient`. Staat tweede in
de Build Settings, na `ShiftHospital` (sectie 12). `ScenarioTest` blijft bestaan als technische testscene.
De scene wordt volledig opgebouwd door `Assets/_Project/Editor/Scenario01SceneBuilder.cs` (menu
**ProCommVR > Setup > 7. Build Scenario 01 Patient Room**). Pas de layout dus liefst daar aan en bouw opnieuw;
handmatige wijzigingen in de scene gaan verloren bij een rebuild.

**Layout** (`GAME_BRIEF.md`): kamer van 4 x 4 m (x -2..2, z 0..4), plafond op ongeveer 3 m. Deur in de zuidmuur,
bed met het hoofdeinde tegen de noordmuur (tegenover de deur), raam in de oostmuur. De speler start binnen aan
de deur (-1, 0, 0,5), naar de patiënt gericht, op ongeveer 2,6 m van de patiënt, en kan vrij tot bij het bed
lopen. De opties verschijnen binnen 1,5 m van de patiënt (sectie 8).

**Patiënt (placeholder):** er is nog geen NPC-model. Patiënt X is een capsule (zelfde materiaal als in
`ScenarioTest`) die voorovergebogen op de rand van het bed zit en naar het raam kijkt, zoals in het brondocument.
Een donker blokje ("Face Direction") toont naar waar hij kijkt. De `NpcReactor` op `Patient X (placeholder)`
heeft drie poses (lege objecten onder `Poses`): `Engaged` (rechtop, naar de verpleegkundige), `TurnsAway`
(rug naar de verpleegkundige) en `Withdrawn` (nog verder voorover, naar het raam). Een echt model vervangt
later de capsule: sleep het in `Body` van de reactor, geef het een Animator met triggers met dezelfde namen.

**Colliders:** de kamer-modellen hebben geen colliders. Onder `Room/Colliders` staan onzichtbare boxen voor de
vloer (anders valt de rig) en de 4 muren (thumbstick-beweging blijft in de kamer).

### Gebruikte assets

| Map | Waarvoor |
| --- | --- |
| `Assets/ThirdParty/Sketchfab/HospitalRoom_UgurYksk/` | Kamerstructuur: vloertegels, muren, de muur met deuropening, de muur met raam, plafondlampen. Ook de ziekenhuisprops in dezelfde stijl: infuuspaal met zak, patiëntenmonitor, nachtkastje, bedtafel |
| `Assets/ThirdParty/Sketchfab/HospitalDoor_anasrar/` | De deur, passend gemaakt in de deuropening van de UgurYksk-muur (de deur van de kit zelf wordt niet gebruikt) |
| `Assets/ThirdParty/Sketchfab/HospitalBed_anshartis/` | Het bed van de patiënt (2,1 m lang) |
| `Assets/ThirdParty/GRADD/HospitalRoom/` | Ziekenhuisprops: ladekast op wieltjes en afvalbak voor medisch afval (biohazard) |
| `Assets/ThirdParty/Kenney/FurnitureKit/` | Generieke, niet-ziekenhuisprops: bezoekersstoel (`chair`) en plant (`pottedPlant`) |

Credits van al deze assets staan in `CREDITS.md`.

**Hoe de kit gebruikt wordt:** `HospitalScene.dae` (UgurYksk) is geen kant-en-klare kamer maar een modulaire kit:
losse stukken van één tegel naast elkaar, op ongeveer 1/6 schaal. De builder kopieert de nodige stukken op naam
(bijvoorbeeld `Ground.001`, `Wall3.001`, `DoorWall.001`, `WindowWall.001`) en schaalt ze zodat één vloertegel
2 x 2 m is. Alles wordt geplaatst op basis van de echte mesh-grenzen, dus de vreemde pivots en rotaties van de
modellen spelen geen rol. De GRADD-set is één groot OBJ-bestand in inch met alle props naast elkaar: de builder
houdt enkel de meshes van één prop over en schaalt van inch naar meter.

**Materialen:** de UgurYksk-kit gebruikt de oude Standard-shader, die in URP roze is. De builder maakt daarom
eigen materialen in `Assets/_Project/Materials/PatientRoom/` (`Kit_<naam>.mat`, URP Simple Lit) met de albedo- en
emissietexturen van de kit. Normal maps zijn bewust weggelaten (performance). Het raam is opaak (geen
transparantie, duur op de Quest): het glas toont de blauwgrijze tint uit de texture. De bestanden in `ThirdParty/`
zelf zijn niet aangepast.

**Performance (Quest 3), nog te controleren op de headset:** het bed alleen is ongeveer 28.000 triangles, de hele
kamer blijft ruim onder de richtwaarde van 100k. De texturen van de kit zijn 2048 x 2048; als het geheugen krap
wordt, kan de max size van de gebruikte texturen naar 1024 (in de importinstellingen, na overleg). Eén directional
light zonder schaduwen (het mood-systeem stuurt het aan), nog geen baked lighting.

## 12. Shift (gameloop) en scene `ShiftHospital`

De speler werkt een shift af: briefing, een aantal patiënten verzorgen, eindscherm. Taak 2 is **Scenario 2** (de boze
dochter in de gang van verdieping 3, `Tense`), taak 3 is **Scenario 3** (de zoon aan de verpleegpost en de drukke arts op
verdieping 2, `Neutral`), taak 4 is **Scenario 4** (de vrouw van een patiënt met een slechte prognose in kamer 1B,
`Distressed`) en taak 5 is **Scenario 5** (de agressieve patiënt in de gedeelde kamer 1C, `Tense`). Taak 1 speelt
Scenario 1 (`Happy`). Alle vijf taken uit de bron zitten dus in de shift. Taak 1 (kamer 1A) heeft ook de **patient view** (Y, zie "Taak 1"
hieronder).

### Verloop

1. **Briefing:** de speler start in de **lobby** van Liam's ziekenhuis, voor de receptiebalie (die dient als briefing room).
   De taken uit het shift-plan zijn al aan de kamers toegewezen: de deurbordjes en de shift-borden tonen de patiënten,
   de kamerlichten hebben de kleur van hun humeur.
2. **Start:** de speler richt de laser op de **computer op de receptiebalie** en drukt de trigger in. Alle kamers starten
   hun gesprek en de timer loopt.
3. **Taken:** de patiënten liggen op verdieping 1 (kamers 1A, 1B, 1C); taak 2 speelt in de **gang van verdieping 3** en
   taak 3 aan de **verpleegpost van verdieping 2** (zie "Taak 2" en "Taak 3" hieronder). De speler gaat met de **lift**:
   laser + trigger op een knop van het liftpaneel naast de liftdeur brengt hem naar die verdieping. In elke kamer zit een patiënt. De NPC-zin hangt boven de patiënt, de 4 opties verschijnen binnen 1,5 m
   (zoals in sectie 8). Na de keuze reageert de patiënt (houding, zin) en verschijnt de ballon **"Finish"**. Pas daarna
   is de patiënt "verzorgd": het bordje toont "Cared for" en de borden tellen mee.
4. **Einde:** zodra alle taken afgelopen zijn (verzorgd of mislukt) **of** de timer op 0 staat (wat eerst komt) stoppen
   alle gesprekken en verschijnt het **Shift report** 85 cm voor de speler: per kamer de score en de feedback (en waar
   het gesprek plaatsvond), "failed: time ran out" met de feedback van het scenario, of "not cared for".
   Het spel stopt daar. De ballon **"Close game"** sluit de app (in de editor stopt hij Play mode). Er is nog geen
   volgende shift.

### Taak 2: de gang, de locatiekeuze en de countdown

1. De dochter staat midden in de gang van verdieping 3, tussen de lift en kamer 3B, met haar gezicht naar de lift (en
   een collider, zodat de speler niet door haar loopt). Boven de gang hangt de countdown, als een digitale wandklok.
2. **Countdown:** de taak krijgt bij de start van de shift de `Time Limit` van het scenario (90 s), maar de tijd loopt
   pas als de speler binnen 4 m van de dochter komt, op dezelfde verdieping (dus meteen bij het uitstappen van de lift).
   Dan verschijnt de tijd ("Surgical gurney arrives in 1:29") en tikt het (3D, tot 8 m). Onder 15 s wordt de tijd rood.
3. **Locatiekeuze:** binnen 1,5 m klaagt de dochter en kiest de speler: blijven (`corridor`) of naar de spreekkamer
   (`consultation-room`). Bij de spreekkamer loopt ze voor (1,1 m/s, door de deur van 3B naar de tafel); de opties
   verschijnen pas als ze er is en de speler bij haar staat. De keuze wordt bewaard (`PatientRoom.ChosenLocation`) en
   staat op het eindscherm ("Talked in: Consultation room"), maar telt nu niet mee in de score.
4. **Antwoorden op tijd:** de countdown stopt (tikken en display weg) zodra de speler een optie kiest, ook bij een
   slecht antwoord. "Finish" sluit de taak af: uitkomst `Completed`, met de score van de optie (goed of slecht gereageerd,
   zoals bij Scenario 1). De mood blijft `Tense`.
5. **Tijd op:** kiest de speler geen optie voor de countdown op 0 staat, dan **mislukt** de taak (`FailedTimeUp`), los van
   een slecht antwoord: de opties verdwijnen, de dochter roept de `Time Up Line`, de brancard rolt van het oosteinde van de
   gang naar de lift (stopt na 3B), en de mood van de taak gaat naar **`Alarm`**: de gang- en spreekkamerlichten worden rood
   en knipperen, het bordje kleurt rood met "Time ran out", en het waarschuwingsgeluid speelt rond de dochter. De shift
   loopt verder; op de borden staat de taak als "Failed: time ran out", apart van "Cared for".

### Taak 3: de verpleegpost en de drukke arts

1. Uit de lift op verdieping 2 staat de zoon aan de verpleegpost (een bureau met computer en dossiers tegen de zuidmuur,
   bordje "Nurses' station"). Binnen 3 m stapt hij naar de speler, draait zich naar hem en klaagt (`Angry`, zijn ballon).
2. De arts loopt een ronde langs de noordkant van de gang, tussen de deuren van 2C en 2A (`BusyNpc`). Opties zijn er
   niet, ook niet binnen 1,5 m: stapt de speler in zijn weg, dan stopt hij even, draait naar de speler, gebaart en
   zijn ballon zegt "I don't have time right now."
3. Na zijn ronde loopt hij naar zijn bureau in **kamer 2C** (het bureau van de arts, deur open, bordje "Doctor's office")
   en bekijkt papierwerk. Vanaf de deur zijn er nog geen opties; binnen 1,5 m wel, en hij draait zich naar de speler.
4. Na de keuze reageert de arts (zin, houding) en verschijnt "Finish". Bij het beste antwoord worden het ganglicht bij de
   post, het licht van 2C en het bordje groen (`Happy`).
5. Terug bij de verpleegpost hoort de zoon de uitkomst: zijn ballon toont zijn reactie en zijn houding verandert.

### Taak 4: de slechte prognose (kamer 1B)

1. Vanuit de deur: een gedimde, zacht paarse kamer. De patiënt (`Patient_Male_Old`) slaapt in bed, zijn vrouw
   (`Patient_Female_Old` in bezoekerskleren) zit op Liam's stoel naast het bed, naar het bed gericht. Op het nachtkastje
   staat een monitor met schommelende hartslag, saturatie en bloeddruk. Geen opties vanaf de deur.
2. Binnen 2,4 m staat ze op, stapt naar de deur toe, draait zich naar de speler en reikt naar hem (`Talking`); haar ballon
   toont haar vraag en de 4 opties verschijnen binnen 1,5 m.
3. Beste antwoord: ze gaat weer zitten (`Sitting`) en de kamer wordt blauw (`Somber`) en niet meer gedimd. Andere antwoorden:
   ze blijft staan (hoopvol, naar haar man of naar het raam) en de kamer blijft paars.

### Taak 5: de gedeelde kamer (kamer 1C)

1. In kamer 1C staan twee bedden: Liam's bed aan de raamkant (Patient X) en een kopie aan de andere kant, waarin de buurman
   ligt te lezen (een krant boven zijn borst). Tussen de bedden hangt een privacygordijn, open en samengeschoven tegen de
   achtermuur. Liam's stoel en nachtkastje staan uit.
2. Vanuit de gang gebeurt nog niets. In de deuropening komt Patient X roepend op de speler af (`Angry`) en legt de buurman
   zijn krant neer (`NpcAttention`). De twee keuzes verschijnen binnen 1,5 m.
3. Kiest de speler het gordijn, dan schuift het in 1,5 s dicht tussen de bedden; daarna de 4 opties.
4. Beste antwoord: Patient X kalmeert, de kamer wordt `Neutral`. Een antwoord met `Fails Task`: hij loopt naar de deur, de
   kamer gaat op `Alarm` (rood knipperlicht, waarschuwingsgeluid), na "Finish" is de taak **mislukt** (`FailedAnswer`):
   bordje "Not resolved", op de borden "Failed: not resolved", op het eindscherm "failed: not resolved" met de feedback.
   Bij het hardop noemen van de diagnose staat de buurman overstuur op en zegt zijn zin.

### Taak 1: de patient view (kamer 1A)

Scenario 1 heeft in kamer 1A dezelfde patient view als in `Scenario01_PatientRoom` (sectie 14 tot 18). De builder
(`ShiftHospitalSceneBuilder.BuildPatientView`, enkel voor de kamer in `PatientViewRoom` = `"1A"`) zet:

1. een `EmotionHolder` op de patiënt (`Patient_Male`, zittend op de stoel), gekoppeld aan de presenter van 1A: bij de start
   van de shift (`ScenarioStarted`) de Initial Emotion van Scenario 1 (vooral verdrietig), na de keuze de Emotion Delta;
2. `PovSetup.AddToScene` (een `PovAnchor` op de patiënt, Liam's Humanoid-rig: de ogen volgen het `Head`-bot), de fade op de
   camera en de `POV Controller (Room 1A)`; de emotie-HUD (`EmotionHudSetup`) en de `Patient World (Room 1A)`
   (`EmotionWorldSetup`);
3. de lichten die de patient view kleurt: de zon, het plafondlicht van 1A en elk punt- of spotlicht op verdieping 1 waarvan
   het bereik tot in de kamer komt (zonder schaduwen schijnen ze door de muren). Enkel in de patient view, daarna exact terug;
4. de effect-anchors van de kamer (`BuildPatientViewEffects`, berekend uit Liam's meubels en raam, niet uit vaste
   coördinaten): planten op de vensterbank, naast het raam en bij de deur, een zonnestraal door het raam; vuur op de
   vensterbank, op het nachtkastje en aan het voeteneinde van het bed (1A heeft geen gordijn, en de stoel is die van de
   patiënt) met één flikkerend licht; de zaklamp op het hoofd; regen **buiten** het raam (Liam's raam heeft helder glas)
   en druppels van het plafond.

Wanneer kan de speler wisselen? Alle kamers starten hun gesprek bij de start van de shift, dus "het gesprek loopt" is
niet genoeg. Y werkt enkel als het gesprek van 1A **open** is (`ScenarioPresenter.IsPlaying`: na "Finish" niet meer) **en**
de speler binnen `Max Distance To Patient` (3,5 m, `PovSwitchSettings`) van de patiënt staat, op dezelfde verdieping.
Sluit het gesprek terwijl de speler door de ogen van de patiënt kijkt (de shift loopt af), dan keert hij vanzelf terug.
De score verandert niet: de emotie is enkel visuele feedback.

**Sectoren:** de builder gebruikt `Assets/Scenes/liam_test_scene.unity` (verdiepingen boven elkaar, 3,5 m per verdieping,
lift). Liam's U-afdeling met sectoren (`liam_u_afdeling.unity`, op `origin/Liam`, commit `347617d`) zit nog niet in
`jalil` en raakt deze builder nu niet. Bij een overstap moeten de verdiepingshoogte, de lift, de vaste plekken van taak 2
en 3 en de shift-borden mee; de patient view van 1A rekent vanuit de kamer zelf (`Room_1A`, `Furniture/Chair`, `Bed`,
`Nightstand`, `Window/Glass` en `Window/Frame_Bottom`, raam in de rechtermuur) en schuift mee zolang die er zijn.

### Klassen (`Assets/_Project/Scripts/Shift/`)

| Klasse | Wat |
| --- | --- |
| `ShiftPlan` | Asset (Create > ProCommVR > Shift Plan): de lijst **Tasks**. Taak 1 gaat naar de eerste kamer van de ShiftManager, taak 2 naar de tweede, ... |
| `ShiftTask` | Eén patiënt: `Patient Id` (uniek in het plan, bijvoorbeeld `patient-01`), `Scenario`, `Patient Name` (leeg = de NPC-naam van het scenario, enkel voor weergave) en `Patient Mood` |
| `ShiftManager` | De gameloop: wijst de taken toe, `StartShift()`, timer (`Shift Duration` in seconden, nu 600 als testwaarde), einde bij alle taken afgelopen of tijd op, toont het Shift report. Houdt de **uitkomst per patiënt-id** bij: `GetOutcome(patientId)`, `IsCompleted(patientId)` (verzorgd), `IsFailed(patientId)`, `CompletedCount`, `FailedCount`, `FinishedCount`, `FindRoom(patientId)`, `SetPatientMood(patientId, mood)`. Events: `ShiftStarted`, `TaskCompleted`, `TaskFailed`, `ShiftEnded` |
| `TaskOutcome` | Hoe een taak afliep: `None` (nog open), `Completed` (beantwoord en "Finish"; de score zegt of het goed of slecht was), `FailedTimeUp` (de countdown van de taak liep af voor er een antwoord was), `FailedAnswer` (beantwoord met een optie met `Fails Task`, en "Finish") |
| `PatientRoom` | Eén plek met een taak, meestal een kamer, voor taak 2 de gang: `Room Name`, eigen `ScenarioPresenter`, `MoodController` (het humeur van de patiënt) en `RoomSign`, optioneel een `TaskCountdown` en de `Failed Mood` (standaard `Alarm`). `PatientId` = de id van de toegewezen taak. Zet bij toewijzing de mood en het bordje, speelt het scenario bij de start (en zet de countdown klaar als het scenario een tijdslimiet heeft), bewaart `ChosenLocation`, `ChosenOption`, `Score` en `Outcome`, en meldt `TaskFinished` na "Finish" of als de tijd op is |
| `TaskCountdown` | De countdown van één taak, naast de shift-timer: `Arm(seconds, label)`, start als de speler binnen `Start Range` (4 m) van de NPC komt, toont de tijd op een display, speelt het tikken, stopt met `Stop()`. Op 0: `Arrival` speelt (de brancard) en het event `Expired`. `State`: `Idle`, `Armed`, `Running`, `Stopped`, `Expired` |
| `RoomSign` | Het deurbordje: kamer, patiënt, scenariotitel en een kleurvlak met de mood-kleur (leest de `MoodController`); toont na de taak "Cared for" of in het rood "Time ran out" |
| `ShiftBoard` | Wereld-UI-paneel met de patiënten van de shift (Waiting / Cared for / Failed: time ran out), de resterende tijd en de teller ("Cared for: 1 / 5", met "Failed: 1" ernaast als er een taak mislukte). Er staan er vier: in de lobby (achter de receptiebalie) en in de gang van verdieping 1, 2 en 3 |
| `ShiftStartButton` | Op de computer: laser + trigger start de shift (XRSimpleInteractable). Het label erboven kleurt blauw als de laser erop staat |
| `PathMover` (`Scripts/Navigation/`) | Beweegt zijn object langs waypoints (in de looprichting gedraaid, eindigt in de rotatie van het laatste punt). Optioneel verborgen tot `Play()`. Gebruikt voor de brancard en voor de dochter die naar de spreekkamer loopt. Event `Arrived` |
| `LiftButton` (`Scripts/Navigation/`) | Op een knop van Liam's liftpaneel: laser + trigger teleporteert de speler (via de `Teleportation Provider` van de rig) naar het aankomstpunt voor de lift op die verdieping, met de blik de gang in. "L" is de lobby. De liftdeuren blijven dicht: de rit is een teleport |

**Patiënt-id:** de weergavenaam mag overal hetzelfde zijn (nu 4 keer "Patient X" met Scenario 1); intern
onderscheidt het systeem de patiënten enkel op `Patient Id`. Een taak zonder id, met een dubbele id of zonder scenario
geeft een waarschuwing in de Inspector (`ShiftPlan.OnValidate`) en wordt bij de start van de shift overgeslagen. Het
placeholder-plan gebruikt `patient-01` tot `patient-05`. Echte content per patiënt (eigen scenario, mood, later
dossiergegevens) hangt dus aan die id, niet aan de naam of de kamer.

Ook nieuw: `NpcAnimationState` (`Scripts/Scenarios/`) speelt bij de start één state van de Animator van een personage,
bijvoorbeeld `Sitting`, op een willekeurig moment van de animatie (`Random Start`, uit voor een patiënt die op het eerste
frame in bed gelegd werd). `ResultsScreen` kreeg `ShowText(subtitle, body)` zodat de shift zijn eigen rapport kan tonen.

### Scene en layout

`Assets/_Project/Scenes/ShiftHospital.unity` wordt volledig opgebouwd door `Assets/_Project/Editor/ShiftHospitalSceneBuilder.cs`
(menu **ProCommVR > Setup > 8. Build Shift Hospital**). Staat **eerst** in de Build Settings, dus een headset-build start met de shift.

De builder opent **Liam's ziekenhuis** (`Assets/Scenes/liam_test_scene.unity`, gebouwd door Liam's
`ProCommVR > Hospital > Build`) als sjabloon, voegt de shift toe en bewaart het resultaat als `ShiftHospital`. Liam's scene
en scripts worden niet aangepast. Bouwt Liam zijn ziekenhuis opnieuw, draai dan ook deze builder opnieuw. Verandert
Liam de namen of de indeling (`Hospital/Floor1/Room_1A`, `Card_1A`, `Door_1A`, `Furniture/Chair`, `Anchor_Sitting`, `Light`,
`Lift/LiftButton_2`, `Lobby/Furniture/ReceptionDesk`, `Floor3/Light_3`, `Room_3B/Furniture/Bed`), dan meldt de builder
welk object hij niet vindt.

- **Gebouw (Liam):** lobby op de begane grond, 3 verdiepingen van 3,5 m hoog met elk een gang van 18 x 3 m en 3 kamers
  (A, B, C) van 4 x 4 m aan de noordkant. De lift zit in de westmuur. Kamers, meubels (Kenney), deuren (Sketchfab), borden,
  lichten en de zorgverleners als decor komen uit Liam's builder.
- **Briefing (lobby):** de computer (`ShiftStartButton`) staat op de bezoekerskant van de receptiebalie, naar de speler
  gericht. Het shift-bord hangt achter de balie aan de noordmuur (1,4 keer groter, want de speler leest het van ongeveer
  3 m). De speler start 1,3 m voor de balie.
- **Taken:** `TaskLocations` in de builder: taak 1 naar 1A, taak 2 naar de gang van verdieping 3, taak 3 naar de verpleegpost
  van verdieping 2, taak 4 naar 1B, taak 5 naar 1C. Kamer 2B is (sinds Scenario 2) en kamer 2A is (sinds Scenario 3) weer een
  kamer van Liam, met zijn kaart en zijn patiënt.
- **Shift-kamers 1A, 1B, 1C, 2A:** Liam's kamer krijgt de `PatientRoom`-component. Liam's
  patiëntkaart en zijn patiënt voor die deur maken plaats voor ons deurbordje (`RoomSign`, op de plek van de kaart, rechts
  van de deur) en onze patiënt. De deur zwaait open, de kamer naar binnen. Liam's plafondlicht van de kamer krijgt de
  `MoodRoomLight` (geen extra licht), het mood-geluid (`MoodAudio`, 3D, tot 4 m) hangt midden in de kamer.
- **Kamer 1B (taak 4):** de slapende patiënt ligt op het matras: de builder legt hem op het eerste frame van zijn
  `Laying`-animatie (lengte van het bed, hoofd aan het hoofdeinde, laagste punt 1 cm boven het matras), gemeten op een
  tijdelijke kopie zoals Liam's builder dat doet. Zijn `NpcAnimationState` start dus op frame 0. De monitor is een eigen
  world-space canvas op het nachtkastje. De vrouw heeft een `NpcAttention` (opstaan) en een `NpcReactor` met een pose
  `Engaged` op de stoel (weer gaan zitten); haar ballon draait naar de speler (`FacePlayer`).
- **Kamer 1C (taak 5):** Liam's bed schuift naar room-local x = +1, een kopie staat op x = -1, de buurman
  (`Patient_Male_Old`) ligt erin zoals de patiënt van 1B (eerste frame van `Laying`, op een eigen anker `Anchor_Laying
  (next bed)`). De krant is twee dunne vlakken (lezen en neergelegd, `Materials/Newspaper.mat`) die `NpcAttention` wisselt.
  Het gordijn (`Materials/PrivacyCurtain.mat`, zonder collider) hangt aan een rail op x = 0, van de achtermuur tot 1 m van
  de deur. Patient X (`Patient_Male`) staat aan het voeteneinde van zijn bed en heeft een collider.
- **Liam's patiënten op het nulpunt:** in Liam's opgeslagen scene staan zijn patiënten van 1A, 2A en 3A niet in hun kamer
  maar op (0, 0, 0), in de lobbymuur naast de lift (zijn builder zet ze enkel juist voor zijn eigen screenshots). De
  builder verbergt in `ShiftHospital` wie niet in zijn kamer staat (nu 2A en 3A) en logt een waarschuwing. Liam's scene
  zelf blijft ongewijzigd; staat hij ze later goed, dan verschijnen ze vanzelf na een rebuild.
- **Patiënten:** de personages van Liam (`Patient_Male`, `Patient_Female_Old`, `Patient_Male_Old`, `Patient_Male`),
  zittend (Animator-state `Sitting`) op Liam's stoel naast het bed. Liam's stoel heeft al de zithoogte
  van de animatie (0,45 m). De builder draait de stoel naar de deur, zoals Liam's eigen patiënten zitten. De `NpcReactor`
  draait de patiënt bij een keuze naar de verpleegkundige (de deur, `Engaged`), naar het bed (`TurnsAway`) of naar het raam
  (`Withdrawn`). Zie de beslissing van 2026-10-20 over de stoel in plaats van de bedrand.
- **Gang van verdieping 3 (taak 2):** `Shift/Encounter (Corridor 3)` met de dochter (`Patient_Female` met andere
  kleren: Liam's patiënten dragen pyjama's, dus kopieën van haar kledingmaterialen in blauw en donkergrijs onder
  `Materials/Characters/`; Liam's materialen blijven ongewijzigd), animatie `Angry`. Haar root draagt de NPC-ballon, de
  pose-doelen, het mood-geluid (3D, tot 6 m) en de `ConversationLocationMover`. Het bordje (`RoomSign`) hangt aan de
  zuidmuur bij de lift. De mood van de taak kleurt de ganglichten `Light_3` en `Light_9` en het licht van kamer 3B.
  Liam's 3 zorgverleners op verdieping 3 maken de gang druk. Countdown-display op 2,6 m hoogte tussen de lift en de dochter.
- **Verpleegpost (verdieping 2, taak 3):** `Shift/Encounter (Station 2)`: het Kenney-bureau met computer, toetsenbord en
  dossiers (dunne witte vlakken, `Materials/Paper.mat`) tegen de zuidmuur op x = 3,4, het bordje "Nurses' station" erboven
  en ons deurbordje (`RoomSign`) links ervan. De zoon (`Patient_Male` in bezoekerskleren, bruin en donkergroen) staat
  ervoor. De arts (`Doctor_Male`) heeft zijn ballon, pose-doelen en een `FacePlayer` op zijn root, die `BusyNpc` laat lopen.
  De mood kleurt het ganglicht `Light_3` (bij de post) en het licht van 2C; het mood-geluid (3D, tot 6 m) hangt bij de post.
- **Bureau van de arts (2C):** Liam's bed, nachtkastje en stoel staan uit, een Kenney-bureau met computer en papierwerk
  staat tegen de linkermuur, een bureaustoel ernaast, de deur staat open en zijn kaart is vervangen door "Doctor's office".
- **Spreekkamer (3B):** Liam's bed, nachtkastje en stoel staan uit, een ronde Kenney-tafel met twee stoelen staat in
  hun plaats, de deur staat open en zijn kaart is vervangen door een bordje "Consultation room".
- **Brancard:** het ziekenhuisbed van Sketchfab (ansh_artis, CC-BY 4.0, al in `CREDITS.md`), verborgen tot de countdown
  afloopt; rolt dan aan 1 m/s van x = 17 naar x = 11,3 aan de noordkant van de gang (Liam's personeel staat aan de zuidkant).
- **Andere kamers** (2A, 2B, 3A, 3C): blijven zoals Liam ze bouwde (zijn kaart, "Coming soon" of zijn eigen case 3A).
  Hun deuren blijven dicht en krijgen een box-collider, zodat niemand erdoor loopt.
- **Lift:** de knoppen van Liam's liftpanelen (lobby 1, 2, 3; verdiepingen L, 1, 2, 3) krijgen een `XRSimpleInteractable`
  en een `LiftButton`. Aankomstpunten: `Shift/Lift Arrivals/Arrival L` tot `Arrival 3`, 1,2 m voor de liftdeur en 3 cm
  boven de vloer: Liam's vloer heeft een afwerklaag van 1 cm bovenop de plaat, en zo start de CharacterController van de
  rig nooit in een vloer-collider. Ook het startpunt in de lobby staat 3 cm hoog.
- **Licht:** Liam's lichten (één directional light, een point light per gang-segment, per kamer en twee in de lobby, geen
  schaduwen). Daarbovenop een eigen `Ward Mood` (MoodController op `Neutral` + MoodAtmosphere) voor het hoofdlicht en het
  ambient licht, zonder geluid. Liam's `Main Camera` wordt verwijderd: de XR rig heeft zijn eigen camera.
- **Colliders:** Liam's muren en vloeren zijn primitives met box-colliders, zijn meubels hebben mesh-colliders.

**Performance (Quest 3), nog te controleren op de headset:** de builder logt het totale aantal driehoeken: 72.237 voor
de hele scene (richtwaarde ongeveer 100k). Daarvan is ongeveer 28k de brancard (Sketchfab-bed, ook als hij verborgen is
telt hij mee); is dat te zwaar, dan kan Kenney's `bedSingle` (Liam's kamerbed) in zijn plaats. Realtime lichten: 1 directional en 20 point lights (9 kamers, 9 gang-segmenten,
2 lobby), geen schaduwen. Geluid: 5 loopende 3D-bronnen voor de moods (tijdens een crossfade even 2 per taak), mono
22 kHz, buiten 4 m geruisloos (de dochter: 6 m), plus het tikken van de countdown zolang die loopt (tot 8 m).

### Uitbreiden

- **Andere patiënten of scenario's:** open `Assets/_Project/Dialogue/Shifts/Shift_Placeholder.asset` en vul per taak
  een ander **Scenario**, **Patient Name** en **Patient Mood** in. Geen code nodig.
- **Een andere mood per patiënt:** kies de mood in de taak. Bordje, kamerlicht en geluid volgen vanzelf (sectie 10).
  Tijdens het spel: `ShiftManager.SetPatientMood("patient-03", Mood.Somber)`.
- **Een nieuwe patiënt in het plan:** geef hem een nieuwe, unieke **Patient Id**.
- **Duur van de shift:** `Shift Duration` op de **Shift Manager** in de scene.
- **Een tijdslimiet of locatiekeuze voor een ander scenario:** vul de velden in het scenario in (sectie 8). In de scene
  heeft de taak dan een `TaskCountdown` nodig (gekoppeld op de `PatientRoom`, met de NPC, een display en eventueel een
  `PathMover` als `Arrival`) en een `ConversationLocationMover` met een `PathMover` per plek. In de shift-scene bouwt
  `BuildCorridorEncounter` dat voor taak 2; voor een andere taak moet de builder het ook doen.
- **De plek laten meetellen:** zet een `Score` (en eventueel `Feedback`) op een `Location` in het scenario. Geen code nodig.
- **Meer kamers:** voeg een deurlabel van Liam's ziekenhuis (bijvoorbeeld `2C`) toe aan `RoomLabels` in de builder en
  een taak aan het shift-plan, en bouw de scene opnieuw. Op een nieuwe verdieping komt er ook een shift-bord in de gang.
- **Een andere omgeving:** de shift-scripts hangen niet aan deze layout. Per kamer heb je een `PatientRoom` nodig met
  een presenter, een MoodController (met MoodRoomLight en MoodAudio) en een RoomSign, plus een Shift Manager, een ShiftBoard,
  een object met `ShiftStartButton` en een ResultsScreen. Enkel de builder kent Liam's indeling.

## 13. Automatische tests in Play mode

`Assets/_Project/Editor/Testing/PlayModeSmokeTest*.cs` speelt het spel in Play mode en sluit de editor af met code 0
(geslaagd) of 1 (gefaald). Elke error of exception in de Console tijdens het spelen laat de test falen.

| Test | Wat |
| --- | --- |
| `RunShiftLoop` | Een volledige shift in `ShiftHospital` (Liam's ziekenhuis): taak 2 tot 5 zijn Scenario 2 tot 5, unieke patiënt-id's (ook met 4 keer dezelfde naam), de mood van elke taak (Happy, de gang Tense) (data, kamerlicht en bordje groen, Happy-geluid 3D), elke patiënt **zichtbaar zittend** (Animator in `Sitting`, heupen tussen 0,3 en 0,75 m, hoofd onder 1,45 m, heupen boven de zitting van de stoel, stoel naast het bed), mood van één patiënt wijzigen via zijn id (enkel die kamer verandert, geluid kruisfadet), start via de computer in de lobby, met de **lift** naar verdieping 1 en 2 (laser op de liftknop, de speler komt op de juiste hoogte aan en staat op de vloer, niet erin), Scenario 2 via de spreekkamer met het beste antwoord, Scenario 3, 4 en 5 met het beste antwoord (zoals de `Good`-tests; in Scenario 5 met het gordijn), per kamer: geen opties vanaf de deur, 4 opties binnen 1,5 m (en geen opties van de patiënt recht erboven of eronder), keuze, "Finish", voortgang per id, dan het eindscherm met elke kamer, de vier borden op "Shift ended" en 5 / 5, met de lift terug naar de lobby, waar de computer niets meer doet |
| `RunShiftTimeout` | Zelfde scene, de shift duurt 4 s: na 1 patiënt loopt de tijd af, het spel stopt met 1 / 5 en de andere kamers stoppen hun gesprek |
| `RunScenario02Good` | Scenario 2, **op tijd en goed**: de countdown wacht in de lobby en start bij het uitstappen op verdieping 3, display en tikken aan, de 2 plekken binnen 1,5 m, keuze spreekkamer, de dochter loopt naar binnen (geen opties onderweg), 4 opties daar, beste optie: countdown stopt, animatie `Talking`, "Finish", uitkomst `Completed`, score 3, bordje en bord "Cared for", geen brancard, mood blijft Tense; eindscherm "3 / 3" en "Talked in: Consultation room" |
| `RunScenario02Poor` | Scenario 2, **op tijd maar slecht**: in de gang blijven, slechtste optie (-1): countdown stopt, animatie `Angry`, `Completed` met score -1, eindscherm "-1 / 3" en "Talked in: Corridor" |
| `RunScenario02TimeUp` | Scenario 2, **tijd op**: in de gang blijven en niet antwoorden (de countdown wordt op 2 s gezet): uitkomst `FailedTimeUp`, 1 mislukt en 0 verzorgd, de shift loopt verder, opties weg, de dochter roept, de brancard rolt de gang in en komt aan, mood `Alarm` (lichten rood en knipperend, bordje rood met "Time ran out", waarschuwingsgeluid), bord "Failed: time ran out"; eindscherm "failed: time ran out" met de feedback en "Failed: 1" |
| `RunScenario03Good` | Scenario 3, **goed**: met de lift naar verdieping 2, de zoon draait zich naar de speler, is boos en zegt de zin uit de bron; de arts rent rond (`Rushing`, opties tegengehouden), stopt als de speler in zijn weg staat, zegt "I don't have time right now." en gebaart, geen opties binnen 1,5 m; na zijn ronde (in de test verkort) staat hij aan zijn bureau in 2C (`AtDesk`, bereikbaar), geen opties vanaf de deur, 4 opties binnen 1,5 m, hij blijft (`Engaged`) en draait naar de speler; beste optie: `Talking`, zijn reactiezin, de zoon weet nog van niets, mood `Happy` (lichten en bordje groen), "Finish", `Completed` met score 3; terug bij de zoon: zijn reactie en `Talking`, bordje en bord "Cared for"; eindscherm "3 / 3" met de feedback |
| `RunScenario03Poor` | Scenario 3, **slecht**: zelfde verloop met de slechtste optie (-2): de arts draait zich weg (`Idle`), mood `Tense`, de zoon reageert boos (`Angry`); eindscherm "-2 / 3" |
| `RunScenario04Good` | Scenario 4, **goed**: met de lift naar verdieping 1, vanuit de deur van 1B: geen opties, de vrouw zit (`Sitting`), de patiënt ligt op het bed (`Laying`, heupen boven het matras), de vitale functies schommelen, het kamerlicht is paars en gedimd; binnen bereik staat ze op, reikt naar de speler (`Talking`) en vraagt, 4 opties; beste optie: ze gaat weer zitten, mood `Somber`, licht blauw en niet meer gedimd; "Finish", `Completed`, score 3; eindscherm "3 / 3" |
| `RunScenario04Poor` | Scenario 4, **valse hoop** (-2): ze blijft staan (`Talking`), mood blijft `Distressed`; eindscherm "-2 / 3" |
| `RunScenario05Good` | Scenario 5, **gordijn en goed**: vanuit de gang niets (krant omhoog, gordijn open); in de deur komt Patient X roepend af (`Angry`, de zin uit de bron) en legt de buurman zijn krant neer, 2 keuzes; gordijn kiezen: geen opties zolang het schuift, daarna dicht; 4 opties; beste optie: Patient X rustig (`Idle`), de buurman blijft liggen en zwijgt, mood `Neutral`; "Finish", `Completed`, score 3 + 1; eindscherm "4 / 4" en "Talked in: Behind the privacy curtain" |
| `RunScenario05Fail` | Scenario 5, **open kamer en de diagnose hardop**: Patient X loopt naar de deur (`Angry`), de buurman staat overstuur op (`Sad`) met zijn zin, mood `Alarm` (rood knipperlicht en waarschuwingsgeluid); "Finish": uitkomst `FailedAnswer`, geen score, bordje "Not resolved", bord "Failed: not resolved" en "Failed: 1", de shift loopt verder; eindscherm "failed: not resolved" met de feedback |
| `RunScenario01Mood` | `Scenario01_PatientRoom`: na de fade hebben het hoofdlicht en het ambient licht de waarden van het Somber-profiel, en het Somber-geluid speelt |
| `RunEmotionModel` | `Scenario01_PatientRoom` met een kopie van Scenario 1 met testwaarden (het echte asset blijft ongewijzigd): `ScenarioStarted` zet Initial Emotion meteen (één `EmotionChanged`, dominant Sadness), 4 opties binnen bereik, laser + trigger kiest een optie, het target is initial + delta (geclampt), Current springt niet maar vloeit in ongeveer 1,5 s naar het target en geeft onderweg `EmotionChanged`; daarna met de hand: `Apply` clampt op 1 en op 0, alles 0 geeft geen dominante emotie, `SetImmediate` springt meteen; de Mood blijft Somber |
| `RunPlayerAvatar` | `Scenario01_PatientRoom`: precies één avatar (uit de rig), los van de rig, standaard onzichtbaar, hoogstens 3k driehoeken, geen lichten of schaduwen; `SetVisible(true)` toont hem en geeft het event; met de gesimuleerde HMD staat het lichaam onder het hoofd, rechtop, voeten op de vloer, gedraaid met de yaw van het hoofd, ook na lopen en draaien, en het eigen hoofd is weggeklapt; met `FollowHead` uit blijft hij staan en is zijn hoofd van op 2 m zichtbaar (screenshot `avatar-from-outside`); `SetVisible(false)` verbergt hem weer |
| `RunPovSwitch` | `Scenario01_PatientRoom`, met de **Y-knop van een gesimuleerde linkercontroller** (via de echte action `SwitchPov`): binnen bereik 4 opties; Y: de camera staat op de ogen van de patiënt (1,25 m, zijn kijkrichting), de avatar staat waar de speler stond, het lichaam van de patiënt en alle ballonnen zijn weg, lasers, locomotion en de CharacterController staan uit, de hint "Press Y to return" staat er; een snelle tweede druk wordt genegeerd; Y na de wachttijd: de rig staat **exact** op de oude pose, alles is terug, dezelfde 4 opties in dezelfde volgorde, geen keuze gemaakt; drie keer, ook een vierde wissel mag (geen limiet); een nieuw gesprek zet de teller op 0; `PovChanged` 6 keer, afwisselend. Screenshots: nurse view, patient view, de verpleegkundige door de ogen van de patiënt |
| `RunEmotionHud` | `Scenario01_PatientRoom`: precies één HUD, uit in de nurse view (ook als de emoties veranderen), alle 19 graphics tekenen bovenop (overlay-shaders); na Y (gesimuleerde linkercontroller): getoond (alpha 1) met de hint "Press Y to return", 1,60 m ver, 22° links van het midden, naar de camera gedraaid; de 4 getallen en balken komen overeen met de `EmotionHolder`, de dominante rij (Anger) is vet en fel, de andere gedimd; hoofdletters van de rijen 27 mm = 0,98°, kleinste tekst 0,68°; na `Apply` beweegt de balk mee tijdens het overvloeien en wordt Sadness dominant; bij een snelle draai van 60° naar links blijft de HUD minstens 12° links van het midden en komt hij terug op 22°; Y terug: uit. Screenshots: patient view met HUD |
| `RunScenario01PatientPov` | `Scenario01_PatientRoom` met de echte Scenario 1-data: Initial Emotion (vooral verdrietig), de scores ongewijzigd, en naar welke emotie elke optie leidt (beste: Joy, afwijzend: Anger, de andere twee: Sadness); de patiënt start op de Initial Emotion; beste optie met laser + trigger: emoties = initial + delta, Sadness daalt van 0,70 naar 0,35, Joy dominant, in de nurse view verandert de kamer niet; Y (gesimuleerde linkercontroller): de HUD toont Joy als dominant en de 4 getallen van de holder, de kamer toont Joy op volle sterkte met precies de Joy-effecten; Y terug: de kamer is exact zoals ervoor (alle getinte lichten, ambient, fog, post-processing), geen effecten, geen HUD, het gesprek staat nog open, de score is 3; daarna opnieuw met de afwijzende optie: Anger, het vuur brandt. Screenshots: patient view en een effect, per antwoord |
| `RunScenario01PatientPovShift` | `ShiftHospital`, taak 1 in kamer 1A: één POV controller; vanuit de lobby (gesprek al open) weigert Y ("too far from the patient"); met de lift naar 1A, beste optie, dezelfde checks als hierboven (HUD, Joy in de kamer, de zon en de lichten die tot in 1A schijnen exact terug); "Finish" sluit het gesprek en Y doet niets meer ("no conversation"); eindscherm "3 / 3" |
| `RunPrototype1` | `Prototype1` met de echte AI-server (zie sectie 20): briefing zonder gesprek (Y geweigerd); bij de patiënt start het AI-gesprek, vertrouwen en emotie volgen de openingsbeurt (laag vertrouwen, Sadness); Y: patient view met de vertrouwensmeter, regen binnen en plassen op de vloer; Joy: de planten staan op de vloer; Fear: donker; afronden: naar de dokter, de nabespreking opent; Opnieuw proberen: terug bij de patiënt, nieuw gesprek, 0 switches; Klaar: einde. Ook: handenvrij praten (een opgenomen zin tussen stiltes wordt vanzelf verstuurd en de AI hoort ze), het klembord start in de linkerhand, de oranje voortgangsbalk (zichtbaar, oranje, vol aan het einde), de plattegrond met oranje punten, het achtergrondgeluid (5 bronnen), Y werkt na meer dan 3 switches, de trust-aura per niveau en het dev-klembord (emotie, vertrouwen, *AI drives*, pagina's, Take en Put down). De spraak van de student zit niet in deze test |
| `RunPatientViewTest` | `PatientViewTest`: het testpaneel toont de status (emotie, view, of Y werkt); de knop Joy (laser + trigger) maakt Joy dominant; Y toont het in de patient view (HUD en kamer); de testbediening zet de patiënt daar op Anger (de kamer volgt) en op Neutral (alles 0); Y terug; de knop Restart herstart het gesprek (Initial Emotion, 0 switches) |
| `RunEmotionWorld` | `Scenario01_PatientRoom`: budget (volume met Color Adjustments en Vignette, geen Bloom; per emotie een profiel, effect-anchors en hoogstens 1 extra licht; deeltjessystemen hoogstens 200 deeltjes; geen schaduwen); **nurse view**: elke emotie dominant maken verandert niets (licht, ambient, fog, post-processing, volume, anchors); na Y (gesimuleerde linkercontroller), **patient view**, per emotie (Joy, Anger, Fear, Sadness via `SetImmediate`): volle sterkte, volume en post-processing aan, saturatie en kleurfilter van het profiel, kamerlicht maal het profiel, precies de eigen effecten aan, deeltjes spelen, eigen geluid speelt; wissel Sadness naar Joy: via neutraal, nooit twee emoties of twee extra lichten tegelijk, geen sprongen; een mood-verandering van het kamerlicht tijdens de patient view blijft behouden; Y terug: de kamer is exact zoals ervoor. Screenshots per emotie (naar het raam en de kamer) |

De shift-tests gebruiken **gesimuleerde XR-devices** (`XRSimulatedHMD` en `XRSimulatedController`, dezelfde devices
als de XR Interaction Simulator): de "headset" kijkt rond, de rechtercontroller richt de laser op de computer en de
ballonnen en drukt de trigger in. Zo gaat elke klik door de echte rig (Near-Far Interactor, XR UI Input Module). Enkel
het lopen wordt overgeslagen: de test verplaatst de XR Origin naar het liftpaneel, de deur en de patiënt. De liftrit
zelf gaat door de echte `LiftButton` en de `Teleportation Provider` van de rig; verplaatst het spel de XR Origin (de lift),
dan beweegt het gesimuleerde hoofd mee, zoals een echte headset. Bij een klik blijft de trigger minstens 0,2 s en 3 frames
ingedrukt (in batchmode kan één frame langer duren dan 0,2 s); had een klik geen effect, dan drukt de test nog één keer
en logt dat. Mislukt hij dan nog, dan logt de test welke colliders tussen de controller en het doel zitten en wat de
laser raakt.

**Starten:** via het menu **ProCommVR > Tests** in de editor, of in batchmode (zonder `-quit`):

```bash
Unity.exe -batchmode -projectPath <project> -logFile <log> \
  -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunShiftLoop
```

Optioneel `-smokeTestScreenshots <map>`: bewaart onderweg beelden van de camera van de speler (zonder `-nographics`):
de lobby, het shift-bord, elke aankomst met de lift, de deur van elke kamer, de patiënt van zij, de opties, de reactie en
het eindrapport; bij Scenario 2 de gang met de countdown, de locatiekeuze, de opties op de gekozen plek, de reactie,
het alarm met de brancard en het eindrapport; bij Scenario 3 de verpleegpost, de drukke arts, de deur van zijn bureau, de
opties, zijn reactie, de reactie van de zoon en het eindrapport; bij Scenario 4 de kamer vanuit de deur, de vrouw die
opstaat met de opties, haar reactie en het eindrapport; bij Scenario 5 de roepende patiënt met de keuzes, de opties, de
reactie, een overzicht van de kamer (bedden, gordijn, buurman) en het eindrapport.
Een test vervangt geen test op de Quest 3 zelf: performance, leesbaarheid in de headset en echte handbewegingen moet je
daar nog controleren.

## 14. Emotion model

**Wat:** wat de patiënt vanbinnen voelt, als vier waarden van 0 tot 1: Joy, Anger, Fear en Sadness. Het is de basis
voor de patient view (POV-switch, emotie-HUD en een kamer die op de emotie reageert: sectie 16 tot 18; gekoppeld aan Scenario 1 en kamer 1A
in de shift: sectie 12 "Taak 1"; snel testen: sectie 19). In de
nurse view verandert er niets: voor de verpleegkundige is het gewoon een woensdag, en dat verschil is net de bedoeling.
Los van het **Mood-systeem** (sectie 10): de mood is de sfeer van een kamer voor iedereen, de emotie is van de patiënt.
Geen van beide leest of verandert de andere.

**Klassen** (`Assets/_Project/Scripts/Emotions/`, namespace `ProCommVR.Emotions`):

| Klasse | Wat |
| --- | --- |
| `Emotion` | enum `Joy`, `Anger`, `Fear`, `Sadness` |
| `EmotionState` | serialiseerbare struct met de vier waarden. `Get(emotion)`, `With(emotion, value)`, `Add(delta)` (geclampt op 0..1), `Lerp(a, b, t)`, `Approximately`, en `Dominant` / `GetDominant(threshold)`: de sterkste emotie, of `null` als geen enkele de drempel haalt (standaard 0,15). Bij een gelijke stand wint de eerste in de enum. |
| `EmotionRangeAttribute` | tekent een `EmotionState`-veld als vier sliders: `[EmotionRange(0, 1)]` voor een toestand, `[EmotionRange(-1, 1)]` voor een delta, met `readOnly` om enkel te tonen. De drawer staat in `Editor/EmotionStateDrawer.cs`. |
| `EmotionHolder` | op de patiënt. **Target** is waar de emoties naartoe gaan, **Current** volgt in `Blend Seconds` (1,5 s, smoothstep), zodat effecten nooit springen. `EmotionChanged(previous, current)` gaat af telkens Current verandert, ook tijdens het overvloeien, zodat lezers niet hoeven te pollen. `Apply(delta)` telt bij het target op, `SetImmediate(state)` zet alles meteen. |

**Data** (geen code nodig):
- `Scenario` > **Initial Emotion**: wat de patiënt voelt bij de start van het gesprek.
- Elke optie > **Emotion Delta** (-1..1 per emotie): wat er na die keuze bij komt of af gaat.
- Alles staat standaard op 0: bestaande scenario's blijven werken zonder emotie. De emotie is enkel visuele feedback,
  de score verandert niet.

**Koppeling:** met een `ScenarioPresenter` in het veld *Presenter* zet de holder bij `ScenarioStarted` meteen de Initial
Emotion, en telt hij bij `OptionChosen` de Emotion Delta op. Dat zijn dezelfde events als bij `MoodController`; hun
signatuur is niet veranderd.

**Scenes:** `Scenario01SceneBuilder` zet een `EmotionHolder` op de root van de patiënt (naast de `NpcReactor`). In
`ShiftHospital` zit hij op de patiënt van kamer 1A (sectie 12, "Taak 1"). De waarden van Scenario 1 (een eerste gok, nog
te laten nakijken door iemand met zorgkennis):

| | Joy | Anger | Fear | Sadness | Dominant daarna |
| --- | --- | --- | --- | --- | --- |
| Initial Emotion | 0,05 | 0,25 | 0,20 | 0,70 | Sadness |
| Beste optie (3): erkent de teleurstelling | +0,55 | -0,10 | 0 | -0,35 | **Joy** (zon, planten) |
| Afwijzend (0): "There will be other birthdays" | -0,05 | +0,60 | 0 | +0,10 | **Anger** (vuur) |
| Valse hoop (-2) | +0,15 | +0,10 | +0,10 | 0 | Sadness (regen) |
| Passief (1): geeft het door | 0 | +0,15 | 0 | +0,15 | Sadness (regen) |

De beste en de afwijzende optie wijken af van het eerste voorstel (Joy +0,2, Sadness -0,25; Anger +0,3): daarmee bleef
Sadness na elke keuze dominant, en zou de speler nooit de zon of het vuur zien. Het korte opflakkeren van Joy bij valse
hoop is niet gebouwd (een delta is blijvend).

**Met de hand testen:** start `Scenario01_PatientRoom` in Play mode, selecteer de patiënt en sleep de **Target**-sliders
van de `EmotionHolder`; **Current** (grijs) volgt. Zo kan elk effect van de latere stappen apart getest worden.

**Uitbreiden:**
- Een lezer (HUD, licht, effecten) abonneert zich op `EmotionChanged` en gebruikt `Current` of `Dominant`.
- Een vijfde emotie: een waarde in de enum, een veld in `EmotionState` (ook in `Get`, `With`, `Add`, `Lerp`,
  `Approximately` en de lus in `GetDominant`), en een slider in de drawer. Bestaande assets krijgen de nieuwe waarde op 0.
- Waarden per scenario aanpassen: enkel in het `Scenario`-asset, in de inspector.

## 15. Player avatar

**Wat:** het lichaam van de speler, een verpleegkundige, enkel bedoeld om van buitenaf gezien te worden (in de patient
view, sectie 14). Vanuit het eigen hoofd zie je je eigen hoofd nooit.

**Model:** Liam's `Nurse_Female_Scrubs` (470 driehoeken, Humanoid-rig, Animator in Idle). Wij gebruiken een **prefab
variant**, `Assets/_Project/Prefabs/Player/PlayerAvatar_Nurse.prefab`, met de component `PlayerAvatar`, zonder schaduwen
en zonder root motion. Liam's prefab blijft ongewijzigd.

**In de rig:** de avatar zit als kind `Player Avatar (Nurse)` in `XR Origin (ProCommVR).prefab`. Elke scene met de rig
heeft hem dus één keer, zonder iets per scene. Bij de start van de scene **koppelt hij zich los** van de rig (naar de
root van de scene), zodat hij kan blijven staan als de rig verhuist (de POV-switch).

**`PlayerAvatar`** (`Assets/_Project/Scripts/Player/`, namespace `ProCommVR.Player`):
- `SetVisible(bool)`, `IsVisible` en het event `VisibilityChanged(bool)`. Standaard onzichtbaar (nurse view). Onzichtbaar
  zet de renderers en de Animator uit, dus het kost dan niets. Het vinkje *Visible* in de inspector werkt in Play mode
  hetzelfde.
- `FollowHead` (standaard aan): elke frame (`LateUpdate`) staat het lichaam onder de XR-camera, 0,1 m achter de ogen,
  enkel gedraaid met de yaw van het hoofd, rechtop, met de voeten op de vloer van de XR Origin. Uit: de avatar blijft
  staan waar hij is.
- **Eigen hoofd verborgen:** het personage is één skinned mesh (hoofd, haar en lichaam in één renderer), dus een aparte
  layer voor het hoofd kan niet. Daarom wordt het `Head`-bot naar schaal 0 gezet zolang de camera binnen *Hide Head
  Distance* (0,6 m) van het hoofd is: hoofd en haar klappen weg. Verder weg is het hoofd gewoon zichtbaar. Dit geldt
  voor alle camera's tegelijk; op de Quest is er maar één.

**Opnieuw opbouwen:** **ProCommVR > Setup > 8. Add Player Avatar to Rig** (of in batchmode
`-executeMethod ProCommVR.Editor.PlayerAvatarSetup.Run`) maakt de variant opnieuw en vervangt de avatar in de rig.
Meermaals draaien geeft geen dubbele avatar. Ook **2. Build XR Test Scene**, die de rig variant opnieuw maakt, zet de
avatar er weer in.

**Met de hand testen:** start een scene in Play mode, selecteer `Player Avatar (Nurse)` en vink *Visible* aan; loop rond
en kijk naar beneden: je ziet je lichaam, maar nooit je eigen hoofd. Vink *Follow Head* uit en loop weg: de
verpleegkundige blijft staan.

**Beperkingen:** vaste lengte van het model (niet geschaald naar de speler), geen armen die de controllers volgen,
geen loopanimatie.

**Uitbreiden:**
- Een ander model: verander `SourceCharacterPath` in `Editor/PlayerAvatarSetup.cs` (bv. `Nurse_Male_Scrubs`) en draai de
  setup opnieuw. Het model moet een Humanoid-rig hebben (voor het `Head`-bot).
- Armen of handen die de controllers volgen: later met IK (Animator IK of Animation Rigging), in een aparte stap.

## 16. POV switch

**Wat:** tijdens een gesprek wisselt de speler met **Y** (linkercontroller) tussen de nurse view en de patient view:
de camera springt naar de ogen van de patiënt, en de speler ziet de verpleegkundige (zijn eigen avatar, sectie 15) staan
waar hij stond. Nog zonder emotie-effecten (HUD en kamer komen in latere stappen).

**Input:** de action `SwitchPov` in de map *XRI Left Interaction* van onze kopie van de input actions, gebonden aan
`<XRController>{LeftHand}/{SecondaryButton}` (Y op de Quest-controller). De package-actions blijven ongewijzigd, trigger =
Select en grip = Activate ook. Toegevoegd door `Editor/PovSetup.cs` (**ProCommVR > Setup > 9. Add Switch POV Input
Action**, ook aangeroepen als `XRTestSceneBuilder` de input actions opnieuw maakt; meermaals draaien voegt hem één keer toe).

**Klassen** (`Assets/_Project/Scripts/Pov/`, namespace `ProCommVR.Pov`):

| Klasse | Wat |
| --- | --- |
| `PovSwitchSettings` | ScriptableObject (`Settings/Pov/PovSwitchSettings.asset`): `MaxSwitchesPerConversation` (0 = geen limiet, sinds 2026-11-22), `MinSecondsBetweenSwitches` (2), `FadeSeconds` (0,25), `MaxDistanceToPatient` (3,5 m). Tunen in het asset. |
| `PovController` | `PovMode { Nurse, Patient }`, `Mode`, event `PovChanged(PovMode)` (na de verplaatsing, terwijl het beeld nog zwart is), `TrySwitch()` en `WhyNot()` (waarom een wissel nu niet kan). Enkel de wissel **naar** de patiënt telt mee; terugkeren mag altijd. De wachttijd geldt in beide richtingen, dus een snelle dubbele druk wordt genegeerd. `ScenarioStarted` zet de teller op 0. Naar de patiënt kan enkel als het gesprek open is (`ScenarioPresenter.IsPlaying`, dus niet meer na "Finish" of het eindscherm) en de speler binnen `MaxDistanceToPatient` van de patiënt staat, op dezelfde verdieping (`ScenarioPresenter.IsPlayerNear`). Sluit het gesprek in de patient view, dan gaat hij vanzelf terug. |
| `PovAnchor` | op de NPC: `GetEyes(out position, out forward)`. Met een Humanoid-rig: het `Head`-bot (plus een kleine offset) en de kijkrichting van het lichaam, op het moment van de wissel (zittend, liggend of staand). Zonder rig (de placeholder in Scenario 1): het veld *Eyes*, een kind van het lichaam, dus het volgt de houding (`NpcReactor`). `SetBodyHidden(bool)` verbergt de renderers van het lichaam, en zet enkel terug aan wat aan stond. |
| `ScreenFade` | zwart vlak op 0,15 m voor de camera (eigen URP Unlit-materiaal `Materials/Pov/ScreenFade.mat`, transparant, overlay, geen collider); de renderer staat enkel aan tijdens een fade. |

**Naar de patiënt:** fade naar zwart, de avatar komt onder het hoofd en blijft staan (`FollowHead` uit,
`PlaceAtHead`, `SetVisible(true)`), de pose van de XR Origin wordt bewaard, locomotion providers, interactors (lasers) en
de CharacterController gaan uit, de rig draait en verschuift met `MatchOriginUpCameraForward` en
`MoveCameraToWorldLocation`, het lichaam van de patiënt en alle ballonnen (`ScenarioPresenter.BubblesHidden`) worden
verborgen, de hint "Press Y to return" (een niet-klikbare `DialogueBubble`) verschijnt voor de ogen, fade terug.
**Terug:** alles omgekeerd; de XR Origin komt exact op de bewaarde pose. Het gesprek verandert niet: dezelfde opties in
dezelfde volgorde, geen keuze, geen score.

**`ScenarioPresenter.BubblesHidden`** (nieuw): verbergt alle ballonnen (zin van de NPC, keuzes, "Finish"/"Try again") en
houdt de keuzes tegen; daarna komen ze terug zoals ze waren. Events en hun signaturen zijn niet veranderd.

**Scenes:** `Scenario01SceneBuilder` zet via `PovSetup.AddToScene` het anker op de patiënt (met een punt "POV Eyes" ter
hoogte van het vizier), de fade op de camera van de rig, de hint en de `POV Controller`. In `ShiftHospital` heeft enkel
kamer 1A het (sectie 12, "Taak 1").

**Met de hand testen:** start `Scenario01_PatientRoom`, stap naar de patiënt en druk Y (in de editor ook
`PovController.TrySwitch()` via een debug-knop of de XR Interaction Simulator). Met de patiënt in zijn beginhouding kijk
je uit het raam; draai je hoofd om de verpleegkundige te zien.

**Uitbreiden:**
- Een andere NPC: `PovSetup.AddToScene(presenter, body, origin)`; met een Humanoid-rig is geen *Eyes* nodig.
- Iets dat enkel in de patient view mag gebeuren (HUD, kamer-effecten): luister naar `PovChanged`.
- Andere limieten: enkel in `PovSwitchSettings.asset`.

## 17. Emotion HUD

**Wat:** een klein paneel met de vier emoties van de patiënt, **enkel in de patient view** (sectie 16). Header
"PATIENT VIEW", vier rijen (label, balk, getal 0 tot 100) in de kleuren Joy `#5FD0A4`, Anger `#F2784B`, Fear `#B79CF2`,
Sadness `#6FA8E8`, en onderaan de hint "Press Y to return" (dezelfde tekst als `PovSetup.HintText`). Het losse
hint-pilletje uit stap 3 is weg: de HUD toont de hint.

**`EmotionHud`** (`Assets/_Project/Scripts/Emotions/`):
- **Waarden:** abonneert zich op `EmotionHolder.EmotionChanged`. Dat event gaat elke frame af terwijl de emoties
  overvloeien (sectie 14), dus de balken bewegen vloeiend mee zonder polling. Getallen via `TMP_Text.SetText("{0}", ...)`,
  balken via de breedte van een `RectTransform`: geen allocaties per frame.
- **Dominant:** de rij van `EmotionHolder.Dominant` heeft een volle balk en een vet label; de andere rijen zijn gedimd
  (*Dimmed Alpha* 0,55). Geen dominante emotie (alles onder de drempel): alle rijen gedimd.
- **Tonen:** `PovChanged(Patient)` toont, `PovChanged(Nurse)` verbergt, met een fade van 0,2 s (`CanvasGroup`). Verborgen
  staat de Canvas uit.
- **Zacht volgen** (`LateUpdate`): 1,1 m voor de camera (was 1,6 m, dichter na de playtest van 2026-11-22), 22° naar links, 0,1 m onder ooghoogte, rechtop en naar de camera
  gedraaid; de yaw ijlt na (`SmoothDampAngle`, 0,25 s). **Nooit in het midden:** het paneel blijft altijd tussen 12° en
  40° links van het midden, ook bij een snelle draai. Het volgt de pitch van het hoofd niet (het blijft op ooghoogte).
- **Altijd bovenop:** een paneel op 1,6 m kan achter een muur vallen (de patiënt zit vlak bij de muur). Daarom tekent de
  HUD met ZTest Always: het paneel en de balken met de eigen shader `ProCommVR/UI Overlay` (`Assets/_Project/Shaders/`,
  een kopie van UI/Default), de tekst met TextMeshPro's `Mobile/Distance Field Overlay`. Materialen:
  `Materials/UI/HudOverlay.mat` en `HudOverlay_Text.mat`.

**Leesbaarheid:** hoofdletters van de rijen 27 mm op 1,6 m (ongeveer 1°), header en hint 19 mm (ongeveer 0,7°).
Berekend, en bekeken op screenshots; in de headset zelf nog na te kijken.

**Prefab en scene:** `Editor/EmotionHudSetup.cs` bouwt `Assets/_Project/Prefabs/UI/EmotionHud.prefab` in code (zoals de
ballonnen: world-space Canvas, 1 eenheid = 1 mm, de afgeronde `UISprite`), menu **ProCommVR > Setup > 10. Build Emotion
HUD Prefab**. `Scenario01SceneBuilder` zet hem in de scene, gekoppeld aan de `EmotionHolder` en de `PovController`.

**Met de hand testen:** start `Scenario01_PatientRoom`, druk Y bij de patiënt en sleep de Target-sliders van de
`EmotionHolder`: de balken volgen, de dominante rij licht op.

**Uitbreiden:**
- Kleuren, maten of lettergrootte: in `EmotionHudSetup` en de prefab opnieuw bouwen.
- Plaats en gedrag (afstand, hoek, naijlen, grenzen): in de inspector van `EmotionHud`.
- Een vijfde emotie: een rij in `EmotionHudSetup.Rows` (na de uitbreiding van `EmotionState`, sectie 14).

## 18. Emotion world effects

**Wat:** in de **patient view** (sectie 16) reageert de kamer op de dominante emotie van de patiënt (sectie 14). In de
**nurse view** ziet de kamer er exact uit zoals zonder dit systeem: voor de verpleegkundige is het gewoon woensdag.
Het verschil tussen de twee views is het punt van het spel.

| Emotie | Scherm (volume) | Licht | Effecten |
| --- | --- | --- | --- |
| Joy | iets groener en warmer, meer kleur, iets helderder | warmer en sterker, warm ambient | planten groeien bij het raam (op de vloer, op de vensterbank), warm zonlicht door het raam met stofjes |
| Anger | roodachtig, rode vignette die pulseert | rood-oranje | kleine vuurtjes op de stoel, de vensterbank en het nachtkastje, een flikkerend vuurlicht |
| Fear | koud en grauw, sterke vignette, donkerder | licht en ambient bijna uit, zwarte fog | enkel een zaklamp op het hoofd (smalle kegel, zachte rand) verlicht de kamer |
| Sadness | blauwer en grijzer | grijsblauw en zwakker, grijze fog | regen over het raam, een paar druppels van het plafond |

**Klassen** (`Assets/_Project/Scripts/Emotions/`):

| Klasse | Rol |
| --- | --- |
| `EmotionWorldProfile` | ScriptableObject per emotie (`Settings/Emotions/EmotionWorld_<emotie>.asset`): kleurfilter, saturatie, exposure, vignette (sterkte, kleur, puls), tint en sterkte van het kamerlicht en het ambient licht, fog (aan, kleur, dichtheid), welke soorten effecten aan gaan, geluid (placeholder) en volume. Neutraal is "geen verandering" en heeft geen asset. |
| `PatientWorldController` | Luistert naar `EmotionHolder.EmotionChanged` en `PovController.PovChanged` en stuurt alles hieronder aan. |
| `EmotionEffectAnchor` | Een plek in de kamer met een effect (emotie, soort, inhoud). Gewicht 0 tot 1: bij 0 staat de inhoud uit (kost niets). Optioneel: *Grow* (schaalt van niets tot de gebouwde grootte, met wat wiegen), deeltjessystemen (emissie maal het gewicht), een licht (sterkte maal het gewicht, optioneel flikkeren). |

**Hoe het werkt:**
- **Sterkte:** in de patient view groeit het effect met de waarde van de dominante emotie: van de drempel (0,15) tot
  *Full Strength At* (0,6) gaat het van 0 tot vol. Het overvloeien duurt ongeveer 2 s (*Blend Seconds*, met easing).
- **Andere emotie:** de oude emotie vloeit eerst terug naar neutraal (twee keer zo snel), dan komt de nieuwe op. Zo is er
  nooit meer dan één emotie, en dus nooit meer dan één extra realtime licht, tegelijk actief.
- **Terug naar de nurse view:** `PovChanged(Nurse)` komt terwijl het beeld nog zwart is (sectie 16); de controller zet
  alles meteen terug. Er lekt dus niets in de nurse view.
- **Volume:** één globaal URP-volume ("Patient World (emotion effects)") met enkel Color Adjustments en Vignette
  (`Settings/Emotions/EmotionWorldVolume.asset`), **geen Bloom** (dat laat dingen gloeien, verschuift geen kleuren en is
  duur op de Quest). In Play mode werkt de controller op een kopie van het profiel, het asset blijft ongewijzigd.
  **Post-processing staat enkel aan op de camera zolang er een effect te zien is**: in de nurse view kost het niets.
- **Licht bovenop de mood:** de controller vermenigvuldigt de kleur en sterkte van de kamerlichten (*Room Lights*) en het
  ambient licht met het profiel. Het Mood-systeem (sectie 10) blijft ongewijzigd en mag die lichten blijven aanpassen:
  ziet de controller een andere waarde dan hij zelf schreef, dan is dat de nieuwe basis (apart voor kleur en sterkte).
  Bij het terugzetten krijgt elk licht zijn basis terug, tenzij iemand het intussen veranderde.
- **Fog:** enkel aan zolang een profiel met fog getoond wordt; daarna komen de oorspronkelijke instellingen terug.
- **Geluid:** één 2D-`AudioSource` op de controller speelt de loop van het profiel, met een volume maal het gewicht.

**Budget voor de Quest 3** (gecontroleerd door `RunEmotionWorld`): hoogstens 1 extra realtime licht tegelijk (zon, vuurlicht
of zaklamp; Sadness heeft er geen), geen schaduwen, deeltjessystemen met hoogstens 200 deeltjes (regen 200, vuur 60,
stofjes 40, druppels 10), een eenvoudige unlit deeltjes-shader (`ProCommVR/Particle`, werkt met single-pass instanced
stereo), geen schermvullende transparante lagen buiten het volume. De frame-kost op de Quest zelf is **nog niet gemeten**.
Ogen de kleurtinten op het toestel te zwak, dan zijn de lichten en de fog in het profiel de eerste uitwijk.

**Placeholder-geluiden:** `Assets/_Project/Audio/Placeholder/Emotion_<emotie>_Placeholder.wav`, gegenereerd (Joy: een
zacht akkoord, Anger: geknetter, Fear: een lage drone met hartslag, Sadness: regen). Mono, 22 kHz, loops van 4 s. Echte
audio komt later.

**Scene:** `Editor/EmotionWorldSetup.cs` maakt de profielen (enkel als ze ontbreken: getunede assets blijven behouden;
verwijder er een om de standaardwaarden terug te krijgen), het volume-profiel, de deeltjesmaterialen en de textuur, en
zet de controller in een scene (`AddToScene(holder, pov, roomLights)`). De effect-anchors zet een builder met
`AddPlant`, `AddSunBeam`, `AddFire`, `AddFireLight`, `AddFlashlight` (een kind van de XR-camera), `AddRain` en `AddDrips`,
nooit met de hand. In `Scenario01_PatientRoom` doet `Scenario01SceneBuilder.BuildEmotionEffects` dat rond het raam waar
de patiënt naar kijkt. Het raam van de kit heeft een gesloten rolgordijn in het model zelf; de regen valt daarom net voor
het raam, van boven tot op de vensterbank. Kamer 1A in de shift: `ShiftHospitalSceneBuilder.BuildPatientViewEffects`
(sectie 12, "Taak 1").

**Met de hand testen:** start `Scenario01_PatientRoom`, druk Y bij de patiënt en sleep de Target-sliders van de
`EmotionHolder`: de kamer volgt de dominante emotie; druk Y terug: de kamer is weer gewoon.
Sneller, ook op de Quest 3: de testscene `PatientViewTest` (sectie 19).

**Uitbreiden:**
- Kleuren, licht, fog of geluid van een emotie: in het `EmotionWorld_<emotie>.asset`, zonder code.
- Een nieuw effect in een kamer: een `Add*`-aanroep in de builder van die kamer; een nieuwe soort effect: een waarde in
  `EmotionEffectKind`, een `Add*`-methode in `EmotionWorldSetup` en de soort in de lijst *Effects* van het profiel.
- Een andere kamer: `EmotionWorldSetup.AddToScene` met de lichten van die kamer, en de anchors via de builder.

## 19. Testscene voor de patient view (`PatientViewTest`)

Om alle Patient POV-features snel na elkaar te testen, in de editor, via Quest Link of op de Quest 3 zelf, zonder telkens
het gesprek opnieuw te spelen. Het is de kamer van `Scenario01_PatientRoom` (zelfde builder, zelfde Scenario 1), met een
testpaneel op de linkermuur naast de startplek.

| Wat | Hoe |
| --- | --- |
| Emotie van de patiënt zetten | Knoppen Joy, Anger, Fear, Sadness, Neutral op het paneel (laser + trigger, in de nurse view); **A** (rechtercontroller) gaat naar de volgende emotie, ook in de patient view waar de lasers uit staan; toetsenbord 1 tot 4 en 0 |
| Gesprek herstarten | Knop Restart, **B** (rechtercontroller) of R: Initial Emotion en het aantal switches komen terug op de start |
| Status | Het paneel toont de dominante emotie met de vier waarden, de view, het aantal switches (geen limiet meer sinds 2026-11-22) en of Y nu werkt (en anders waarom) |

De gekozen emotie vloeit over zoals na een antwoord (`EmotionHolder.Apply`): de gekozen emotie op 0,8, de andere op 0,05;
Neutral zet alles op 0 (geen dominante emotie, de kamer blijft gewoon). Een antwoord kiezen werkt nog gewoon en telt zijn
delta op bij wat het paneel zette.

**Code:** `Scripts/Testing/PatientViewTestControls.cs` (runtime, enkel in deze scene), `Editor/PatientViewTestPanel.cs`
(het paneel). **Scene:** `Scenario01SceneBuilder.BuildPatientViewTestScene` (menu *ProCommVR > Setup > 11. Build Patient
View Test Scene*); de scene komt achteraan in de Build Settings, zodat een build nog altijd met de shift start. Voor een
headset-build met enkel de testscene: zet `PatientViewTest` tijdelijk bovenaan in de Build Settings (niet committen).

**Niet in de shift:** `ShiftHospital` en `Scenario01_PatientRoom` hebben geen testpaneel.

**Beperkingen:** A en B zijn met een eigen binding in de component gezet (`<XRController>{RightHand}/primaryButton` en
`secondaryButton`), niet in de XRI-input actions; nog niet op de Quest 3 gecontroleerd.

## 20. Prototype1: de AI-patiënt in Liam's U-afdeling (`Prototype1`)

Eén volledige loop met één patiënt, als basis voor de hele gameloop: de feedback van de playtest van 2026-11-22
(dokter als nabespreking, AI-gesprek, vertrouwensmeter, Liam's ziekenhuis en personage). Werkt **enkel in de editor of via
Quest Link**: Liam's AI-server (`Tools/AiServer`, Ollama, Whisper, Piper) draait op de laptop, niet op de Quest.

**Verloop** (`Scripts/AiTalk/PrototypeFlow.cs`):
1. **Briefing:** de speler start in de linkergang voor de deur van kamer 1A, met een bord naast de deur.
2. **Gesprek:** binnen 2,6 m van de patiënt zegt die zijn openingszin (Liam's `AiNpc`, scenario `disappointed_patient`).
   **Gewoon praten** (Engels): de microfoon staat altijd aan, geen knop (zie *Handenvrij praten* hieronder). **Y** voor de
   patient view, **B** 1,2 s ingedrukt houden (F in de editor) om het gesprek zelf af te ronden. De AI-server rondt ook zelf af (opgelost of een
   kritieke fout).
3. **Naar de dokter:** fade, de speler staat in de briefingruimte voor de arts (`Doctor_Female`, bij het whiteboard). Die zegt
   een korte zin in een ballon en daarna opent Liam's nabespreking (`AiDebriefPanel`: score, rubric, betere zinnen).
4. **A: opnieuw proberen** brengt de speler terug naar de patiënt met een nieuw gesprek; **B: klaar** sluit het prototype af.

De teksten van de dokter, het bord en de hint staan in de inspector van `PrototypeFlow` en `Signs` (data, geen code).

**AI en de patient view** (`Scripts/AiTalk/AiPatientBridge.cs`, op de patiënt naast `AiNpc`):
- Implementeert `IPatientConversation` (nieuw, `Scripts/Scenarios/`), net als `ScenarioPresenter`. De `PovController` werkt met
  beide: het veld *Conversation Source* (de brug) of zoals vroeger *Presenter*. Y kan zolang het AI-gesprek loopt; een
  Sinds 2026-11-22 is er geen limiet meer op het aantal switches (*Max Switches Per Conversation* 0 = geen limiet).
- Na elke beurt: het emotie-label van de AI-server naar onze emoties (Happy: Joy, Angry: Anger, Worried en Surprised: Fear,
  Sad, Crying en Pain: Sadness; Neutral met spanning vanaf 55 blijft Sadness, vertrouwen vanaf 70 wordt Joy), sterkte uit de
  spanning (of het vertrouwen bij Joy). Alles aanpasbaar in de inspector. `EmotionHolder.SetTarget` (nieuw) laat het overvloeien.
- Het vertrouwen van de AI-server (0 tot 100) is de **reputatie** van de patiënt (`PatientReputation`, nieuw).
- De patiënt ligt in bed: zijn ogen kijken naar het voeteneinde (`PovAnchor` > *Look At*, nieuw).

**Vertrouwensmeter in de HUD:** onder de vier emoties "TRUST IN YOU" met een balk en een getal (rood onder 40, oranje,
groen vanaf 60). Enkel zichtbaar als de HUD een `PatientReputation` heeft (`EmotionHudSetup.AddToScene(..., reputation)`);
in de andere scenes blijft hij verborgen.

**Effecten in kamer 1A** (`Prototype1SceneBuilder.BuildEffects`): elk punt op de vloer gaat via een raycast op de vloer en
enkel waar geen meubel of muur staat (`EmotionWorldSetup.OnFloor`, nieuw), zodat niets zweeft.
- Joy: drie planten op de vloer en zonlicht door het raam.
- Anger: vuur op het nachtkastje, aan het voeteneinde en op de vloer.
- Fear: donkerder dan voor de playtest (profiel `EmotionWorld_Fear`: belichting -0,9, vignet 0,65, licht 0,01,
  ambient 0,02, mist 0,45), enkel de zaklamp op het hoofd.
- Sadness: **regen binnen** in de kamer (`EmotionWorldSetup.AddIndoorRain`, van net onder het plafond tot de vloer, met
  spatjes), **plassen** die groeien op de vloer (`AddPuddle`, materiaal `Materials/Emotions/Puddle.mat`) en druppels.

**De speler zoals de patiënt hem ziet** (`Scripts/Player/ReputationAura.cs`, gebouwd door `Editor/ReputationAuraSetup.cs`):
in de patient view is de speler een **blanco witte figuur** (`Materials/Player/AvatarWhite.mat` vervangt in Play mode alle
materialen van de avatar; de rig-prefab zelf blijft ongewijzigd) met een aura volgens het vertrouwen:

| Vertrouwen | Niveau | Effect |
| --- | --- | --- |
| 80 tot 100 | Radiant | stralend wit-gouden licht, lichtstralen, vleugels van licht die zachtjes klapperen |
| 60 tot 80 | Golden | iets donkerdere gouden aura, gouden deeltjes, warm licht op de figuur |
| 40 tot 60 | Neutral | niets |
| 20 tot 40 | Liquid | zwarte doorzichtige vloeistof die traag langs de figuur naar beneden loopt, plas op de vloer |
| 0 tot 20 | Abyss | zwarte golven die van boven het hoofd over de figuur naar beneden stromen, zwarte wolk en plas |

De grenzen 40 en 60 zijn die van `PatientReputation` (*Low* en *High Threshold*), dezelfde als de kleuren van de
meter, zodat meter en aura altijd overeenkomen; 20 en 80 staan in de inspector van de aura. De niveaus vloeien in 1,2 s in elkaar over, een niveau op gewicht 0 staat uit. De aura
staat naast de avatar (niet erin) en volgt hem; enkel zichtbaar zolang de avatar zichtbaar is (de patient view). De
vleugels zijn een mesh die bij het starten gegenereerd wordt (`ReputationAura.WingMesh`). Effecten met de bestaande
deeltjesmaterialen (`Particle_Additive`, `Particle_Alpha`), max. 180 deeltjes per systeem.

**Dev-klembord in de lobby** (`Scripts/Testing/DevClipboard.cs`, gebouwd door `Editor/DevClipboardSetup.cs`): Liam's
klembord op de balie van de staff station (in deze scene vervangen door een kopie, zijn prefab blijft ongewijzigd) met
een canvas en twee pagina's (knoppen `<` en `>`, laser + trigger):
- **Tasks:** een sjabloon voor de taken van de shift (tabel #, Task, Room, Status en Notes). De tekst staat in de
  inspector (*Tasks Text*), geen code.
- **Dev:** de emotie van de patiënt (Joy, Anger, Fear, Sadness, Neutral), een slider voor het vertrouwen (0 tot 100),
  *AI drives* en *Restart talk*, plus een status (emotie, wie emotie en vertrouwen bepaalt, gesprek, of Y werkt).
  Een waarde die je zelf zet blijft staan: de AI-beurten overschrijven ze niet meer (`AiPatientBridge.HoldEmotions`
  en `HoldTrust`) tot je op *AI drives* drukt.
- **Take / Put down:** het klembord zit **vanaf de start in de linkerhand** (*Start In Hand*); klikken met de rechterlaser.
  *Put down* legt het op de balie in de lobby, *Take* neemt het terug. In de patient view is het verborgen.

**Handenvrij praten** (`Scripts/AiTalk/VoiceActivityTalk.cs`): vervangt in Prototype1 Liam's push-to-talk (dat staat er nog,
maar uit). Feedback van de playtest van 2026-11-22: bij elke druk op de trigger hing het beeld even vast (`Microphone.Start`
blokkeert) en het begin van de zin ging verloren. Nu:
- De microfoon start één keer en blijft aan (de Quest-microfoon, anders de standaardmicrofoon).
- Spraakdetectie op luidheid: een stukje van 20 ms is spraak als het duidelijk luider is dan het achtergrondgeluid
  (dat wordt voortdurend gemeten; *Noise Factor* 3, *Min Level* 0,012). Na 0,15 s spraak begint een zin, na 0,9 s stilte
  wordt ze verstuurd naar de AI-server (`AiNpc.SendStudentAudio`), met 0,4 s van vóór de spraak (*Pre Roll*), zodat het
  eerste woord heel blijft. Korter dan 0,5 s spraak: genegeerd; langer dan 15 s: toch verstuurd.
- Terwijl de patiënt praat of de AI denkt, luistert de microfoon niet (geen echo van de patiënt), tot 0,4 s erna.
- M in de editor zet de microfoon aan of uit. De hint bij het bed toont wat de microfoon doet (*Listening, just talk*,
  *Hearing you...*, *The patient is thinking...*).
- Alle waarden staan in de inspector. Test: `VoiceActivityTalk.Feed` voert opgenomen geluid in zonder microfoon.

**Microfoon kiezen:** de Dev-pagina van het klembord toont de microfoon, het niveau (*HEARD* als het als spraak telt) en
*Next mic*. De keuze wordt onthouden. Geeft de gekozen microfoon 3 s helemaal geen geluid (bv. de Quest-microfoon terwijl
Quest Link de invoer naar een andere microfoon stuurt), dan schakelt `VoiceActivityTalk` vanzelf naar de volgende (niet
onthouden). Elke 10 s staat in de log hoe luid de microfoon was.

**Klembord met X:** **X** op de linkercontroller (C in de editor) neemt het klembord in de hand of legt het op de balie.

**Voortgangsbalk van het gesprek** (`Scripts/AiTalk/ConversationProgressBar.cs`): een oranje balk onderaan in het midden van
het beeld tijdens het gesprek. Ze vult als de patiënt kalmeert (vertrouwen naar 66, spanning naar 36: de voorwaarde van de
laatste fase) en sneller als hij boos wordt (spanning naar 92 of vertrouwen naar 5: de escalatie). Tot 92% tot het gesprek
echt eindigt, dan vol. De doelen staan in de inspector en moeten gelijk blijven aan het scenariobestand.

**Langer gesprek en escalatie** (`Tools/AiServer/scenarios/disappointed_patient.json`, data):
- Een fase meer: B1 (teruggetrokken) naar B2 (opent zich, vertrouwen 38+) naar B3 (overweegt, vertrouwen 52+, een open
  vraag, samenvatting, uitleg of steun) naar END (vertrouwen 66+, spanning onder 36 en een concreet voorstel). Getest met de
  echte AI: een goed gesprek duurt nu 7 beurten (was ongeveer 3).
- `escalation` (nieuw, algemeen in `engine.py`): spanning 92+ of vertrouwen 5-: de patiënt zegt boos dat je moet gaan
  (fase ESCALATED) en het gesprek eindigt met `escalated` (nabespreking: "Gesprek geëscaleerd").
- `rules.unsafe_effect` (nieuw): beledigingen en dreigingen (unsafe) wegen zwaarder (spanning +25, vertrouwen -20) dan
  off-topic (+5). Nieuw label `rude_or_hostile` (+20/-20). Getest: één grove zin ("Stop whining, nobody cares about your
  stupid party") beëindigt het gesprek meteen.
- Test zonder Unity: `Tools/AiServer/test_conversation_length.py` (speelt een goed en een grof gesprek in tekst).

**Plattegrond** (`Editor/WardMapAndAmbienceSetup.cs`): `Art/Map/UWardFloorPlan.png` (hetzelfde plan als `UWardLayout`, 42 x 28
m, ongeveer 50 px per meter) hangt in de gang onder, op de muur van de medicatiekamer, net links van de ingang van de lobby.
Oranje punten: *You are here*, kamer 1A (patiënt), de balie en de dokter (briefingruimte).

**Achtergrondgeluid:** `Audio/Ambience/HospitalBabble.wav` (40 s, loopt rond), gemaakt met `Tools/Ambience/make_babble.py`:
twee Piper-stemmen van de AI-server (kristin en norman, op LibriVox-opnames) zeggen gewone ziekenhuiszinnen door elkaar,
gedempt en met galm, geen echt gesprek. Vijf 3D-bronnen (lobby, gangen) starten elk op een ander punt (`AmbienceLoop`).

**Aanpassingen aan Liam's code:** `AiDebriefPanel` kreeg *Open On Conversation End* (in Prototype1 uit: het paneel opent pas
bij de dokter) en het event `Retried`. De rest van Liam's AI-code is ongewijzigd.

**Teleport:** Liam's lege markers `Hospital/Teleport/TeleportArea_*` voor de hal, de gangen, kamer 1A en de briefingruimte
krijgen een `TeleportationArea`.

**Beperkingen:** nog niet getest op de Quest 3 of via Quest Link (microfoon, knoppen, 72 fps). Geen muziek (komt nog). De
spraakdetectie is enkel getest met opgenomen spraak, nog niet met de echte Quest-microfoon in een lawaaierige ruimte. De AI-scenario's en de mapping naar emoties zijn nog
na te kijken door de inhoudsexpert.

