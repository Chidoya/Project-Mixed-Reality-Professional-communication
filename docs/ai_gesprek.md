# Vrij gesprek met Sarah (AI) in Unity

Unity praat met de lokale AI-server `Tools/AiServer` (zie de README daar): spraak naar tekst, classificatie,
boosheid en vertrouwen, het antwoord van Sarah en haar stem. Alles draait op de laptop.

## Testen in de Editor

1. Start Ollama en de server: `cd Tools/AiServer` en `.venv\Scripts\python server.py` (GPU: zie de README).
2. Open `Assets/Scenes/liam_ai_gesprek_test.unity` (opnieuw bouwen: **ProCommVR > AI Talk > Build Test Scene**).
3. Play. Sarah zegt de openingszin. Houd **spatie** ingedrukt, praat Engels, laat los.
   Op de Quest: de **trigger van de rechtercontroller**.

Het debugpaneel links van Sarah toont de transcriptie, labels, boosheid, vertrouwen, band, fase en de wachttijden.
Uitzetten: `showDebugPanel` in `Assets/Hospital/Data/AiTalkSettings.asset`.

## Scripts (`Assets/Hospital/Scripts/AiTalk/`, namespace `ProCommVR.Hospital.AiTalk`)

| Script | Wat |
| --- | --- |
| `AiTalkSettings` | serverUrl (`http://127.0.0.1:8765`, niet localhost), scenarioId, maxRecordSeconds, showDebugPanel, timeout, reservezin, fps-limiet in de Editor |
| `AiTalkClient` | UnityWebRequest naar `/session/start`, `/turn/audio`, `/session/{id}/end` (Awaitable, timeout 20 s). Fout of `retry`: lokaal "Sorry, what did you say?" |
| `PushToTalk` | Microfoon zolang de knop ingedrukt is (`AiTalkInput.inputactions`: spatie + rechtertrigger). Knipt af, 16 kHz mono wav, korter dan 0,4 s wordt genegeerd |
| `WavUtility` | Samples naar wav (16 kHz mono) en wav naar AudioClip |
| `AiNpc` | Op het personage. Luisterreactie meteen na het loslaten, antwoord als 3D-geluid op het hoofd, `FaceController.SetExpression`, Animator-state, `HeadLookAt` (TurnsAway/Withdrawn kijkt weg), mond via `TalkLevel`. Onderbreken: de knop indrukken terwijl Sarah praat stopt haar |
| `AiDebugPanel` | World-space testpaneel |

Events op `AiNpc` voor licht, geluid of de hartmonitor: `OnMoodChanged(float anger, float trust)` en
`OnConversationEnded(string outcome)` (`resolved`, `time_up`).

## Nabespreking

`AiDebriefPanel` opent als het gesprek eindigt: afspraak gemaakt (`resolved`), tijd op (4 minuten, Sarah zegt
"They're taking him away now. I have to go.") of de testtoets **E**. Tijdens het wachten staat Sarah in Idle met
een smal vak "Even nadenken over je gesprek...". Daarna een world-space paneel (1200 x 960 mm, 1 px = 1 mm) op
ongeveer 0,85 m, altijd vóór Sarah:
- de samenvatting en de score, de lijn van de boosheid per beurt met de 3 belangrijke momenten;
- de 11 rubric-items met de score als bolletjes (onzeker in het grijs), de feedback, het citaat en een betere zin;
- **Opnieuw proberen** (A / R): nieuwe sessie, zelfde scenario. **Klaar** (B / Enter).

Op de Quest: A en B op de rechtercontroller (geen XR-ray nodig). In de Editor: R en Enter, of de muis.

