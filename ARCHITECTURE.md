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
    Materials/PatientRoom/  URP-materialen voor de patiëntenkamer (scenario 1), zie sectie 11
    Prefabs/                prefabs, o.a. de XR rig variant
    Prefabs/UI/             UI-prefabs, o.a. DialogueBubble
    Scenes/                 scenes
    Scripts/                runtime-code, namespace ProCommVR
    Scripts/Scenarios/      scenario-systeem, namespace ProCommVR.Scenarios
    Scripts/Atmosphere/     mood-systeem, namespace ProCommVR.Atmosphere
    Settings/Input/         input actions
    Settings/Moods/         mood-profielen (licht, ambient, muziek per mood), zie sectie 10
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

`Assets/_Project/Scenes/XRTest.unity` (derde scene in de Build Settings, na `Scenario01_PatientRoom` en `ScenarioTest`)
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

In batchmode: `-executeMethod ProCommVR.Editor.XRProjectSetup.ConfigureForQuest`, daarna
`ProCommVR.Editor.XRTestSceneBuilder.ImportStarterAssets` en
`ProCommVR.Editor.XRTestSceneBuilder.BuildTestScene` (elk in een aparte run). Voor het
scenario-systeem: `ProCommVR.Editor.ScenarioTestSceneBuilder.ImportTmpEssentialsAndExit`
(zonder `-quit`, want de package-import is asynchroon), daarna `BuildBubblePrefab`,
`BuildResultsScreenPrefab` en `BuildScenarioTestScene`. Voor de patiëntenkamer:
`ProCommVR.Editor.Scenario01SceneBuilder.BuildScene`.

## 8. Scenario's

Scenario's zijn **data**, geen code: elk scenario is een asset (ScriptableObject) in
`Assets/_Project/Dialogue/Scenarios/`. Een nieuw scenario toevoegen vraagt geen nieuwe code.

### Datastructuur (`Assets/_Project/Scripts/Scenarios/`)

| Klasse | Wat |
| --- | --- |
| `Scenario` | Eén gesprek: `Id`, `Title`, `Mood`, `NpcName`, `NpcLine` (openingszin van de NPC) en precies 4 `Options` |
| `DialogueOption` | Eén antwoord van de speler: `Text`, `Score` (verborgen), `Feedback` (uitleg, enkel op het eindscherm) en de NPC-reactie: `Npc Reaction` (korte zin), `Npc Pose` en optioneel `Npc Voice` (audioclip) |
| `NpcPose` | Lichaamstaal van de NPC na een keuze: `Unchanged`, `Engaged`, `TurnsAway`, `Withdrawn` |
| `Mood` | Sfeer van het scenario: `Neutral`, `Tense`, `Somber`. Stuurt licht en muziek aan via het mood-systeem (sectie 10) |

Een scenario heeft altijd precies 4 opties. Zijn het er meer of minder, dan geeft Unity een
waarschuwing en past het de lijst aan naar 4.

**Scores:** de schaal is nog een **voorstel**, af te stemmen met content en de inhoudsexpert:
beste optie `3`, matig `1`, laag `0`, kritische fout `-2`. Per scenario: 1 beste, 2 middelmatige
en 1 duidelijk foute optie (`GAME_BRIEF.md`). De score is nooit zichtbaar tijdens het spel.

### Weergave

- `ScenarioPresenter` (in de scene) toont een `Scenario`: de NPC-zin in een ballon boven de NPC
  en de 4 opties als ballonnen in een 2x2-raster voor de speler, in **willekeurige volgorde**.
- **Opties enkel dichtbij:** de NPC-zin is meteen zichtbaar, de 4 opties zijn enkel zichtbaar
  zolang het hoofd van de speler binnen **1,5 m** (horizontaal) van de NPC staat (`GAME_BRIEF.md`).
  Ze verschijnen tussen de speler en de NPC: 0,7 m voor het hoofd, 25 cm onder ooghoogte, naar
  de speler gericht. Stapt de speler weg (verder dan 1,5 m + 10 cm marge), dan verdwijnen ze weer;
  komt hij terug, dan verschijnen ze opnieuw voor hem, in dezelfde volgorde. De marge voorkomt
  geflikker als de speler net op de grens staat.
  Instelbaar op de presenter onder **Proximity**: `Npc` (leeg = opties meteen tonen),
  `Options Range`, `Options Hide Margin`, `Player` (leeg = de main camera, dus de headset), `Place Options In Front Of
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

Een echt NPC-model met animaties (nu een capsule), opgenomen stemmen, de omgeving voor scenario 2 (gang),
en echte muziek voor de moods.

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

Elk scenario heeft een `Mood` (`Neutral`, `Tense`, `Somber`). Bij de start van een scenario past
de sfeer zich aan: licht, ambient licht en achtergrondmuziek vloeien in 1,5 s over naar het
profiel van die mood (`GAME_BRIEF.md`, Sfeer en mood-systeem).

| Klasse | Wat |
| --- | --- |
| `MoodProfile` | Asset per mood: `Mood`, `Light Color`, `Light Intensity`, `Ambient Color`, `Music` (optioneel, loopt) en `Music Volume` |
| `MoodController` | In de scene: luistert naar `ScenarioPresenter.ScenarioStarted` en zet het hoofdlicht, het ambient licht en de muziek op het juiste profiel |

- De profielen staan in `Assets/_Project/Settings/Moods/` (`Mood_Neutral`, `Mood_Tense`,
  `Mood_Somber`). Het zijn **placeholders**: enkel licht, nog geen muziek.
- In `ScenarioTest` staat een **Mood Controller** met een AudioSource (2D, loop). Het tweede
  placeholder-scenario staat op `Tense`, dus bij "Next scenario" verandert het licht merkbaar.
- Het ambient licht wordt op **Flat** gezet zodra er een mood wordt toegepast.
- Een mood zonder profiel laat de sfeer ongewijzigd en geeft een waarschuwing in de Console.

**Muziek toevoegen:** zet het audiobestand in `Assets/_Project/Audio/` (map aanmaken), selecteer
het profiel in `Settings/Moods/` en sleep de clip in **Music**. Stel **Music Volume** in.
Geen code nodig.

**Een nieuwe mood toevoegen** (bijvoorbeeld `Hopeful`): voeg de waarde toe **achteraan** in
`Mood.cs` (achteraan, anders schuiven de bestaande scenario's van mood), maak via
**Create > ProCommVR > Mood Profile** een profiel, kies de nieuwe mood en sleep het profiel in
de lijst **Profiles** van de Mood Controller.

**Performance (Quest 3):** de mood verandert enkel kleur en intensiteit van bestaande lichten,
er komen geen extra lichten of schaduwen bij. Als de scenes later baked lighting gebruiken,
werkt de lichtkleur van de mood enkel op realtime of mixed lichten; dan kan een mood ook via
post-processing (color grading) of light probes werken. Dat beslissen we samen met de echte
omgevingen.

## 11. Scene: Scenario 1, patiëntenkamer

`Assets/_Project/Scenes/Scenario01_PatientRoom.unity` speelt `Scenario01_DisappointedPatient`. Staat eerst in
de Build Settings, dus een headset-build start hiermee. `ScenarioTest` blijft bestaan als technische testscene.
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

