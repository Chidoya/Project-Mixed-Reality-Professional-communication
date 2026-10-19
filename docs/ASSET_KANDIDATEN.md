# Asset kandidaten (prototype ziekenhuis)

Status: **categorie 1 beoordeeld (zie "Besluit Liam"), categorie 2 tot 5 wachten nog op goedkeuring.**
Gedownload op 2026-10-19 (nog niet gecommit, nog niet in Unity geopend):
- Kenney Furniture Kit: enkel de FBX-versie (141 modellen) + `License.txt`, in `Assets/ThirdParty/Kenney/FurnitureKit/` (5 MB).
- GRADD Hospital Room: OBJ + MTL + 47 JPG-texturen + `LICENSE.txt`, in `Assets/ThirdParty/GRADD/HospitalRoom/` (9,3 MB).
  Let op: 61.151 triangles volgens Poly Pizza, zwaar voor Quest. Enkel de nodige stukken gebruiken.
- Credits staan in `CREDITS.md` in de root.

- Sketchfab (door Liam gedownload, door Claude uitgepakt), elk met `LICENSE.txt`:
  - Hospital Room (UgurYksk): `HospitalScene.dae` + texturen, in `Assets/ThirdParty/Sketchfab/HospitalRoom_UgurYksk/` (28 MB).
    Originele download was DAE (Collada), Unity importeert dat zonder extra package.
  - Hospital Bed (ansh_artis): FBX + 3 PNG-texturen, in `Assets/ThirdParty/Sketchfab/HospitalBed_anshartis/` (2,9 MB).
  - Hospital Door (anasrar): FBX zonder texturen, in `Assets/ThirdParty/Sketchfab/HospitalDoor_anasrar/` (33 KB).

Fase 1 (interieur) is daarmee volledig binnen. Lift bouwen we zelf.

Opgesteld: 2026-10-19. Context: `GAME_BRIEF.md` (branch `jalil`): ziekenhuis met 3 vloeren (gang met deuren),
lift of trap, patiëntenkamer van ongeveer 4 x 4 m. Doelplatform Meta Quest 3, dus low-poly, weinig materialen,
baked lighting.

## Legende

- ⭐ = aanrader in die categorie.
- **Naamsvermelding**: CC0 en MIT vragen geen credit (MIT: licentietekst meeleveren). CC-BY vraagt een credit
  (auteur, titel, link, licentie) in de app of documentatie. Voorstel: een `CREDITS.md` bijhouden.
- **Polygonen**: "faces" zoals Sketchfab ze toont, anders een schatting (aangeduid met ~).
  Richtwaarde voor Quest 3: hele scène onder ~100k tot 200k triangles, een personage onder ~15k.
- **Grootte**: bij Sketchfab is de downloadgrootte pas zichtbaar na inloggen, daar staat "na login".
- **Unity 6 URP**: FBX/OBJ werkt standaard. glTF/GLB vraagt de package `glTFast` (nieuwe dependency, eerst akkoord nodig),
  dus bij Sketchfab en Poly Pizza kiezen we liefst de FBX-download. Materialen van externe modellen moeten
  meestal omgezet worden naar `URP/Lit` (Render Pipeline Converter of handmatig).
- Niet geverifieerd = de pagina gaf het niet prijs, moet gecontroleerd worden bij download.

## 1. Ziekenhuisinterieur

Er bestaat geen gratis CC0 "hospital kit" van Kenney of Quaternius. Alle gratis hospital-packs op de
Unity Asset Store die ik vond, zijn betalend (ithappy €91, Mixall €27, 3D Everything €23, Kiberfank $4).
Voorstel: modulaire basis uit een CC0-kit + losse ziekenhuisstukken van Sketchfab.

### Besluit Liam (2026-10-19)

