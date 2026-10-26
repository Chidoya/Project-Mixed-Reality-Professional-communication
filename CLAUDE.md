# CLAUDE.md: Professional Communication VR (Immersive Storytelling)

Dit bestand wordt automatisch ingeladen door Claude Code bij elke sessie in deze repo.
Het bevat de projectcontext. Alle Git-regels staan in een apart bestand (zie "Git workflow").
Houd dit bestand actueel: "Status" en "Beslissingen" zijn het geheugen van het project.

## Git workflow

De volledige Git workflow staat in `instructie.md` (opgesteld door Jalil) en is bindend:
@instructie.md

Samengevat (bij twijfel geldt altijd `instructie.md`):
- Vraag bij de start van elke taak wie er werkt (Jalil of Liam) en wat de taak is.
- Flow: `feature/<owner>/<naam>` vanaf de persoonlijke branch, PR naar de persoonlijke branch,
  later een door een mens gereviewde PR van de persoonlijke branch naar `main`.
- Nooit committen, pushen of mergen op `main`, `Jalil` of `Liam`. Altijd toestemming vragen voor een push.
- Commit messages: `feat: ...`, `fix: ...`, `docs: ...`, `chore: ...` (Engels).

Let op hoofdletters: op GitHub heten de persoonlijke branches `Jalil` en `Liam` (met hoofdletter).
`instructie.md` schrijft `jalil` en `liam`. Gebruik in git-commando's altijd de echte
namen (`origin/Jalil`, `origin/Liam`) zolang de branches niet hernoemd zijn.
Feature branches mogen wel in kleine letters: `feature/jalil/dialogue-system`.

## Over het project

- Vak: Merging Realities: Immersive Storytelling (AP Hogeschool Antwerpen), multidisciplinair, 2026-2027.
- Opdracht "Professional Communication": een VR-ervaring waarmee eerste- en tweedejaars
  zorgstudenten hun professionele communicatie (en veerkracht) trainen in situaties
  waarin mensen lijden, zichtbaar of niet.
- Voorbeeld uit de opdracht: ViCoSim (Artevelde).
- Mogelijke varianten (nog te kiezen): slechtnieuwsgesprek, motiverende gespreksvoering,
  (psycho)educatie, gesprek met suïcidale patiënt, omgaan met agressie.
- Platform: Meta Quest 3 (standalone, Android). Publicatie via SideQuest + APK.
- Nooit echte patiëntgegevens gebruiken: enkel fictieve of goedgekeurde scenario's.

## Team (5)

| Rol | Persoon | Opleiding | Verantwoordelijk voor |
| --- | --- | --- | --- |
| Developer | Liam | EA-ICT | TODO |
| Developer | TODO | EA-ICT | TODO |
| Content / script | TODO | Communicatie | Dialoogscript, playbook, gesprekstechnieken |
| Story / media | TODO | Journalistiek | Storyboard, opnames, demo video, documentatie |
| Inhoudsexpert | TODO | TODO (mogelijk verpleegkunde) | Realisme, theorie, testen met doelgroep |

## Belangrijke data

- DocLab Amsterdam: 14-15 november 2026 (verplicht voor COMM/JOU, optioneel voor ons).
- Midterm presentatie + peer assessment 1: TODO datum.
- Prototype demo: TODO datum.
- Finale presentatie + peer assessment 2: januari 2027, TODO datum.

Mijlpalen (volgorde uit de lesplanning): concept en user-centered design, storystructuur en
immersiedesign, midterm, opnames en prototype bouwen, prototype demo, finetuning en finale.

## Op te leveren (eindbeoordeling)

VR-applicatie
- Gedeelde map met alle assets, documenten, code, clips en APK.
- Publicatie op SideQuest.

Documentatie
- Teamsamenstelling + rollen + groepsnummer.
- Titel die het functionele doel duidelijk maakt + visual.
- Geschreven playbook + visuele storyboards (logica van de simulatie).
- Toepassing van theoretische concepten in de VR-ervaring (min. 1 A4).
- 1 paragraaf: waarom VR een meerwaarde is t.o.v. zonder VR.
- Ervaring multidisciplinaire samenwerking (min. 1/2 A4).
- Individuele conclusie per teamlid (min. 10 zinnen).
- Volgende stappen van prototype naar echt product (min. 1/2 A4).

