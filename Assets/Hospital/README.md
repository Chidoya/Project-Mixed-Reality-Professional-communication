# Assets/Hospital (Liam)

Het ziekenhuis in `Assets/Scenes/liam_test_scene.unity`, volledig opgebouwd door **ProCommVR > Hospital > Build**
(`Editor/HospitalBuilder*.cs`). Dit is omgeving en vaste plekken; de spellogica (shift, scenario's) is van Jalil.
Bouw je opnieuw, draai daarna ook Jalils **ProCommVR > Setup > 8. Build Shift Hospital**.

Bestaande namen (`Hospital/Lobby/Furniture/ReceptionDesk`, `Lift`, `Floor*`, `Room_*`, `Card_*`, `Door_*`, ...) blijven
ongewijzigd. Hieronder enkel wat nieuw is.

## Verpleegpost in de lobby: `Hospital/Lobby/StaffStation`

Voor het begin en einde van de shift, oost van de receptiebalie. Vrij gelaten: de bezoekerskant van de balie
(Jalils startcomputer) en de noordmuur achter de balie, x 6,67 tot 7,93 m (Jalils ShiftBoard).

| Object | Waar |
| --- | --- |
| `Badge_Reader` | noordmuur, x 8,3 m, onderkant op 1,1 m. Kind `Lamp` (apart object, kleur wisselbaar) |
| `Tablet_Dock` | noordmuur, x 8,8 m, onderkant op 1,0 m. Past de `Care_Tablet` rechtop |
| `Wall_Clock` | noordmuur, x 8,55 m, onderkant op 2,05 m. `WallClock` zet de wijzers op de tijd van het toestel |
| `Clipboard` | op de balie, personeelskant (oosteinde) |
| `Care_Tablet` | op de balie, personeelskant (westeinde). Kind `Screen` |
| `Supply_Cart` | op de vloer naast de balie (oostkant), laden naar de lobby, met een BoxCollider |

### Ankers (lege objecten)

De blauwe as (forward) is de richting waarin de speler kijkt of reikt.

| Anker | Plek | Forward |
| --- | --- | --- |
| `Anchor_BadgeScan` | 10 cm voor het midden van de badgelezer | +Z, naar de lezer |
| `Anchor_TabletDock` | in de gleuf van het dock (waar de tablet rechtop komt) | +Z, naar de muur |
| `Anchor_Clipboard` | op de clipboard op de balie | zoals de clipboard ligt |
| `Anchor_EndScreen` | oostmuur, z 4,0 m, hoogte 1,7 m: vrije muur voor een scherm aan het einde van de shift | +X, naar de muur (zoals `CreateCanvas` van Jalil: de leesrichting) |

## Lichtstroken in de gang: `Hospital/FloorN/GuideLights/Guide_<deur>`

Per deur (1A tot 3C) een smalle strook in de vloer: langs de gang vanaf de lift, dan haaks naar de deur.
De strook naar A ligt het dichtst bij de deuren, zodat de stroken elkaar niet kruisen. Emissive materiaal
(`Materials/FloorGuide.mat`), geen lichtbron, geen collider.

Script `Scripts/FloorGuideLight.cs` (namespace `ProCommVR.Hospital`) op elk `Guide_<deur>`:

```csharp
var guide = hospital.Find("Floor1/GuideLights/Guide_1A").GetComponent<FloorGuideLight>();
guide.SetColor(FloorGuideLight.Calm);       // groen = rustig
guide.SetColor(FloorGuideLight.Difficult);  // paars = moeilijk
guide.Pulse();                              // een paar keer oplichten in de huidige kleur
guide.SetNeutral();                         // standaard: zwak neutraal wit
guide.SetColor(Color.black);                // uit
```

Werkt via een MaterialPropertyBlock: elke strook kan een eigen kleur hebben, het gedeelde materiaal verandert niet.

## Props: `Art/Props`

19 low-poly props uit Blender (`blender/scripts/06_props.py`), elk een FBX met een prefab in `Art/Props/Prefabs`
en gedeelde URP Lit materialen in `Art/Props/Materials` (effen kleuren, flat shading, schaal 1).
**ProCommVR > Hospital > Setup Props** (`Editor/PropSetup.cs`) zet ze klaar; Build doet dat zelf als er prefabs ontbreken.

Klein (< 300 driehoeken): `Lunch_Tray`, `Pill_Blister`, `Water_Glass`, `Photo_Frame`, `Birthday_Card`, `Tissue_Box`,
`Newspaper`, `Clipboard`, `Care_Tablet`, `Surgical_Gown`.
Groter (max 800): `Vitals_Monitor`, `Wall_TV`, `Wall_Clock`, `Badge_Reader`, `Tablet_Dock`, `Nursing_Station_Desk`,
`Supply_Cart`, `Gurney`, `Privacy_Curtain_Rail`.

Aparte kinderen om aan te sturen:
- `Screen` (vlak met UV's 0 tot 1, voor een texture of RenderTexture): `Vitals_Monitor`, `Nursing_Station_Desk`, `Wall_TV`, `Care_Tablet`.
- `Hand_Hour`, `Hand_Minute`, `Hand_Second`: `Wall_Clock`. `Lamp`: `Badge_Reader`.

Goed om te weten bij het plaatsen:
- **Oorsprong** onderaan het object; bij wandobjecten ligt de achterkant op de oorsprong (tegen de muur).
  `Privacy_Curtain_Rail`: oorsprong onderaan de haakjes, de bovenkant (plafond) ligt 0,11 m hoger.
- **Voorkant** naar **-Z** in Unity.
- **De root van elke prefab heeft een rotatie van 90 graden rond X** (overblijfsel van de FBX-conversie; de mesh staat
  wel rechtop). Draai een prop dus enkel rond de wereld-Y: `t.rotation = Quaternion.Euler(0, yaw, 0) * t.rotation`,
  niet `t.rotation = Quaternion.Euler(0, yaw, 0)`.
- **Klokwijzers**: draaien rond hun lokale Y-as (de as uit Blender). In de ruimte van de klok-root is "met de klok mee"
  de as (0, -1, 0); in de wereld staat die loodrecht op de wijzerplaat. Gemeten door Setup Props en gecontroleerd:
  om 3:00 wijst de uurwijzer naar rechts (gezien van voren), om 9:30 tussen 9 en 10. `WallClock` gebruikt deze as.

## Scripts (`Scripts/`, namespace `ProCommVR.Hospital`)

| Script | Doel |
| --- | --- |
| `PatientCase` | Data per patiënt (deur, naam, situatie, startpose, opties) |
| `CharacterPose` | Start een Animator-state (Sitting, Laying, ...) |
| `WallClock` | Wijzers op de echte tijd; `SetTime(DateTime)` voor een vaste tijd |
| `FloorGuideLight` | Kleur en puls van een lichtstrook in de gang |
| `ItemSpots`, `RoomTint`, `EarGrow`, `MonitorScreen`, `PassingCart` | Losse stukken voor de 360°-afleidingen, zie hieronder |

## Losse stukken voor de 360°-afleidingen

Liam bouwt de stukken, Jalil koppelt ze aan zijn scenario's. Ze zitten niet in de kamers die Jalil gebruikt
(1A, 1B, 1C, 2C, gang van verdieping 3, verpleegpost verdieping 2), maar in de testscene
`Assets/Scenes/liam_props_test.unity` en in prefabs. **ProCommVR > Hospital > Build Props Test**
(`Editor/HospitalBuilder.PropsTest.cs`) bouwt de testscene en de prefabs opnieuw (vereist een eerdere Hospital > Build).
Elk script heeft **testknoppen in de Inspector** (`Editor/HospitalTestButtons.cs`); behalve bij ItemSpots enkel in Play mode.
Getest in Play mode (editor), niet op de Quest 3.

| Stuk | Wat | Aanroepen |
| --- | --- | --- |
| `ItemSpots` | Een object verspringt tussen benoemde plekken (lege objecten); het houdt zijn rotatie ten opzichte van de plek. | `MoveTo(int)`, `MoveTo("Spot_Windowsill")`, `Next()`, event `Moved` |
| `RoomTint` | Het bestaande licht van een kamer langzaam dimmen (30 %), opwarmen (warm oranje, 85 %) of terugzetten, in 3 s. Geen extra lichtbronnen; zonder lijst neemt hij alle lichten onder zijn object. | `Dim()`, `Warm()`, `Restore()` |
| `EarGrow` | Op een Humanoid-personage: een klein oor ("Ear_Grow", huidskleur) aan het hoofdbot groeit van schaal 1 naar 2,5 en de krant zakt 25 cm en kantelt. Rechteroor standaard (instelbaar). | `Grow(seconds)`, `Reset()` |
| `MonitorScreen` | Lopende hartslaglijn (LineRenderer) op het `Screen`-vlak. Alarm: rode lijn, 130 bpm, scherm knippert rood met een piep (AudioClip in code, 3D). | `SetAlarm(bool)` |
| `PassingCart` | Een kar rijdt over een pad van punten (bv. door de gang), draait enkel rond de wereld-y. | `Play()`, `Stop()`, `ResetToStart()` |

Prefabs (`Prefabs/`):
- `Case1_Props.prefab`: in de ruimte van een `PatientRoom` (zet hem als kind van een `Room_*` op lokale positie 0). Tafel
  aan de linkermuur met `Lunch_Tray` en `Birthday_Card`, `Photo_Frame` aan de muur erboven, `Water_Glass` op het
  nachtkastje, en de `Pill_Blister` met `ItemSpots` tussen `Spot_Tray`, `Spot_Nightstand` en `Spot_Windowsill`.
- `Vitals_Monitor_Live.prefab`: variant van de `Vitals_Monitor` met `MonitorScreen` en een AudioSource; lijnmateriaal
  `Materials/MonitorLine.mat` (URP Particles/Unlit, vertexkleur).

In de testscene: `Room_Case1` (Case1_Props, RoomTint, monitor), `Room_Case5` (de buurman `Patient_Male_Old` zittend op de
stoel met een krant in `NewspaperHolder`, `EarGrow`, een `Wall_TV` tegenover hem) en `Corridor` (`Supply_Cart` en `Gurney`
met `PassingCart`, elk met een `Path_*` vol waypoints).

Aandachtspunten voor het koppelen:
- **RoomTint en Jalils `MoodRoomLight`** sturen allebei het kamerlicht. Niet tegelijk op hetzelfde licht laten werken,
  of RoomTint pas starten nadat de mood gezet is (RoomTint onthoudt de beginstand bij Awake).
- **EarGrow.Reset()** is ook de naam van Unity's editorbericht `Reset`; dat doet dan niets zolang het oor nog niet bestaat.
  Het oor wordt pas gemaakt bij Awake (Play mode) of bij de eerste `Grow`.
- **PassingCart, vooruit**: de lokale as van de prop-root. Voor de props is lokaal voorkant (0, -1, 0), boven (0, 0, -1)
  en Blender +X = (1, 0, 0). Supply_Cart rijdt met het handvat achteraan: (-1, 0, 0). Gurney met het hoofdeinde voorop: (1, 0, 0).
- **Props op wereldposities, enkel rond de wereld-y draaien** (zie hierboven). De krant van de buurman zit in een eigen
  houder (`NewspaperHolder`) die kantelt; de prop zelf houdt zijn rotatie.

## Screenshots (`docs/Screenshots/`)

`hospital_lobby_staff_station.png` (de post) en `hospital_guide_lights_floor1.png` (1A groen, 1B neutraal, 1C paars),
naast de bestaande screenshots van de lobby, de gang en de kamers.
