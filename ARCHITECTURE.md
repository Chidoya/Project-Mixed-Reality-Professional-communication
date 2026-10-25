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
    Audio/Placeholder/      placeholder-geluiden per mood (gegenereerde tonen), zie sectie 10
    Dialogue/Scenarios/     scenario-data (Scenario assets), zie sectie 8
    Dialogue/Shifts/        shift-plannen (ShiftPlan assets), zie sectie 12
    Editor/                 editor-scripts (setup, tools), niet in de build
    Editor/Testing/         Play mode-tests (batchmode), zie sectie 13
    Materials/              materialen
    Materials/PatientRoom/  URP-materialen voor de patiëntenkamer (scenario 1), zie sectie 11
    Prefabs/                prefabs, o.a. de XR rig variant
    Prefabs/UI/             UI-prefabs, o.a. DialogueBubble
    Scenes/                 scenes
    Scripts/                runtime-code, namespace ProCommVR
    Scripts/Scenarios/      scenario-systeem, namespace ProCommVR.Scenarios
    Scripts/Atmosphere/     mood-systeem, namespace ProCommVR.Atmosphere
    Scripts/Shift/          gameloop van een shift, namespace ProCommVR.Shift
    Scripts/Navigation/     verplaatsen tussen verdiepingen (lift), namespace ProCommVR.Navigation
    Settings/Input/         input actions
    Settings/Moods/         mood-profielen (kleur, licht, ambient, muziek per mood), zie sectie 10
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

`Assets/_Project/Scenes/XRTest.unity` (vierde scene in de Build Settings, na `ShiftHospital`, `Scenario01_PatientRoom` en `ScenarioTest`)
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

In batchmode: `-executeMethod ProCommVR.Editor.XRProjectSetup.ConfigureForQuest`, daarna
`ProCommVR.Editor.XRTestSceneBuilder.ImportStarterAssets` en
`ProCommVR.Editor.XRTestSceneBuilder.BuildTestScene` (elk in een aparte run). Voor het
scenario-systeem: `ProCommVR.Editor.ScenarioTestSceneBuilder.ImportTmpEssentialsAndExit`
(zonder `-quit`, want de package-import is asynchroon), daarna `BuildBubblePrefab`,
`BuildResultsScreenPrefab` en `BuildScenarioTestScene`. Voor de patiëntenkamer:
`ProCommVR.Editor.Scenario01SceneBuilder.BuildScene`. Voor de shift: `ProCommVR.Editor.ShiftHospitalSceneBuilder.BuildScene`.
Elke builder zet zijn scene **eerst** in de Build Settings. Bouw je ze allemaal opnieuw, doe `ShiftHospital` dan als
laatste, zodat een headset-build met de shift start.

## 8. Scenario's

Scenario's zijn **data**, geen code: elk scenario is een asset (ScriptableObject) in
`Assets/_Project/Dialogue/Scenarios/`. Een nieuw scenario toevoegen vraagt geen nieuwe code.

### Datastructuur (`Assets/_Project/Scripts/Scenarios/`)

| Klasse | Wat |
| --- | --- |
| `Scenario` | Eén gesprek: `Id`, `Title`, `Mood`, `NpcName`, `NpcLine` (openingszin van de NPC) en precies 4 `Options` |
| `DialogueOption` | Eén antwoord van de speler: `Text`, `Score` (verborgen), `Feedback` (uitleg, enkel op het eindscherm) en de NPC-reactie: `Npc Reaction` (korte zin), `Npc Pose` en optioneel `Npc Voice` (audioclip) |
| `NpcPose` | Lichaamstaal van de NPC na een keuze: `Unchanged`, `Engaged`, `TurnsAway`, `Withdrawn` |
| `Mood` | Sfeer van het scenario (of humeur van een patiënt): `Neutral`, `Tense`, `Somber`, `Happy`. Zie het mood-systeem (sectie 10) |

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
- **Animatie:** is er een **Animator** ingevuld, dan krijgt die een trigger met de naam van de pose
  (`Engaged`, `TurnsAway`, `Withdrawn`). Zo werkt het later ook met een echt personage met animaties.
- **Stem:** is er een **Voice**-AudioSource en heeft de optie een `Npc Voice`-clip, dan wordt die afgespeeld.