Opnames
- Filmpje (demo) van de VR-applicatie.
- Opgenomen teampresentatie (max. 20 minuten).

Theorie uit de les die we kunnen gebruiken: levels of immersion (involvement, engrossment,
total immersion), typologie (strategic, tactical, spatial, narrative, temporal, emotional),
dissociation, presence, flow, model van Hyunkook Lee (plausibility, interestingness, interactivity).

## Notion

Workspace "Kyumin Yominro's Space". Notion is voor planning en coördinatie, GitHub is de bron
van waarheid voor code. Beweer nooit dat Notion bijgewerkt is als de tool het niet effectief deed.

- Project Hub: https://app.notion.com/p/3f7b4e82942c814299f4c9f681a5b10c
- Project Tasks (database): https://app.notion.com/p/9c8d4928ebf64c3590c2105c1bb85a2f
  - Velden: Task, Status (Not started / In progress / Done), Assignee, Priority (Low / Medium / High),
    Workstream (Research / Storytelling / Unity / Testing / Documentation / Presentation),
    Schedule (datum of periode), GitHub Issue (URL), Notes.
  - Zet in Notes: acceptatiecriteria, persoonlijke branch en feature branch.
- Brainstorm & Storyboards: https://app.notion.com/p/3f7b4e82942c8122b2ffc5a7f25a50f6
- Meetings & Decisions (meeting template + decision log): https://app.notion.com/p/3f7b4e82942c8141b782f6a0ac0cd3f1
- Unity & GitHub (technische afspraken + build log): https://app.notion.com/p/3f7b4e82942c81c5bd43dcb6241856fe
- Documentation & Deliverables (eindchecklist): https://app.notion.com/p/3f7b4e82942c81948963ce83a92131ad

Taak in Notion naast de Git workflow
1. Taak bestaat in Project Tasks met Assignee, Workstream, Priority en Schedule.
2. Codetaak: GitHub issue aanmaken en de link in "GitHub Issue" zetten.
3. Status "In progress" bij de start, "Done" na de merge in de persoonlijke branch.
4. Demo build: datum, versie, toestel, resultaat en blockers in de build log (pagina Unity & GitHub).
5. Belangrijke beslissing: decision log in Notion en "Beslissingen" hieronder.

## Technische stack

- GitHub repo: https://github.com/Chidoya/Project-Mixed-Reality-Professional-communication
- Unity versie: 6000.3.9f1 (Unity 6.3). Beide developers exact deze versie, geen upgrade zonder akkoord.
- Render pipeline: URP (aanbevolen voor Quest)
- XR: XR Interaction Toolkit 3.6.1 + OpenXR 1.18.0 (XR Plug-in Management 4.7.0),
  Meta Quest feature, profielen Oculus Touch en Meta Quest Touch Plus. Details: `ARCHITECTURE.md`.
- Interactie: laserpointer op beide controllers, trigger = select, grip = activate, geen gaze-select.
- Dialoogsysteem: TODO (Yarn Spinner / Ink / eigen JSON of ScriptableObjects)
- Spraak (optioneel): TODO
- Build target: Android, ARM64, IL2CPP, min. API 32, Vulkan, Linear color space

## Unity en code conventies

Unity
- Asset Serialization: Force Text en Version Control mode: Visible Meta Files (beide al ingesteld).
- Git LFS en `.gitattributes`: nog niet ingesteld. Voorstel, maar enkel na akkoord van het team
  (zie `instructie.md`, sectie 4): fbx, blend, png, jpg, psd, wav, mp3, mp4.
- Eén persoon tegelijk in een scene; werk zoveel mogelijk in prefabs. Scene-eigenaar staat in Notion.
- Mappenstructuur onder `Assets/_Project/`: `Scenes/`, `Scripts/`, `Prefabs/`,
  `Art/`, `Audio/`, `Dialogue/`, `Materials/`, `Settings/`, `Editor/` (editor-scripts).
- Third-party assets buiten `_Project/` laten. Package-samples staan in `Assets/Samples/` en
  worden niet aangepast; wijzigingen aan de XR rig gebeuren in de prefab variant
  `Assets/_Project/Prefabs/XR Origin (ProCommVR).prefab`.
