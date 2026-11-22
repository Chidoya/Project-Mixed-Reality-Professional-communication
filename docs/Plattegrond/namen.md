# U-afdeling: namen voor Jalil

Welke objecten Jalils `ShiftHospitalSceneBuilder` (branch `Jalil`) opzoekt in `liam_test_scene`, en wat er in
`Assets/Scenes/liam_u_afdeling.unity` op die plek staat. Alles onder de root `Hospital`. Gebouwd door
**ProCommVR > Hospital > Build U-Afdeling** (`Assets/Hospital/Editor/UWard/`).

Enkel ter info: Jalils shift gebruikt nog altijd `liam_test_scene`. Overstappen is een latere, gezamenlijke beslissing.

## Bestaat met dezelfde naam

| Pad | In de U-afdeling |
| --- | --- |
| `FloorN/Room_<id>` | 15 kamers. Floor1 = linkerarm (1A, 1B, 1C, 1D), Floor2 = onderste rij (2A tot 2G), Floor3 = rechterarm (3A tot 3D). Oorsprong op de binnenkant van de gangmuur, z de kamer in |
| `Room_*/Anchor_Laying`, `Anchor_Sitting` | eerste bed en stoel. Gedeelde kamers ook `Anchor_Laying_2`, `_3` en `Anchor_Sitting_2`, `_3` |
| `Room_*/Furniture/Bed`, `Chair`, `Nightstand` | eerste bed. Volgende bedden: `Bed_2`, `Chair_2`, `Nightstand_2`, ... In gedeelde kamers ook `Furniture/Curtain` |
| `Room_*/Light` | één plafondlicht per kamer |
| `FloorN/Door_<id>` | dicht, draait met Jalils `OpenDoor` naar binnen open (houder heeft dezelfde oriëntatie als de kamer) |
| `FloorN/Card_<id>` | kaart naast de deur, met de case als die bestaat (1A, 2A, 3A), anders "Coming soon" |
| `FloorN/Light_3`, `Light_9`, ... | ganglichten om de 6 m (armen: 3, 9, 15; onderste gang: 3 tot 27) |
| `FloorN/GuideLights/Guide_<id>` | lichtstrook van de doorgang van de hal naar elke deur, met `FloorGuideLight` |
| `Lobby/Furniture/ReceptionDesk` | U-vormige balie, 5 x 2,4 m. Bezoekerskant naar de liften (+Z), personeel binnen in de U |
| `Lobby/StaffStation` + `Anchor_BadgeScan`, `Anchor_TabletDock`, `Anchor_Clipboard`, `Anchor_EndScreen` | zelfde props en ankers als in de testscene |
| `Lobby/Wall_North` | de muur achter de balie, met de doorgang naar de gang. Let op: ligt aan de **zuid**kant van de hal (-Z) |
| `Lobby/Light_1`, `Light_2` | lichten in de hal |
| `Characters/Patient_1A`, `_2A`, `_3A` | zelfde personages en poses als in de testscene (uit de PatientCase) |
| `Characters/Lobby_*` | zorgverleners als decor, allemaal in de hal (minstens 0,8 m van de muren), met `EditorPosePreview` |

## Nieuw

| Pad | Wat |
| --- | --- |
| `Floor2/NurseStation` | leeg object in de onderste gang, voor scenario 3 (forward = naar de muur) |
| `Lobby/XROriginStart` | startplek van de speler, 1,3 m voor de balie aan de bezoekerskant, kijkend naar de balie |
| `Teleport/TeleportArea_*` | dunne BoxCollider per vloer (hal, 3 gangen, briefing, gesprekskamer, rustruimte, 15 kamers), zonder XRI-component |
| `Core/BriefingRoom/Screen`, `Whiteboard` | briefingruimte met tafel en 8 stoelen (plan v8: y 12,6..19,6, `Door_Briefing_Hall` op y 14..15) |
| `Core/FamilyRoom` + `Light`, `Furniture/*`, `Painting`, `Sign_FamilyRoom` | gesprekskamer (plan v8, y 8,0..12,6) voor scenario 2: warm licht dat `Light` heet, zoals in de kamers. Deuren `Core/Door_FamilyRoom_Hall` en `Core/Door_FamilyRoom_Corridor` (gang rechts). Bord met pictogram aan de gangkant |
| `Core/Door_Stairwell`, `Lobby/Lift/Lift_1`, `Lift_2` | dicht, met collider en `LockedDoor` (reden voor de speler) |