Zolang er geen NPC-model is, is de NPC een capsule en zijn de poses enkel verschuivingen en rotaties.

### Een nieuw scenario toevoegen (zonder code)

1. Ga in het Project-venster naar `Assets/_Project/Dialogue/Scenarios/`.
2. Rechtermuisklik > **Create > ProCommVR > Scenario** en geef het een naam, bijvoorbeeld
   `Scenario_DischargedPatient`.
3. Vul in de Inspector in:
   - **Id:** korte unieke naam in kleine letters, bijvoorbeeld `discharged-patient`
   - **Title**, **Mood**, **Npc Name** en **Npc Line** (wat de NPC zegt)
   - **Options:** voor elk van de 4 opties de **Text**, **Score** en **Feedback**, en de NPC-reactie:
     **Npc Reaction** (korte zin), **Npc Pose** en eventueel **Npc Voice**
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

### Placeholder

`Placeholder_Scenario.asset` ("The waiting patient", mood Neutral) en `Placeholder_Scenario2.asset`
("The upset relative", mood Tense) bevatten **verzonnen** testtekst, enkel om het systeem te testen. Echte scenario's komen van het content-team en de inhoudsexpert.

### Nog niet gebouwd

Een echt NPC-model in `ScenarioTest` en `Scenario01_PatientRoom` (daar nog een capsule; de shift-scene in sectie 12
gebruikt al de personages van Liam), opgenomen stemmen, de omgeving voor scenario 2 (gang), en echte muziek of
geluiden voor de moods (de infrastructuur staat, nu met placeholder-tonen, sectie 10).

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
| `Mood` (enum) | `Neutral`, `Tense`, `Somber`, `Happy` |
| `MoodProfile` | Asset per mood: `Mood`, `Color` (de kleur van de mood: deurbordje en kamerlicht), `Light Color`, `Light Intensity`, `Ambient Color`, `Sound` (loopend geluid: muziek of ambiance, leeg = stilte) en `Sound Volume` |
| `MoodController` | **Enkel data.** Houdt de huidige mood van één scene, kamer of patiënt bij (`CurrentMood`, `CurrentProfile`, `CurrentColor`) en meldt elke wijziging via het event `MoodChanged`. De mood komt van code (`ApplyMood`), van een gekoppelde `ScenarioPresenter` (volgt de mood van elk scenario dat start) of van `Initial Mood` bij de start |
| `MoodAtmosphere` | Lezer: zet het hoofdlicht en het ambient licht in 1,5 s op het profiel van de mood |
| `MoodAudio` | Lezer: speelt de `Sound` van de mood op de AudioSource van zijn object en **kruisfadet** (2 s) naar het nieuwe geluid als de mood verandert. Maakt daarvoor zelf een kopie van die AudioSource, zodat oud en nieuw geluid even overlappen |
| `MoodRoomLight` | Lezer: geeft één licht in een kamer de kleur van de mood (`Strength`: 0 = wit, 1 = de volle kleur) |
| `RoomSign` | Lezer (sectie 12): het deurbordje toont de kleur van de mood naast de patiënt |

- De profielen staan in `Assets/_Project/Settings/Moods/` (`Mood_Neutral` grijs, `Mood_Tense` oranje, `Mood_Somber`
  blauw, `Mood_Happy` groen). Het zijn **placeholders**: kleur, licht en een placeholder-geluid.
- **Placeholder-geluiden:** `Assets/_Project/Audio/Placeholder/Mood_<mood>_Placeholder.wav`, zachte gegenereerde tonen
  van 6 s die naadloos loopen (Happy: majeurakkoord, Somber: laag mineurakkoord, Tense: dissonant met tremolo, Neutral:
  zachte brom), volume 0,25. Geen echte muziek, geen licentie nodig. De Setup-builders vullen deze clip enkel in bij een
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

De speler werkt een shift af: briefing, een aantal patiënten verzorgen, eindscherm. Voorlopig is dit **infrastructuur
met placeholder-inhoud**: 5 keer Scenario 1, elke patiënt `Happy`.

### Verloop

1. **Briefing:** de speler start in de **lobby** van Liam's ziekenhuis, voor de receptiebalie (die dient als briefing room).
   De taken uit het shift-plan zijn al aan de kamers toegewezen: de deurbordjes en de shift-borden tonen de patiënten,
   de kamerlichten hebben de kleur van hun humeur.
