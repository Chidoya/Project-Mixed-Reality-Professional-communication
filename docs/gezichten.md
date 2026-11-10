# Gezichten met uitdrukkingen

Alle 18 personages in `Assets/Character/` hebben een low-poly gezicht (ogen met lichtpuntje,
wenkbrauwen, neusje, mond met tanden en tranen) met 14 blendshapes. Het gezicht volgt vanzelf de
Animator-state, knippert, kijkt naar de speler en huilt met echte druppels.

## Keten

1. Blender: `blender/scripts/07_gezicht.py` (na 03 en 05) maakt het gezicht en de shape keys en
   exporteert met `--save --export` naar `blender/export/unity/<Naam>/<Naam>.fbx`.
   Het palet staat in `blender/export/unity/_Face/FacePalette.png`.
2. Kopieer de FBX'en naar `Assets/Character/<Naam>/` en het palet naar `Assets/Character/Face/`.
3. Unity: `ProCommVR > Characters > Setup` (of `CharacterSetup.RunBatch`). Dit importeert de
   blendshapes (normals None, zodat de flat look blijft), zet alle `<Naam>_Face` materialen op het
   gedeelde `Assets/Character/Face/Face.mat` en zet de componenten op elke prefab. Een bestaande prefab
   wordt nu bijgewerkt in plaats van opnieuw gemaakt, zodat toegevoegde componenten blijven bestaan.

## Blendshapes

`Blink`, `EyesWide`, `EyesNarrow`, `LookDown`, `BrowsAngry`, `BrowsSad`, `BrowsRaise`, `Smile`,
`Frown`, `MouthOpen`, `MouthTight`, `Tears`, `EyesSqueeze` (ogen als "> <") en `Grimace` (brede mond met geklemde tanden). De gewichten per uitdrukking staan in
`Assets/Hospital/Data/FacePresets.asset` (zelfde tabel als `EXPRESSIONS` in 07_gezicht.py).

## Componenten (`Assets/Hospital/Scripts/Face/`, namespace `ProCommVR.Hospital.Face`)

| Component | Wat |
| --- | --- |
| `FaceController` | `SetExpression(FaceExpression, intensity, blendTime)`, `SetExpression(string)` voor UnityEvents, `Intensity`, `TalkLevel`, `Blink()`. Mengt vloeiend, knippert vanzelf (2,5 tot 5,5 s, 15 % dubbel, minder bij Angry, niet bij Crying en Pain), wenkbrauwen bewegen licht (`MicroMotion`). Schrijft iets anders (bv. `MouthMover`) `MouthOpen`, dan telt dat als praten. |
| `FaceFromAnimator` | Angry, Sad en Crying in de Animator geven dat gezicht, andere states de rustuitdrukking. Een `SetExpression` van buitenaf wint 5 s of tot de state wijzigt. |
| `HeadLookAt` | Hoofd en nek naar `Camera.main`: max 60 graden opzij, 25 op/neer, binnen 3 m, 30 graden bij Laying. `Mode = AvoidGaze` kijkt weg en naar beneden. |
| `TearDrops` | Bij `Tears` boven 70 rollen druppels over het traanspoor en vallen van het gezicht. |
| `FaceTestDriver` | Testscene: 1-8 uitdrukking, B knipperen, L kijken aan/uit, A state Angry, I Idle. |

`HeadLookAt` en `TearDrops` bewaren vooraf berekende gegevens (kijkrichting en traanpad in de ruimte
van het Head-bot). Die berekent `CharacterSetup`, want de mesh is in een build niet leesbaar.
Na een nieuwe FBX dus altijd Setup opnieuw draaien.

Voor Jalil (optioneel): `GetComponentInChildren<FaceController>()?.SetExpression(FaceExpression.Angry)`
per dialoogregel, en `HeadLookAt.Mode = AvoidGaze` bij TurnsAway/Withdrawn.

## Testen

- Scene `Assets/Scenes/liam_face_test.unity` (bouwen met `ProCommVR > Characters > Build Face Test Scene`).
- Beelden en video-frames opnemen:
  `Unity -batchmode -projectPath . -executeMethod ProCommVR.Editor.FaceCapture.RunBatch -faceOut <map> -screenshotOut docs/Screenshots`.
  Dat schrijft per uitdrukking een raster van alle personages, een blad met de 8 uitdrukkingen van
  Visitor_Daughter en de frames van twee video's (`frames_dochter`, `frames_alle`).
- Screenshots: `docs/Screenshots/faces_neutral.png` en `faces_angry.png`.