De scores komen niet van een taalmodel maar uit de labels per beurt (regels per item in
`Tools/AiServer/scenarios/angry_daughter.json`, `rubric[].scoring`), net als de feedback en de betere zinnen
(`rubric[].feedback`, `rubric[].better_phrase`). Die teksten moeten nog nagelezen worden door een
verpleegkundige. De samenvatting van 2 zinnen wordt standaard gemaakt uit dezelfde vaste teksten (`summary_mode: "template"`); met `"model"` schrijft het taalmodel ze. De nabespreking duurt dan minder dan een seconde (met het model 1 tot 2,5 s).
Screenshots: `docs/Screenshots/ai_debrief_goed.png`, `ai_debrief_slecht.png`
en `ai_debrief_wachten.png`.

## Wachttijden

Gemeten op de laptop (RTX 4070 Laptop), Whisper op de GPU:

| | spraakherkenning | classificatie | acteur | spraak | totaal |
| --- | --- | --- | --- | --- | --- |
| server alleen | 0,3-0,6 s | 0,8-1,1 s | 0,9-1,5 s | 0,1-0,6 s | 2,6-3,7 s |
| met de Unity Editor in Play mode | 0,3-0,7 s | 1,8-2,7 s | 2,8-3,9 s | 0,4-0,9 s | 6-8 s |

De Editor deelt de GPU en CPU met Ollama, ook met de fps-limiet (72). Op een losse Quest (de headset rendert zelf)
speelt dat niet; met Quest Link of in de Editor wel.

## Automatische test

`Unity -projectPath . -executeMethod ProCommVR.Editor.AiTalkCapture.Run -aiWavs "a.wav;b.wav" -aiShot docs/Screenshots/x.png -aiShotTurn 2 -aiLog Temp/ai.log`
speelt wav-bestanden af als studentbeurten (zelfde weg als PushToTalk na het loslaten, enkel zonder microfoon)
en neemt een screenshot terwijl Sarah antwoordt. Screenshots: `docs/Screenshots/ai_gesprek_goed.png` en
`ai_gesprek_slecht.png`.

## Alle 5 scenario's

De testscene bevat de vijf scenario's van Jalil als vrij gesprek. Kies met de toetsen **1 t.e.m. 5** (Editor) of de
**linker joystick** naar links/rechts (Quest). Elk scenario heeft zijn eigen personage (zelfde als in Jalils
`ShiftHospitalSceneBuilder`), plek en stem:

| # | Scenario (`Tools/AiServer/scenarios/`) | Personage | Plek | Spanning | Kritieke fout |
| --- | --- | --- | --- | --- | --- |
| 1 | `disappointed_patient` | Patient_Male | in bed (eenpersoonskamer) | moedeloosheid | vals ontslag beloven |
| 2 | `angry_daughter` | Visitor_Daughter | gang met brancard | boosheid | - |
| 3 | `doctor_and_family` | Doctor_Male (de zoon wacht verderop) | gang | ongeduld | - (verwijten en bevelen wegen zwaar) |
| 4 | `poor_prognosis` | Patient_Female_Old naast Patient_Male_Old in bed | stoel naast het bed | onrust | valse hoop, prognose meedelen |
| 5 | `aggressive_patient` | Patient_Male, Patient_Male_Old in het tweede bed | stoel in een gedeelde kamer | boosheid | diagnose hardop noemen |

Een kritieke fout beëindigt het scenario meteen: het personage reageert nog één keer met een vaste zin en de
nabespreking toont de fout bovenaan in een rode balk. Een kritiek label telt alleen als een van zijn sleutelwoorden
echt gezegd werd (`critical_requires_any` in de JSON), zodat een fout label het gesprek niet onterecht stopt.

De scenario's 1, 3, 4 en 5 zijn **concepten** op basis van Jalils teksten (feiten, kritieke fouten, feedback):
laat de rollen, labels en feedbackteksten nalezen door de inhoudsexpert of een docent verpleegkunde.

Voor Jalil: niet-verbale acties (gordijn dicht, afstand nemen) kunnen naar `POST /session/{id}/event`
(`{"event": "curtain_closed"}` of `"stepped_back"`), met het effect uit `aggressive_patient.json` (`events`).