- Houd `ARCHITECTURE.md` bij bij elke stap die iets opzet of verandert (zie `GAME_BRIEF.md`).

Code (C#)
- Namespace: `ProCommVR`.
- PascalCase voor klassen en publieke members, `_camelCase` voor private fields.
- Dialoogcontent hoort in data (bestanden), niet hardcoded in scripts, zodat
  niet-IT teamleden het script kunnen aanpassen zonder Unity-kennis.
- Test op de Quest 3 zelf, niet alleen in de editor. Doel: stabiel 72 fps.
  Kan er niet op de headset getest worden, zeg dat dan expliciet in het eindrapport.

## Build validatie

Voor elke commit (zie `instructie.md`, sectie 5, stap 3) draai je een Unity batchmode
compile-check, niet enkel vertrouwen op wat de editor toont:

```bash
"/mnt/c/Program Files/Unity/Hub/Editor/6000.3.9f1/Editor/Unity.exe" \
  -batchmode -quit -nographics \
  -projectPath "<pad naar dit project>" \
  -logFile "<pad naar dit project>/build.log"
```

- Commit enkel als het logbestand geen compile-errors bevat.
- Kan Unity in deze omgeving niet draaien (geen toegang tot het Windows-bestandssysteem
  of de Unity-installatie), zeg dat dan expliciet in plaats van te doen alsof er gecheckt is.
- Dit is een compile-check, geen volledige Android-build. Een Android-build testen
  (bijvoorbeeld voor een demo) gebeurt apart en enkel na overleg, want dat duurt langer.

## Hoe Claude moet helpen

- Antwoord in het Nederlands, code, commits en PR-beschrijvingen in het Engels.
- Geen em dashes in teksten; gebruik punten, komma's of dubbele punten.
- Volg eerst de startprocedure uit `instructie.md` voor je bestanden wijzigt.
- Lees deze file en de relevante code voor je iets voorstelt.
- Stel bij grote wijzigingen eerst een plan voor en wacht op akkoord.
- Teksten voor de documentatie (playbook, theorie, reflectie) mogen als concept,
  maar de individuele reflecties schrijft elk teamlid zelf.
- Na een belangrijke beslissing of mijlpaal: stel voor om "Status" en "Beslissingen"
  hieronder bij te werken, en de Notion decision log.
- Hou rekening met gevoelige thema's (suïcide, agressie): realistisch en respectvol,
  met een duidelijke debriefing en uitstapmogelijkheid in de ervaring.

## Status

- 2026-10-12: Notion hub, subpagina's en taakdatabase aangemaakt; database nog leeg.
  In Notion zijn enkel Liam en Jalil lid. GitHub repo met leeg Unity 6.3 URP project
  (branches `main`, `Jalil`, `Liam`). Jalil zette `instructie.md` (Git workflow voor Claude Code)
  op `main`; `Jalil` en `Liam` zijn op 2026-10-12 bijgewerkt naar `main`. Nog geen `.gitattributes`/LFS,
  geen XR packages, geen README, geen branch protection gecontroleerd.
  Scenario, rollen en data nog niet beslist.
- 2026-10-18: Jalil installeerde per ongeluk Unity 6000.6.6f1. Bevestigd dat het project bij
  de afgesproken versie 6000.3.9f1 blijft, zodat het bestaande lege project niet geüpgraded
  moet worden. Jalil installeert 6000.3.9f1 ernaast via Unity Hub (beide versies kunnen
  naast elkaar bestaan).
- 2026-10-19: PR #10 (`feature/jalil/scenario01-patient-room` naar `Jalil`) gemerged.
  Scenario 1 uitgewerkt (uit het brondocument, scores 3/0/-2/1, mood Somber) in
  `Scenario01_PatientRoom.unity` (4x4m kamer, deur-bed-raam). Eigen URP-materialen
  gebruikt omdat de asset-kit Standard-shaders had. Patient X zit rechtop op de bedrand
  ipv plat liggend. Proximity-bug opgelost met marge. Directe feedback vervangen door
  NPC-houding + korte reactiezin; volledige uitleg staat enkel nog op het eindscherm.
  Enkel compile-check en screenshot-renders gecontroleerd, nog niet getest in Play mode
  of op de Quest 3 zelf. De 4 reactiezinnen zijn door Claude verzonnen, nog niet
  nagekeken door content/inhoudsexpert.
- 2026-10-20: PR #11 (`feature/jalil/shift-gameloop`) en PR #12 (`feature/jalil/shift-followups`)
  gemerged in `jalil`. Shift-loop in `ShiftHospital.unity` (briefing, 5 patiëntkamers, timer,
  eindscherm) met mood per kamer (licht, deurbordje, sfeer). Elke patiënt heeft een interne
  `PatientId` (`patient-01` tot `patient-05`); op het scherm blijft het "Patient X".
  De patiënt zit op een stoel naast het bed (niet meer op de bedrand). Nieuwe component
  `MoodAudio` vloeit over naar het geluid van de mood; nu enkel placeholder-tonen.
  Getest: batchmode compile-check en Play mode smoke tests met de XR-simulator
  (`RunShiftLoop`, `RunShiftTimeout`, `RunScenario01Mood`, alle drie geslaagd).
  Nog niet getest op de Quest 3 of via Quest Link, overvloeien van het geluid niet beluisterd.
- 2026-10-25: PR #15 (Scenario 2, de boze dochter in de gang van verdieping 3) is rechtstreeks in `main` gemerged,
  niet in `jalil`. `origin/jalil` staat nog op PR #13. Kamer 2B is sinds Scenario 2 weer een kamer van Liam.
- 2026-10-26: Scenario 3 (de arts en de familie) gebouwd op `feature/jalil/scenario03-doctor-family`, op aanvraag van
  Jalil gestart vanaf de Scenario 2-commit `b594ba1` (optie B), omdat `jalil` Scenario 2 nog niet bevat. Taak 3 speelt aan
  de verpleegpost van verdieping 2 met kamer 2C als bureau van de arts; kamer 2A is weer van Liam. Nieuw en herbruikbaar:
  mood per antwoord, een toeschouwer (`BystanderSpeech`), `NpcAttention`, `BusyNpc` en `FacePlayer`. Getest: batchmode
  compile-check en Play mode smoke tests met de XR-simulator (alle bestaande plus `RunScenario03Good`/`Poor`). Nog niet
  getest op de Quest 3 of via Quest Link. Tekst, scores en reacties door Claude geschreven, nog na te kijken.


## Beslissingen

| Datum | Beslissing | Waarom |
| --- | --- | --- |
| 2026-10-12 | Unity 6000.3.9f1 + URP | Zelfde versie voor beide developers, URP geschikt voor Quest |
| 2026-10-12 | Git workflow volgens `instructie.md` (feature, persoonlijke branch, main) | Veilig werken met een agent, `main` blijft stabiel |
| 2026-10-12 | Notion voor planning, GitHub voor code | Gekoppeld via het veld "GitHub Issue" |
| 2026-10-18 | XR Interaction Toolkit + OpenXR, geen Meta XR SDK | Gevraagd in `GAME_BRIEF.md`; open standaard, geen vendor lock-in |
| 2026-10-18 | Trigger = select, grip = activate, geen gaze-select | Dialoogopties kiezen met laser + trigger, zonder tijdsdruk (`GAME_BRIEF.md`) |
| 2026-10-18 | Projectsetup gescript via menu `ProCommVR > Setup` | Reproduceerbaar voor beide developers, ook in batchmode |
| 2026-10-18 | Unity 6000.3.9f1 bevestigd, geen upgrade naar 6000.6.6f1 | Bestaand leeg project staat al in 6000.3.9f1, voorkomt onnodige project-upgrade |
| 2026-10-19 | Directe feedback vervangen door NPC-houding + reactiezin, volledige uitleg enkel op eindscherm | Realistischer, studenten leren non-verbale signalen lezen ipv meteen een score te zien |
| 2026-10-19 | Patient X zit rechtop op de bedrand ipv plat liggend | Past bij "sitting slumped" uit het scenario, makkelijker te animeren |
| 2026-10-19 | Eigen URP-materialen ipv de materialen uit de asset-kit | Kit gebruikte Standard-shaders, niet compatibel met URP op Quest |
| 2026-10-20 | `MoodController` houdt enkel de mood-data bij; lezers (`MoodAtmosphere`, `MoodRoomLight`, `RoomSign`) tonen ze | Eén mood-gegeven per patiënt/kamer, uitbreidbaar zonder dat de data iets van licht, UI of bordjes weet |
| 2026-10-20 | Shift-loop in een placeholder-ziekenhuis (`ShiftHospital`, Kenney) tot Liam's hospital op GitHub staat | `origin/Liam` bevatte enkel de personages, geen hospital-scene; de shift-scripts hangen niet aan deze layout |
| 2026-10-20 | `ShiftHospital` eerst in de Build Settings | Een headset-build start met de shift (briefing, 5 taken, eindscherm) |
| 2026-10-20 | Patiënt in de shift zit op een stoel naast het bed, niet op de bedrand (wijkt af van 2026-10-19) | De zitanimatie verwacht ongeveer 0,45 m zithoogte; de matras ligt op 0,66 m met bedhekken tot 0,83 m |
| 2026-10-20 | Interne `PatientId` per `ShiftTask`, op het scherm blijft het "Patient X" | Mood, voortgang en latere content per patiënt gescheiden houden zonder namen te tonen |
| 2026-10-20 | Geluid per mood via een aparte `MoodAudio`, die de mood enkel leest | `MoodController` blijft pure data; een nieuw geluid is enkel een clip in het mood-profiel, zonder code |
| 2026-10-26 | Scenario 3 tot 5 op gestapelde branches vanaf `b594ba1`, elk met een eigen PR naar `jalil` | `jalil` bevat Scenario 2 niet; zo kan een probleem in een later scenario een eerder niet breken (keuze van Jalil) |
| 2026-10-26 | Taakindeling: 1A (S1), gang 3 (S2), verpleegpost 2 + bureau 2C (S3), 1B (S4), 1C (S5); 2A terug naar Liam | Scenario 3 heeft een verpleegpost en een werkplek van de arts nodig; elk scenario op een eigen plek |
| 2026-10-26 | Mood na een keuze als optioneel veld van de optie (`Changes Mood`, `Mood After`), geen nieuw systeem | De bron laat de sfeer veranderen na de keuze (S3 opgelost, S4 paars naar blauw, S5 alarm); bordje, licht en geluid volgen vanzelf |
| 2026-10-26 | Drukke arts zonder navigatie-AI: een vaste ronde en een bureau (`BusyNpc`) | Volstaat voor de bron ("de arts vinden op een bereikbaar moment"), voorspelbaar en testbaar |


## Open vragen

- Scenario's: `GAME_BRIEF.md` kiest voor de MVP "de ontslagen patiënt" en "de boze dochter".
  Nog te bevestigen door het hele team (inhoudsexpert, content).
- Hoe "praat" de student: `GAME_BRIEF.md` kiest een keuzemenu (4 opties, laser + trigger).
  Spraak eventueel later?
- Persoonlijke branch van Jalil: `Jalil` of `jalil`? Er bestaan er nu twee op GitHub; één
  verwijderen (door een mens), en dan ook `Liam` / `liam` gelijk trekken met `instructie.md`.
- Git LFS instellen, en voor welke bestandstypes? (De XRI-sample, ongeveer 10 MB, staat nu zonder LFS in Git.)
- Locomotion uit de Starter Assets (thumbstick, snap turn, teleport) houden, of enkel fysiek rondlopen?
- Wie is de vijfde persoon en welke opleiding?
- Hoe meten we "resilience" en communicatie in de ervaring (feedback, score, debrief)?
- Echte mood-geluiden: de huidige `Mood_*_Placeholder.wav` zijn gegenereerde tonen, geen muziek.
  Wie maakt of kiest ze (en met welke licentie)?
- CREDITS.md voor de personages van Liam: bron en licentie nog na te vragen bij Liam.
- De vier placeholder-WAV's (ongeveer 1 MB) staan zonder LFS in Git; meenemen in de LFS-beslissing.
- Shift-loop en zithouding nog testen op de Quest 3 (72 fps, geluid op de headset).
- Oude remote branch `feature/jalil/shift-gameloop` mag door een mens verwijderd worden.