| Asset | Besluit | Opmerking |
| --- | --- | --- |
| Kenney Furniture Kit | ✅ Goedgekeurd | Ook de bedden uit deze kit gebruiken (algemene bedden) |
| Quaternius Ultimate Furniture Pack | ❌ Afgekeurd | Kenney volstaat |
| Hospital Bed (larry3d) | ❌ Afgekeurd | Kenney-bedden volstaan |
| Hospital Bed (ansh_artis) | ✅ Goedgekeurd | Bed voor de patiënten |
| Hospital Room (UgurYksk) | ✅ Goedgekeurd, eerste keuze | Basis voor de patiëntenkamer, kamers hier rond bouwen |
| GRADD Hospital Room (GRADD CO) | ✅ Goedgekeurd, tweede keuze | Ook importeren |
| Hospital Door (anasrar) | ✅ Goedgekeurd | Combineren met GRADD Hospital Room |
| (PSX) Hospital Doors Pack 2 (Kasujin) | ❌ Afgekeurd | |
| LIFT MODEL (projektsansstudios) | ❌ Afgekeurd | |
| Elevator - Low Poly Animated (jeffkolada) | ❌ Afgekeurd | |
| Cartoon Elevator (Jorgeart.Games) | ❌ Afgekeurd | |
| ambientCG texturen | ❌ Afgekeurd | |

Lift: geen asset nodig, die bouwen we zelf.

| | Naam | Categorie | Link | Licentie | Naamsvermelding | Formaat | Grootte | Polygonen | Unity 6 URP | Gebruik |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ⭐ | Kenney Furniture Kit (140 modellen) | Interieur: muren, vloer, deuropening, raam, bed, kast, stoel, tv, plant | https://kenney.nl/assets/furniture-kit | CC0 | Nee | FBX, OBJ, GLB | ~5 tot 10 MB (niet geverifieerd) | ~50 tot 1.000 per model | Ja | Modulaire muren/vloer voor gang en kamer, stoel, nachtkastje, raam. Inhoud per model nog te checken in de zip |
| | Quaternius Ultimate Furniture Pack (20 modellen) | Interieur: bedden, stoelen, tafels | https://quaternius.com/packs/ultimatefurniture.html | CC0 | Nee | FBX, OBJ, Blend | niet vermeld | ~200 tot 2.000 per model | Ja | Alternatief/aanvulling voor stoel en nachtkastje (ook losse "Night Stand" op Poly Pizza) |
| ⭐ | Hospital Bed (larry3d) | Ziekenhuisbed | https://sketchfab.com/3d-models/hospital-bed-04eec268506d45a192f70aea01b8d292 | CC-BY 4.0 | Ja | Sketchfab download (FBX/GLB/OBJ) | na login | 9.724 faces | Ja (materiaal omzetten) | Bed van de patiënt. Let op: gemaakt "voor een horror-scène", textuur mogelijk vuil/roestig, eerst preview bekijken |
| | Hospital Bed (ansh_artis) | Ziekenhuisbed | https://sketchfab.com/3d-models/hospital-bed-0e974ea6eb9a4c069c56f152ae5162a4 | CC-BY 4.0 | Ja | Sketchfab download | na login | 28.279 faces | Ja (materiaal omzetten) | Reserve als het bed van larry3d te "horror" oogt. Zwaarder |
| | Hospital Room (UgurYksk) | Volledige kamer incl. bed | https://sketchfab.com/3d-models/hospital-room-39d5f1555a26402ab1a54ffbaa8afec1 | CC-BY 4.0 | Ja | Sketchfab download | na login | 25.105 faces | Ja (materiaal omzetten) | Snelle kant-en-klare patiëntenkamer, of bron om bed/kast uit te halen |
| | GRADD Hospital Room (GRADD CO) | Volledige kamer | https://poly.pizza/m/9sUalfQ76kn | CC-BY 3.0 | Ja | OBJ, glTF | niet vermeld | niet vermeld | Ja (OBJ), glTF vraagt glTFast | Alternatieve kant-en-klare kamer, oude Google Poly-stijl |
| ⭐ | Hospital Door (anasrar) | Deur | https://sketchfab.com/3d-models/hospital-door-46e99d254ff34f45ad8ae7cb40321406 | CC-BY 4.0 | Ja | Sketchfab download | na login | 740 faces | Ja | Kamerdeuren in de gangen (herhaalbaar, zeer licht) |
| | (PSX) Hospital Doors Pack 2 (Kasujin) | Deuren | https://sketchfab.com/3d-models/psx-hospital-doors-pack-2-386de28254734d4c9143cf0cecdb7138 | CC-BY 4.0 | Ja | Sketchfab download | na login | 72 faces | Ja | Extreem licht, maar PSX-stijl (lage-resolutie textuur), past mogelijk niet bij de rest |
| ⭐ | LIFT MODEL (projektsansstudios) | Lift | https://sketchfab.com/3d-models/lift-model-520ec3454fe84cd586e434b287a44e05 | CC-BY 4.0 | Ja | Sketchfab download | na login | 2.294 faces, 2 animaties | Ja (animaties via FBX) | Lift tussen de 3 vloeren, deuren open/dicht geanimeerd |
| | Elevator - Low Poly Animated (jeffkolada) | Lift | https://sketchfab.com/3d-models/elevator-low-poly-animated-3a9cc99aeb284a4080c03374277231ae | CC-BY 4.0 | Ja | Sketchfab download | na login | 266 faces, animaties idle/open | Ja | Ultralichte liftcabine met gebakken licht, één UV |
| | Cartoon Elevator (Jorgeart.Games) | Lift | https://sketchfab.com/3d-models/cartoon-elevator-b4eb470f749b42dd902826be9c02e37d | CC-BY 4.0 | Ja | Sketchfab download | na login | 3.860 faces | Ja | Reserve, cartoonstijl, geen animatie |
| | ambientCG texturen (tegels, pleister, linoleum) | Materialen muur/vloer | https://ambientcg.com/list?q=tiles | CC0 | Nee | JPG/PNG (1K aanbevolen) | ~2 tot 8 MB per set op 1K | n.v.t. | Ja | Realistischere vloer- en muurmaterialen op de Kenney-geometrie. Concrete sets nog te kiezen |

