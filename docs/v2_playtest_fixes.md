# Scenario 1, v2: verbeterpunten uit de playtest

Branch `feature/liam/full-loop-v2`, vanaf `full-loop-infectedLegSchene` (`acb7514`). Worktree `Project_v2`.
Testen: open `Assets/_Project/Scenes/Prototype1.unity` en druk op Play (de AI-server start vanzelf).

| Playtest | Oorzaak in v1 | Oplossing in v2 |
| --- | --- | --- |
| Vertrouwen en stemming tellen niet mee in de score | De score kwam enkel uit labels per zin | Twee nieuwe rubric-items in `disappointed_patient.json`: 8 "Built his trust" (vertrouwen eindigt op 55 of meer: 2, minstens 10 gestegen: 1) en 9 "Lifted his mood" (spanning eindigt op 46 of lager: 2, minstens 8 gedaald: 1). Max score 18 (was 14). De dokter heeft er eigen stemlijnen en post-its voor. Grenzen staan in de JSON (`trust_final`, `tension_final`). |
| "Hij was droevig, we kozen afraid en de dokter zei dat dat juist was" | De dokter keek enkel naar de emotie na de laatste beurt, en `Surprised` telde als `Fear`. Na de (foute) kritieke reactie "Home? For the party?" was hij dus plots "bang". | Het juiste antwoord is nu wat hij **het meest** voelde over het hele gesprek (`felt_emotion` van de server, anders geteld over de opgenomen beurten). `Surprised` is geen gevoel meer: de patiënt houdt zijn vorige emotie. De kritieke reactie zelf is nu `Sad`. |
| "Ik mag je niet laten vertrekken, alleen de dokter mag dat" werd gezien als "je mag gaan" | Het sleutelwoord "let you go" stond in de zin en het kleine model labelde het als vals ontslag | Ontkenning wordt herkend ("can't / not allowed / may not ... let you go") en "only the doctor ..." telt als doorverwijzen. Zo'n zin geeft nu het goede label `deferred_to_doctor` (telt mee voor "Stays within the nursing role"). Een kritieke fout wordt bovendien nog eens met een ja/nee-vraag aan het model gecontroleerd voor het scenario eindigt. Ook "I can't promise ..." is geen valse belofte meer. |
| Wat we zeggen en wat de AI verstaat klopt soms niet | Whisper kende de woorden en namen van het scenario niet; een onzekere transcriptie werd toch gebruikt | Whisper krijgt de woordenlijst van het scenario (`stt_vocabulary`) en de naam van de student als hint. Is Whisper onzeker (`stt_min_confidence`, standaard -1.0), dan vraagt de patiënt om het te herhalen en verandert er niets aan vertrouwen of spanning. |
| "Ik heet Jalil" en de AI sprak me aan met X | De naam werd enkel herkend met een hoofdletter (Whisper schrijft vaak "jalil"), en "X" werd nooit tegengehouden | Nieuw veld **Nurse Name** in `AiTalkSettings` (Unity stuurt het mee bij de start). Zonder dat veld wordt de naam uit "I'm jalil" gehaald, ook in kleine letters. De patiënt krijgt de naam expliciet, en een antwoord dat de student "X" of een andere naam noemt wordt afgekeurd en opnieuw gevraagd. |
| De slotkaart zegt "The birthday card is still closed", maar de kaart in de kamer stond open | De kaart werd open gebouwd | De kaart staat nu dicht op het nachtkastje (`BirthdayCard`). Ze gaat open zodra de student een videogesprek of een kleine stap voorstelt, of als het gesprek goed eindigt. Opnieuw proberen sluit ze weer. |

Tests: `Tools/AiServer/tests/test_v2_fixes.py` (zonder Ollama) en `test_engine_rules.py`, de EditMode-test
`FeltEmotion_IsWhatHeFeltMost_NotOnlyTheLastTurn` en de Play mode smoke tests.

Nog na te kijken door de inhoudsexpert: de teksten van item 8 en 9 en hun grenzen.