2. **Start:** de speler richt de laser op de **computer op de receptiebalie** en drukt de trigger in. Alle kamers starten
   hun gesprek en de timer loopt.
3. **Taken:** de patiënten liggen op verdieping 1 (kamers 1A, 1B, 1C) en 2 (2A, 2B). De speler gaat met de **lift**:
   laser + trigger op een knop van het liftpaneel naast de liftdeur brengt hem naar die verdieping. In elke kamer zit een patiënt. De NPC-zin hangt boven de patiënt, de 4 opties verschijnen binnen 1,5 m
   (zoals in sectie 8). Na de keuze reageert de patiënt (houding, zin) en verschijnt de ballon **"Finish"**. Pas daarna
   is de patiënt "verzorgd": het bordje toont "Cared for" en de borden tellen mee.
4. **Einde:** zodra alle patiënten verzorgd zijn **of** de timer op 0 staat (wat eerst komt) stoppen alle gesprekken
   en verschijnt het **Shift report** 85 cm voor de speler: per kamer de score en de feedback, of "not cared for".
   Het spel stopt daar. De ballon **"Close game"** sluit de app (in de editor stopt hij Play mode). Er is nog geen
   volgende shift.

### Klassen (`Assets/_Project/Scripts/Shift/`)

| Klasse | Wat |
| --- | --- |
| `ShiftPlan` | Asset (Create > ProCommVR > Shift Plan): de lijst **Tasks**. Taak 1 gaat naar de eerste kamer van de ShiftManager, taak 2 naar de tweede, ... |
| `ShiftTask` | Eén patiënt: `Patient Id` (uniek in het plan, bijvoorbeeld `patient-01`), `Scenario`, `Patient Name` (leeg = de NPC-naam van het scenario, enkel voor weergave) en `Patient Mood` |
| `ShiftManager` | De gameloop: wijst de taken toe, `StartShift()`, timer (`Shift Duration` in seconden, nu 600 als testwaarde), einde bij alle taken klaar of tijd op, toont het Shift report. Houdt de voortgang bij **per patiënt-id**: `IsCompleted(patientId)`, `FindRoom(patientId)`, `SetPatientMood(patientId, mood)`. Events: `ShiftStarted`, `TaskCompleted`, `ShiftEnded` |
| `PatientRoom` | Eén kamer: `Room Name`, eigen `ScenarioPresenter`, `MoodController` (het humeur van de patiënt) en `RoomSign`. `PatientId` = de id van de toegewezen taak. Zet bij toewijzing de mood en het bordje, speelt het scenario bij de start en meldt `TaskCompleted` na "Finish" |
| `RoomSign` | Het deurbordje: kamer, patiënt, scenariotitel en een kleurvlak met de mood-kleur (leest de `MoodController`); toont "Cared for" na de taak |
| `ShiftBoard` | Wereld-UI-paneel met de patiënten van de shift (Waiting / Cared for), de resterende tijd en de teller. Er staan er drie: in de lobby (achter de receptiebalie) en in de gang van verdieping 1 en 2 |
| `ShiftStartButton` | Op de computer: laser + trigger start de shift (XRSimpleInteractable). Het label erboven kleurt blauw als de laser erop staat |
| `LiftButton` (`Scripts/Navigation/`) | Op een knop van Liam's liftpaneel: laser + trigger teleporteert de speler (via de `Teleportation Provider` van de rig) naar het aankomstpunt voor de lift op die verdieping, met de blik de gang in. "L" is de lobby. De liftdeuren blijven dicht: de rit is een teleport |

**Patiënt-id:** de weergavenaam mag overal hetzelfde zijn (nu 5 keer "Patient X" met 5 keer Scenario 1); intern
onderscheidt het systeem de patiënten enkel op `Patient Id`. Een taak zonder id, met een dubbele id of zonder scenario
geeft een waarschuwing in de Inspector (`ShiftPlan.OnValidate`) en wordt bij de start van de shift overgeslagen. Het
placeholder-plan gebruikt `patient-01` tot `patient-05`. Echte content per patiënt (eigen scenario, mood, later
dossiergegevens) hangt dus aan die id, niet aan de naam of de kamer.