Trap: als we geen lift willen, zit er een trapmodel in de Kenney-kits (bv. "Stairs Center" op https://poly.pizza/m/HXfmM6RYeJ, CC0).

## 2. Personages

| | Naam | Categorie | Link | Licentie | Naamsvermelding | Formaat | Grootte | Polygonen | Unity 6 URP | Gebruik |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ⭐ | Microsoft Rocketbox Avatar Library (115 avatars + 417 animaties) | Patiënt, familielid, verpleging | https://github.com/microsoft/Microsoft-Rocketbox | MIT | Nee (licentietekst meeleveren) | FBX + PNG texturen | per avatar ~3 tot 10 MB (FBX 0,6 MB + texturen), repo in totaal ~4 GB: enkel de nodige mappen downloaden | 4 LOD-niveaus (hipoly tot ultralowpoly), ~5k tot 15k voor de lagere LODs (te meten) | Ja, met hun import-script; materialen omzetten naar URP | Realistische mensen. Bevat o.a. `Medical_Female_01..03`, `Medical_Male_01..05` en animaties `sit_chair_idle_*`, `idle_angry_01`, `gestic_talk_angry_01`, `gestic_talk_sad_01`, `gestic_listen_sad_01`. Perfect voor de somber zittende patiënt (scenario 1) en de boze dochter (scenario 2). Geen lig-animatie gevonden |
| | Quaternius Universal Base Characters + Universal Animation Library | Generieke personages + 120+ animaties | https://quaternius.com/packs/universalbasecharacters.html en https://quaternius.com/packs/universalanimationlibrary.html | CC0 | Nee | FBX, glTF, OBJ | niet vermeld | ~13k tris per personage | Ja (Humanoid rig, retargetbaar) | Gestileerde low-poly mensen, zitten zit erin. Minder realistisch, minder emotie in gezicht/houding dan Rocketbox |
| | Mixamo (Adobe) | Personages + animaties | https://www.mixamo.com | Gratis met Adobe-account, royalty-free gebruik in projecten (geen CC, geen herverdeling van losse bestanden) | Nee | FBX | ~1 tot 5 MB per anim/personage | ~10k tot 30k per personage | Ja (Humanoid) | Aanvulling: extra animaties zoals "Sitting Idle", "Sad Idle", "Angry" op een Rocketbox- of Quaternius-rig retargeten |

## 3. Kleine props

| | Naam | Categorie | Link | Licentie | Naamsvermelding | Formaat | Grootte | Polygonen | Unity 6 URP | Gebruik |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ⭐ | IV Stand (teeart) | Infuuspaal | https://sketchfab.com/3d-models/iv-stand-dd3115aafb0e4a628f261d0de7a787b9 | CC-BY 4.0 | Ja | Sketchfab download | na login | 5.084 faces | Ja | Infuuspaal naast het bed |
| | Crutch and IV Drip (Matt_LeMoine) | Infuuspaal + kruk | https://sketchfab.com/3d-models/crutch-and-iv-drip-5cc65c6aed374220b67f7d60e679153e | CC-BY 4.0 | Ja | Sketchfab download | na login | 5.644 faces (beide samen) | Ja | Alternatief, krukken als extra sfeerprop |
| | Doctor's Office - Assets (EvanPetrov) | Diverse medische props | https://sketchfab.com/3d-models/doctors-office-assets-3facd48dff0945f7b96f95ad31c4beb5 | CC-BY 4.0 | Ja | Sketchfab download | na login | 32.212 faces (hele set) | Ja | Set met o.a. infuus, kan ook andere kleine props leveren. Beschrijving bevat een macabere grap, inhoud eerst bekijken |
| ⭐ | Kenney "Sign Hospital" + eigen bordje | Bord naast deur | https://poly.pizza/m/PeM20xAT0V | CC0 | Nee | FBX, OBJ, glTF | < 1 MB | ~100 (schatting) | Ja | Ziekenhuisbord. Kamernummer-kaartjes naast deuren maken we zelf als quad + TextMeshPro (0 assets nodig) |
| ⭐ | Flower Vase (Glen Faught) | Bloemen | https://poly.pizza/m/69MP7keI4UF | Niet geverifieerd (Poly Pizza: CC0 of CC-BY) | Te checken | FBX, OBJ, glTF | < 1 MB | ~500 tot 2.000 (schatting) | Ja | Bloemen op het nachtkastje |
| | Quaternius Flowers / Houseplant | Bloemen, plant | https://poly.pizza/m/NBUxHir6FJ | CC0 | Nee | FBX, OBJ, glTF | < 1 MB | ~200 tot 1.000 (schatting) | Ja | Alternatief, gestileerd |
| ⭐ | Tv uit Kenney Furniture Kit (`televisionModern` / "Cabinet Television") | Tv | https://kenney.nl/assets/furniture-kit (los: https://poly.pizza/m/AL6wwiUgP3) | CC0 | Nee | FBX, OBJ, GLB | zit in de kit | ~100 tot 300 | Ja | Wandtv in de kamer |
| | Flat-screen TV (J-Toastie) | Tv | https://poly.pizza/m/KmT5q0N2AH | Niet geverifieerd | Te checken | FBX, OBJ, glTF | < 1 MB | niet vermeld | Ja | Alternatief |

## 4. Geluid

Alle onderstaande Freesound-bestanden zijn CC0 (pagina zelf gecontroleerd), behalve waar vermeld.
Na download omzetten naar mono OGG/Vorbis in Unity (import-instelling) om ruimte te sparen.

| | Naam | Categorie | Link | Licentie | Naamsvermelding | Formaat | Grootte | Polygonen | Unity 6 URP | Gebruik |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ⭐ | kwun tong hospital ambience9 (martian) | Ambient | https://freesound.org/people/martian/sounds/557013/ | CC0 | Nee | WAV 48 kHz stereo, 2:02 | 22,3 MB | n.v.t. | Ja | Achtergrond wachtzaal/gang, loopbaar |
| | Hospital reception, seating area ambience (misperpodcast) | Ambient | https://freesound.org/people/misperpodcast/sounds/733585/ | CC0 | Nee | WAV 48 kHz stereo, 3:30 | 57,8 MB | n.v.t. | Ja | Alternatief, bevat café- en toiletgeluiden (inkorten) |
| | Suburban hospital ambience (gladkiy) | Ambient | https://freesound.org/people/gladkiy/sounds/348110/ | CC0 | Nee | WAV | niet gecontroleerd | n.v.t. | Ja | Alternatief met pratende mensen en een deur |
| ⭐ | hospital-door-latch (alienistcog) | Deur | https://freesound.org/people/alienistcog/sounds/125297/ | CC0 | Nee | AIFF, 14,5 s | 2,4 MB | n.v.t. | Ja | Deurklink/openen van een echte ziekenhuisdeur, knippen in open en dicht |
| | Kenney RPG Audio (50 geluiden) | Deur + diverse | https://kenney.nl/assets/rpg-audio | CC0 | Nee | OGG | ~1 MB (schatting) | n.v.t. | Ja | Bevat volgens mij `doorOpen` / `doorClose` varianten (nog te verifiëren in de zip) |
| | Hospital Sliding Door Open & Close (Sagnik_Basu) | Deur | https://freesound.org/people/Sagnik_Basu/sounds/398870/ | CC0 (zoekfilter) | Nee | niet gecontroleerd | niet gecontroleerd | n.v.t. | Ja | Schuifdeur, eventueel voor de lift |
| ⭐ | Elevator_DingDong (uniuniversal) | Liftbel | https://freesound.org/people/uniuniversal/sounds/634327/ | CC0 | Nee | WAV 24-bit 44,1 kHz, 5,7 s | 1,4 MB | n.v.t. | Ja | Liftbel bij aankomst |
| | elevator_ding (bsumusictech) | Liftbel + deur | https://freesound.org/people/bsumusictech/sounds/62611/ | CC0 | Nee | WAV, 54 s | 9,1 MB | n.v.t. | Ja | Ding zit pas rond 40 s, knippen nodig |

## 5. UI-iconen (vinkje, slot, moeilijkheid)

| | Naam | Categorie | Link | Licentie | Naamsvermelding | Formaat | Grootte | Polygonen | Unity 6 URP | Gebruik |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ⭐ | Kenney Game Icons (105 iconen) | UI-iconen | https://kenney.nl/assets/game-icons | CC0 | Nee | PNG (+ vector, te verifiëren) | ~1 MB (schatting) | n.v.t. | Ja (Sprite / UI Image) | Vinkje, slot en ster voor moeilijkheid. Exacte iconen nog te checken in de zip |
| | Google Material Symbols | UI-iconen | https://fonts.google.com/icons | Apache 2.0 | Nee (licentie meeleveren) | SVG, PNG | per icoon < 10 KB | n.v.t. | Ja (PNG als Sprite) | `check_circle`, `lock`, `signal_cellular_alt` of `star` voor moeilijkheid. Strak en neutraal |
| | game-icons.net | UI-iconen | https://game-icons.net | CC-BY 3.0 | Ja | SVG, PNG | per icoon < 10 KB | n.v.t. | Ja | Grote keuze (o.a. `padlock`, `check-mark`), maar credit nodig per auteur |

## Openstaande punten voor goedkeuring

1. Stijlkeuze: realistisch (Rocketbox + Sketchfab-stukken) of gestileerd low-poly (Kenney + Quaternius)? Mengen kan,
   maar realistische mensen in een blokkige kamer valt op. Mijn voorstel: Kenney-geometrie met ambientCG-texturen
   + Rocketbox-personages.
2. CC-BY assets (Sketchfab) vragen een `CREDITS.md` en een creditscherm.
3. glTFast installeren of enkel FBX gebruiken? Voorstel: enkel FBX, geen extra package.
4. Waar komen third-party assets? Volgens `CLAUDE.md` buiten `Assets/_Project/`, bv. `Assets/ThirdParty/<bron>/`.
5. Git LFS staat nog niet aan. Grote WAV's en texturen best pas committen na die beslissing.
