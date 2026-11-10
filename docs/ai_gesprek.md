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