Ook nieuw: `NpcAnimationState` (`Scripts/Scenarios/`) speelt bij de start één state van de Animator van een personage,
bijvoorbeeld `Sitting`. `ResultsScreen` kreeg `ShowText(subtitle, body)` zodat de shift zijn eigen rapport kan tonen.

### Scene en layout

`Assets/_Project/Scenes/ShiftHospital.unity` wordt volledig opgebouwd door `Assets/_Project/Editor/ShiftHospitalSceneBuilder.cs`
(menu **ProCommVR > Setup > 8. Build Shift Hospital**). Staat **eerst** in de Build Settings, dus een headset-build start met de shift.

De builder opent **Liam's ziekenhuis** (`Assets/Scenes/liam_test_scene.unity`, gebouwd door Liam's
`ProCommVR > Hospital > Build`) als sjabloon, voegt de shift toe en bewaart het resultaat als `ShiftHospital`. Liam's scene
en scripts worden niet aangepast. Bouwt Liam zijn ziekenhuis opnieuw, draai dan ook deze builder opnieuw. Verandert
Liam de namen of de indeling (`Hospital/Floor1/Room_1A`, `Card_1A`, `Door_1A`, `Furniture/Chair`, `Anchor_Sitting`, `Light`,
`Lift/LiftButton_2`, `Lobby/Furniture/ReceptionDesk`), dan meldt de builder welk object hij niet vindt.

- **Gebouw (Liam):** lobby op de begane grond, 3 verdiepingen van 3,5 m hoog met elk een gang van 18 x 3 m en 3 kamers
  (A, B, C) van 4 x 4 m aan de noordkant. De lift zit in de westmuur. Kamers, meubels (Kenney), deuren (Sketchfab), borden,
  lichten en de zorgverleners als decor komen uit Liam's builder.
- **Briefing (lobby):** de computer (`ShiftStartButton`) staat op de bezoekerskant van de receptiebalie, naar de speler
  gericht. Het shift-bord hangt achter de balie aan de noordmuur (1,4 keer groter, want de speler leest het van ongeveer
  3 m). De speler start 1,3 m voor de balie.
- **Shift-kamers 1A, 1B, 1C, 2A, 2B** (taak 1 naar 1A, ...): Liam's kamer krijgt de `PatientRoom`-component. Liam's
  patiëntkaart en zijn patiënt voor die deur maken plaats voor ons deurbordje (`RoomSign`, op de plek van de kaart, rechts
  van de deur) en onze patiënt. De deur zwaait open, de kamer naar binnen. Liam's plafondlicht van de kamer krijgt de
  `MoodRoomLight` (geen extra licht), het mood-geluid (`MoodAudio`, 3D, tot 4 m) hangt midden in de kamer.
- **Patiënten:** de personages van Liam (`Patient_Male`, `Patient_Female_Old`, `Patient_Male_Old`, `Patient_Female`,
  `Patient_Male_Old`), zittend (Animator-state `Sitting`) op Liam's stoel naast het bed. Liam's stoel heeft al de zithoogte
  van de animatie (0,45 m). De builder draait de stoel naar de deur, zoals Liam's eigen patiënten zitten. De `NpcReactor`
  draait de patiënt bij een keuze naar de verpleegkundige (de deur, `Engaged`), naar het bed (`TurnsAway`) of naar het raam
  (`Withdrawn`). Zie de beslissing van 2026-10-20 over de stoel in plaats van de bedrand.
- **Andere kamers** (2C, 3A, 3B, 3C): blijven zoals Liam ze bouwde (zijn kaart, "Coming soon" of zijn eigen case 3A).
  Hun deuren blijven dicht en krijgen een box-collider, zodat niemand erdoor loopt.
- **Lift:** de knoppen van Liam's liftpanelen (lobby 1, 2, 3; verdiepingen L, 1, 2, 3) krijgen een `XRSimpleInteractable`
  en een `LiftButton`. Aankomstpunten: `Shift/Lift Arrivals/Arrival L` tot `Arrival 3`, 1,2 m voor de liftdeur.
- **Licht:** Liam's lichten (één directional light, een point light per gang-segment, per kamer en twee in de lobby, geen
  schaduwen). Daarbovenop een eigen `Ward Mood` (MoodController op `Neutral` + MoodAtmosphere) voor het hoofdlicht en het
  ambient licht, zonder geluid. Liam's `Main Camera` wordt verwijderd: de XR rig heeft zijn eigen camera.
