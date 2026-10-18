# Game brief: Professional Communication VR

## Concept in één zin
Een VR dialoogtrainer waarin de speler als verpleegkundige lastige gesprekken voert met patiënten en familie, en via de gekozen antwoorden leert hoe hij professioneel en empathisch communiceert.

## MVP scope
2 scenario's, volledig uitgewerkt.
- Scenario 1: de ontslagen patiënt (hospital patient room)
- Scenario 2: de boze familielid (hospital hallway)

De andere 3 scenario's (angstige patiënt, slechte prognose, agressieve patiënt) komen pas na de MVP, zelfde structuur, dus makkelijk uit te breiden.

## Scene structuur (herbruikbaar per scenario)
- Speler staat of zit in de VR-omgeving tegenover de NPC
- NPC spreekt de situatie uit (tekst of audio)
- 4 dialoogopties verschijnen als tekstballonnen in de wereld, zoals in het ViCoSim-voorbeeld
- Speler kiest een optie (aankijken + bevestigen, of controller-knop)
- NPC reageert met animatie, houding en/of stem, passend bij de gekozen optie
- Korte feedback-tekst verschijnt die uitlegt waarom de keuze goed of minder goed was
- Door naar het volgende scenario

## Score systeem
- De score loopt mee op de achtergrond, niet zichtbaar tijdens het spel
- De speler ziet enkel de reactie van de NPC (gezicht, houding, stem, tekst)
- Per scenario is er 1 beste optie, 1 duidelijk foute optie, en 2 middelmatige opties, elk met een score zoals in de scenario-documenten (hoog, laag, kritische fout, matig)
- Het eindscherm toont de samenvatting: score per scenario en een korte uitleg per keuze

## Feedback
- Direct na elke keuze, voor de speler naar het volgende scenario gaat
- NPC-reactie eerst (emotioneel, in de wereld), dan een korte tekst met uitleg waarom het goed of fout was
- Geen timer, de speler mag rustig alle opties lezen voor hij kiest

## Scenario 1: de ontslagen patiënt
- Omgeving: ziekenhuiskamer, 1 patiënt (bed)
- NPC: patiënt X, 51 jaar, somber, kijkt uit het raam
- Context: wil naar huis voor de verjaardag van zijn kleindochter, weigert fysio
- 4 opties: professioneel empathisch (beste), dismissive, grens overschrijden met vals valse hoop, passief afschuiven
- Zie het brondocument voor de volledige dialogen en feedback-teksten per optie
- Scene layout: kamer van ongeveer 4 bij 4 meter, bed tegen de muur tegenover de deur, raam aan de zijkant. Speler start aan de deur, op ongeveer 2,5 meter van het bed, en kan vrij tot bij het bed lopen. Dialoogopties verschijnen zodra de speler binnen ongeveer 1,5 meter van de patiënt staat

## Scenario 2: de boze dochter
- Omgeving: ziekenhuisgang, voor de kamer van een oudere patiënte
- NPC: dochter, staat agressief, verheven stem
- Context: moeder moest te lang wachten op hulp naar het toilet
- 4 opties: de-escalatie (beste), defensief, agressief/dismissive, passief doorschuiven
- Zie het brondocument voor de volledige dialogen en feedback-teksten per optie
- Scene layout: gang van ongeveer 2,5 meter breed, kamerdeur van de patiënte in de zijmuur. Speler start aan het ene uiteinde van de gang, de dochter staat ongeveer 2 meter verder, vlak voor de kamerdeur, en blokkeert de doorgang. Dialoogopties verschijnen zodra de speler op ongeveer 1,5 meter van haar staat

## VR interactie
- Doeltoestel: Meta Quest 3 (standalone), build platform Android, XR Interaction Toolkit met OpenXR
- Dialoogopties kiezen via controller met laserpointer en trigger, geen gaze-select, geen timer dus geen haast nodig
- Handtracking is geen vereiste voor de MVP, kan later toegevoegd worden als er tijd over is
- NPC blijft op een vaste plek, speler kan zich vrij bewegen of rondkijken in de kamer/gang
- Tekstballonnen verschijnen in de wereld (zoals op het ViCoSim screenshot), niet als vlakke UI op het scherm
- Houd rekening met mobiele performance (Quest 3 is standalone): baked lighting waar mogelijk, beperkt polycount, geen zware real-time schaduwen

## Sfeer en mood-systeem
- Elk scenario krijgt een `mood` waarde (bijvoorbeeld neutral, tense, somber), die licht en achtergrondmuziek aanstuurt
- Scenario 1 en 2 staan op `neutral` voor de MVP
- Latere scenario's (bijvoorbeeld een slecht-nieuws-gesprek) kunnen gewoon een andere mood-waarde krijgen, geen nieuw systeem nodig
- Dit systeem nu laten meebouwen, ook al wordt het in de MVP nog niet gebruikt met echte muziek, zodat sfeer later toevoegen geen losse, ad-hoc oplossing wordt

## Taal
- Alle tekst in de game (dialogen, opties, feedback) is in het Engels, zoals in de brondocumenten

## Resultaten
- Voor de MVP enkel een eindscherm binnen de sessie, met score per scenario en korte uitleg per keuze
- Niks wordt weggeschreven of bewaard na het afsluiten van de game

## Wat niet in de MVP zit
- Scenario 3, 4 en 5
- Score zichtbaar tijdens het spel
- Timer of tijdsdruk
- Gevolgen die doorlopen tussen scenario's (bv. patiënt wantrouwiger na een slechte keuze)

## Technisch startpunt
- Unity project met XR Interaction Toolkit (OpenXR), build platform Android voor Quest 3
- Simpele kamer en gang eerst met placeholder-assets (cubes, basic materials), pas later vervangen door echte assets
- NPC start als een simpel character met basis-animaties (staan, zitten, gebaren), stem kan later toegevoegd worden, tekst is voldoende voor de eerste versie
- Scenario's worden opgeslagen als data (bijvoorbeeld ScriptableObjects of JSON), niet hardcoded per scene: elk scenario heeft NPC-tekst, de 4 opties, hun scores, feedback-teksten en een mood-waarde. Een nieuw scenario toevoegen wordt zo het toevoegen van 1 data-item, geen nieuwe code
- Commit na elke werkende stap (bijvoorbeeld "scenario 1 werkt", "scoring werkt"), zodat er altijd een werkende versie is om op terug te vallen

## Documentatie
- Houd een ARCHITECTURE.md bij in de root van het project
- Elke stap die iets opzet of verandert (project setup, scenario-datastructuur, een nieuw scenario, scoring, mood-systeem, ...) wordt er kort in beschreven: wat er is, hoe het werkt, en hoe je het uitbreidt
- Doel is dat iemand die de code niet kent, snel begrijpt hoe het project in elkaar zit en hoe je bijvoorbeeld een nieuw scenario toevoegt