## Anders of ontbreekt

| In de testscene | In de U-afdeling |
| --- | --- |
| `Lobby/Lift/LiftButton_L`, `_1`, `_2`, `_3` en `FloorN/Lift` | één verdieping: enkel `Lobby/Lift` met `LiftButton_Up` en `LiftButton_Down`. De liften gaan niet open |
| `Room_*/Wall_Back` | bestaat niet: de muren zijn gedeeld en staan onder `Structure`, `FloorN` en `Core` |
| `FloorN/Wall_South` (de gangmuur tegenover de deuren) | bestaat niet: tegenover de deuren staan de muren van de kern (`Core/Wall_West`, `Wall_East`, `Wall_South_*`) |
| vaste coördinaten (`FloorHeight` 3,5 m, `LiftCenterZ`, `LobbyBoardX`, `StationDeskSpot`, ...) | gelden niet: alles ligt op Y = 0, de maten staan in `UWardLayout.cs` en `maten.md` |

## Scenario 2 in de U-afdeling

Voor later, als Jalils shift overstapt naar `liam_u_afdeling`. Jalil hoeft nu niets te doen. Alles staat onder
`Hospital/Scenarios/Scenario2_AngryDaughter` (`Anchors/` = lege objecten met `ScenarioAnchor`, `Actors/` = wat zichtbaar is),
gebouwd door `UWardBuilder.Scenario2.cs`, met de waarden in `UWardLayout.Scenario2Anchors`. Plan: X = x - 21, Z = 14 - y.
"Kijkt naar" is de forward van het anker; voor een canvas van `CreateCanvas` (forward = leesrichting) gebruik je
`Quaternion.LookRotation(-anker.forward)`.