- **Colliders:** Liam's muren en vloeren zijn primitives met box-colliders, zijn meubels hebben mesh-colliders.

**Performance (Quest 3), nog te controleren op de headset:** de builder logt het totale aantal driehoeken: 42.078 voor
de hele scene (richtwaarde ongeveer 100k). Realtime lichten: 1 directional en 20 point lights (9 kamers, 9 gang-segmenten,
2 lobby), geen schaduwen. Geluid: 5 loopende 3D-bronnen (tijdens een crossfade even 2 per kamer), mono 22 kHz, buiten 4 m
geruisloos.

### Uitbreiden

- **Andere patiënten of scenario's:** open `Assets/_Project/Dialogue/Shifts/Shift_Placeholder.asset` en vul per taak
  een ander **Scenario**, **Patient Name** en **Patient Mood** in. Geen code nodig.
- **Een andere mood per patiënt:** kies de mood in de taak. Bordje, kamerlicht en geluid volgen vanzelf (sectie 10).
  Tijdens het spel: `ShiftManager.SetPatientMood("patient-03", Mood.Somber)`.
- **Een nieuwe patiënt in het plan:** geef hem een nieuwe, unieke **Patient Id**.
- **Duur van de shift:** `Shift Duration` op de **Shift Manager** in de scene.
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
| `RunShiftLoop` | Een volledige shift in `ShiftHospital` (Liam's ziekenhuis): unieke patiënt-id's (ook met 5 keer dezelfde naam), mood Happy in elke kamer (data, kamerlicht en bordje groen, Happy-geluid 3D), elke patiënt **zichtbaar zittend** (Animator in `Sitting`, heupen tussen 0,3 en 0,75 m, hoofd onder 1,45 m, heupen boven de zitting van de stoel, stoel naast het bed), mood van één patiënt wijzigen via zijn id (enkel die kamer verandert, geluid kruisfadet), start via de computer in de lobby, met de **lift** naar verdieping 1 en 2 (laser op de liftknop, de speler komt op de juiste hoogte aan), per kamer: geen opties vanaf de deur, 4 opties binnen 1,5 m (en geen opties van de patiënt recht erboven of eronder), keuze, "Finish", voortgang per id, dan het eindscherm met elke kamer, de drie borden op "Shift ended" en 5 / 5, met de lift terug naar de lobby, waar de computer niets meer doet |
| `RunShiftTimeout` | Zelfde scene, de shift duurt 4 s: na 1 patiënt loopt de tijd af, het spel stopt met 1 / 5 en de andere kamers stoppen hun gesprek |
| `RunScenario01Mood` | `Scenario01_PatientRoom`: na de fade hebben het hoofdlicht en het ambient licht de waarden van het Somber-profiel, en het Somber-geluid speelt |

De shift-tests gebruiken **gesimuleerde XR-devices** (`XRSimulatedHMD` en `XRSimulatedController`, dezelfde devices
als de XR Interaction Simulator): de "headset" kijkt rond, de rechtercontroller richt de laser op de computer en de
ballonnen en drukt de trigger in. Zo gaat elke klik door de echte rig (Near-Far Interactor, XR UI Input Module). Enkel
het lopen wordt overgeslagen: de test verplaatst de XR Origin naar het liftpaneel, de deur en de patiënt. De liftrit
zelf gaat door de echte `LiftButton` en de `Teleportation Provider` van de rig. Mislukt een klik, dan logt de test ook
welke colliders tussen de controller en het doel zitten.

**Starten:** via het menu **ProCommVR > Tests** in de editor, of in batchmode (zonder `-quit`):

```bash
Unity.exe -batchmode -projectPath <project> -logFile <log> \
  -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunShiftLoop
```

Optioneel `-smokeTestScreenshots <map>`: bewaart onderweg beelden van de camera van de speler (zonder `-nographics`):
de lobby, het shift-bord, elke aankomst met de lift, de deur van elke kamer, de patiënt van zij, de opties, de reactie en
het eindrapport.
Een test vervangt geen test op de Quest 3 zelf: performance, leesbaarheid in de headset en echte handbewegingen moet je
daar nog controleren.

