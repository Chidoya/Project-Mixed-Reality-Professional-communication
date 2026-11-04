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
| `Teleport/TeleportArea_*` | dunne BoxCollider per vloer (hal, 3 gangen, briefing, rustruimte, 15 kamers), zonder XRI-component |
| `Core/BriefingRoom/Screen`, `Whiteboard` | briefingruimte met tafel en 8 stoelen |
| `Core/Door_Stairwell`, `Lobby/Lift/Lift_1`, `Lift_2` | dicht, met collider en `LockedDoor` (reden voor de speler) |

## Anders of ontbreekt

| In de testscene | In de U-afdeling |
| --- | --- |
| `Lobby/Lift/LiftButton_L`, `_1`, `_2`, `_3` en `FloorN/Lift` | één verdieping: enkel `Lobby/Lift` met `LiftButton_Up` en `LiftButton_Down`. De liften gaan niet open |
| `Room_*/Wall_Back` | bestaat niet: de muren zijn gedeeld en staan onder `Structure`, `FloorN` en `Core` |
| `FloorN/Wall_South` (de gangmuur tegenover de deuren) | bestaat niet: tegenover de deuren staan de muren van de kern (`Core/Wall_West`, `Wall_East`, `Wall_South_*`) |
| vaste coördinaten (`FloorHeight` 3,5 m, `LiftCenterZ`, `LobbyBoardX`, `StationDeskSpot`, ...) | gelden niet: alles ligt op Y = 0, de maten staan in `UWardLayout.cs` en `maten.md` |
