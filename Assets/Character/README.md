# Assets/Character (Liam)

18 low-poly personages uit Blender (`blender/scripts/02` tot `05`), elk in `Assets/Character/<Naam>/`:
FBX met een Humanoid-rig (25 botten, Mixamo-naamgeving), URP Lit materialen, een Animator Controller met de
7 states (Idle, Talking, Sitting, Laying, Sad, Angry, Crying) en een prefab `<Naam>.prefab`.
Klaarzetten: **ProCommVR > Characters > Setup** (`Editor/CharacterSetup.cs`). De animaties staan in `Animations/`.

| Groep | Personages |
| --- | --- |
| Patiënten | `Patient_Female_Old`, `Patient_Female`, `Patient_Male`, `Patient_Male_Old`, `Patient_Girl`, `Patient_Boy` |
| Zorgverleners | `Doctor_Female`, `Doctor_Male`, `Nurse_Female_Scrubs`, `Nurse_Female_White`, `Nurse_Male_Scrubs`, `Nurse_Male_White`, `Surgeon_Female`, `Surgeon_Male`, `Porter` |
| Bezoekers (familie) | `Visitor_Daughter`, `Visitor_Son`, `Visitor_Wife` |

## Nieuw: bezoekers en portier

Gemaakt voor de scenario's, met dezelfde basislichamen, stijl en regels als de andere 14 (onder 700 driehoeken).
Ze staan nog in geen enkele scene.

| Personage | Uiterlijk | Lengte | Driehoeken | Voor |
| --- | --- | --- | --- | --- |
| `Visitor_Daughter` | vrouw ca. 35, jeans, rood vest over een crème hemd, witte sneakers, bruine handtas aan de linkeronderarm | 1,68 m | 546 | de dochter (Scenario 2) |
| `Visitor_Son` | man ca. 40, chino, groene trui | 1,82 m | 470 | de zoon (Scenario 3) |
| `Visitor_Wife` | oudere vrouw, pruimkleurige rok, lila blouse, blauw gilet, zilvergrijs haar | 1,62 m | 606 | de vrouw (Scenario 4) |
| `Porter` | man, donkerblauw uniform met lange broek, naamkaartje | 1,80 m | 482 | portier (bv. bij de Gurney) |

**Voor Jalil:** de herkleurde kopieën van patiënten in bezoekerskleren (`Assets/_Project/Materials/Characters/`)
kunnen vervangen worden door deze prefabs. Ze hebben dezelfde controller-states, dus `CharacterPose`, `NpcReactor`
en de Animator-states blijven werken.

Bekend: de handtas van `Visitor_Daughter` zit vast aan de onderarm. In een pose met hangende armen (Idle) staat ze
schuin tegen het dijbeen; zittend ligt ze op de schoot.

Enkel bepaalde personages opnieuw klaarzetten (de andere blijven onaangeroerd):
`Unity -batchmode -executeMethod ProCommVR.Editor.CharacterSetup.RunBatch -characters Visitor_Daughter,Porter`.
In Blender: `blender -b -P scripts/03_variants.py -- --only <namen>` en daarna hetzelfde met `05_rig.py`.
