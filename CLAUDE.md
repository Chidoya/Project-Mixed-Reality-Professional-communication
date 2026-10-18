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
- XR: nog te installeren (XR Interaction Toolkit + OpenXR, of Meta XR SDK)
- Dialoogsysteem: TODO (Yarn Spinner / Ink / eigen JSON of ScriptableObjects)
- Spraak (optioneel): TODO
- Build target: Android, ARM64, IL2CPP

## Unity en code conventies

Unity
- Asset Serialization: Force Text en Version Control mode: Visible Meta Files (beide al ingesteld).
- Git LFS en `.gitattributes`: nog niet ingesteld. Voorstel, maar enkel na akkoord van het team
  (zie `instructie.md`, sectie 4): fbx, blend, png, jpg, psd, wav, mp3, mp4.
- Eén persoon tegelijk in een scene; werk zoveel mogelijk in prefabs. Scene-eigenaar staat in Notion.
- Mappenstructuur onder `Assets/_Project/`: `Scenes/`, `Scripts/`, `Prefabs/`,
  `Art/`, `Audio/`, `Dialogue/`, `Materials/`, `Settings/`.
- Third-party assets buiten `_Project/` laten.

Code (C#)
- Namespace: `ProCommVR`.
- PascalCase voor klassen en publieke members, `_camelCase` voor private fields.
- Dialoogcontent hoort in data (bestanden), niet hardcoded in scripts, zodat
  niet-IT teamleden het script kunnen aanpassen zonder Unity-kennis.
- Test op de Quest 3 zelf, niet alleen in de editor. Doel: stabiel 72 fps.
  Kan er niet op de headset getest worden, zeg dat dan expliciet in het eindrapport.

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

## Beslissingen

| Datum | Beslissing | Waarom |
| --- | --- | --- |
| 2026-10-12 | Unity 6000.3.9f1 + URP | Zelfde versie voor beide developers, URP geschikt voor Quest |
| 2026-10-12 | Git workflow volgens `instructie.md` (feature, persoonlijke branch, main) | Veilig werken met een agent, `main` blijft stabiel |
| 2026-10-12 | Notion voor planning, GitHub voor code | Gekoppeld via het veld "GitHub Issue" |
| 2026-10-18 | Unity 6000.3.9f1 bevestigd, geen upgrade naar 6000.6.6f1 | Bestaand leeg project staat al in 6000.3.9f1, voorkomt onnodige project-upgrade |

## Open vragen

- Welk scenario kiezen we?
- Hoe "praat" de student: keuzemenu, spraak naar tekst, of een combinatie?
- Welke XR-aanpak: XR Interaction Toolkit + OpenXR of Meta XR SDK?
- Branches hernoemen naar `jalil` en `liam` (kleine letters), zodat ze overeenkomen met `instructie.md`?
- Git LFS instellen, en voor welke bestandstypes?
- Wie is de vijfde persoon en welke opleiding?
- Hoe meten we "resilience" en communicatie in de ervaring (feedback, score, debrief)?