| Jalils `ShiftHospitalSceneBuilder` | In de U-afdeling | Plan (x, y, hoogte) |
| --- | --- | --- |
| `EncounterFloor` = 3, `Floor3` | `Floor3` (rechterarm) | |
| `DaughterSpot` (5, 0, 1,6), kijkt -X | `Anchors/Anchor_Daughter_Corridor`, kijkt noord (naar de deur van 3C en de speler) | 35,3 / 5,0 / 0 |
| `DaughterModel` = `Patient_Female` + `DressAsVisitor` | `Actors/Daughter`: `Visitor_Daughter`, pose Angry, CapsuleCollider (r 0,3, h 1,7) | |
| `AddBlocker` (capsule r 0,3) | `Actors/Blocker_Corridor`: onzichtbare BoxCollider over de hele gangbreedte. Uitzetten als ze naar de gesprekskamer loopt | y 5,0 |
| (speler) | `Anchors/Anchor_Player_Corridor`, kijkt zuid. Voorlopig: de plek van de speler wordt later beslist | 34,8 / 3,3 / 0 |
| `CountdownPosition` (3,6, 2,6, 1,5) | `Anchors/Anchor_Countdown` + `Actors/Countdown_Mount` (leeg donker paneel 0,7 x 0,26 m, voorkant naar het noorden) | 34,8 / 7,2 / 2,45 |
| `EncounterSignPosition` op `Wall_South` | `Anchors/Anchor_Sign`, tegen `Core/Wall_East`, kijkt oost (de gang in) | 33,7 / 6,0 / 1,6 |
| `ConsultationRoomLabel` = `3B` (lege patiëntkamer) | `Core/FamilyRoom` (gesprekskamer, plan v8). Niets te verbergen; `Card_3B` blijft. Bord: `Core/FamilyRoom/Sign_FamilyRoom` | x 25..33,6, y 8..12,6 |
| `Room_3B/Light` (`MoodRoomLight`) | `Core/FamilyRoom/Light` | |
| `Door_3B` (`OpenDoor`) | `Core/Door_FamilyRoom_Corridor` (draait de kamer in, zoals `OpenDoor` verwacht) | x 33,6, y 9,5..10,5 |
| Pad "To Consultation Room" (4 waypoints) | `Anchors/Path_ToFamilyRoom/WP_1`, `WP_2`, `WP_3` en daarna `Anchors/Anchor_Daughter_Room` | zie `UWardLayout` |
| plek van het gesprek in 3B | `Anchors/Anchor_Daughter_Room`, `Anchor_Player_Room` (kijken elkaar aan) | 29,5 / 10,6 en 31,2 / 10,2 |
| paneel met 4 antwoorden (`ScenarioPresenter`) | `Anchors/Anchor_Panel_Corridor` (kijkt noord), `Anchors/Anchor_Panel_Room` (kijkt naar de speler) | 34,3 / 4,2 / 1,45 en 30,4 / 9,4 / 1,45 |
| Mood Sound op de dochter | `Anchors/Anchor_Speaker_Corridor`, `Anchor_Speaker_Room` | 35,0 / 5,0 / 2,8 en 29,3 / 10,3 / 2,8 |
| `EncounterCorridorLights` = `Light_3`, `Light_9` | `Floor3/Light_3`, `Floor3/Light_9` (bestaan, op y 3 en 9) | |
| `GurneyStart` (17, 0, 2,15) + Sketchfab-bed | `Actors/Gurney` (INACTIEF): `Gurney.prefab` met laken, root midden op de vloer, forward = rijrichting (hoofdeinde voorop), met de portier (`Porter`, Idle) als kind aan de duwbeugel. Staat op `Anchors/Path_Gurney/Gurney_Start` | 29,0 / 20,6 / 0 |
| `GurneyEnd` (11,3, 0, 2,15) | `Anchors/Path_Gurney/Gurney_End`, kijkt noord; de voorkant stopt 0,29 m achter de collider van de dochter | 34,3 / 6,6 / 0 |
| `AddPath(..., "Gurney", 1f, true, end)` | waypoints in volgorde: `Gurney_Start`, `Gurney_Corner_1`, `_2`, `_3`, `Gurney_Turn`, `Gurney_End` (boog van 1 m om de hoek; `UWardLayout.GurneyRoute`). `hideUntilPlayed` mag blijven: Gurney staat al inactief | |

Kamer 3C (beeld 1): `Characters/Patient_3C` met `Body_Pyjama` (aan) en `Body_Gown` (uit), zittend op
`Floor3/Room_3C/Furniture/Bed/Anchor_BedEdge`; het schort ligt op `Room_3C/Props/Gown`. Let op: Jalils `HideMisplacedPatients`
verbergt een patiënt die meer dan 4,2 m de kamer in staat; de oorsprong van `Patient_3C` ligt op 4,1 m.

Alarm (beeld 7): `Floor3/AlarmLights/AlarmLight_3C`, `AlarmLight_Corridor`, `AlarmLight_FamilyRoom` en
`Core/FamilyRoom/AlarmLight`, elk met `AlarmLamp` (`SetOn(bool)`, `Blink(float)`) en een rood puntlicht dat in rust uit staat.
Jalil kan ze aanzetten bij `Mood.Alarm`; zijn rode ganglichten (`Light_3`, `Light_9`) blijven daarnaast bestaan.
Testen zonder Jalils code: **ProCommVR > Hospital > Test Alarm > Aan / Uit / Knipperen** (in Play mode).
