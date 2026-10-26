# Asset check (gegenereerd)

Gegenereerd door `ProCommVR > Hospital > Prepare Assets` (`Assets/Hospital/Editor/HospitalAssetPrep.cs`).
Texture-limieten: 1024 (grote vlakken, bed), 512 (props), 256 (niet gebruikte maps). Android: ASTC_6x6.
Geheugen = schatting voor Android (ASTC 6x6 met mipmaps), enkel texturen die in een materiaal zitten.

## Per asset

| Asset | Modellen | Driehoeken | Materialen | Texturen in gebruik | Grootte na import | Geheugen (Android) | Afmetingen (m) | Schaal |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Hospital Room (UgurYksk) | 1 | 25,105 | 30 | 30 | 22x 512x512, 8x 1024x1024 | 8.0 MB | 0.94 x 0.49 x 3.70 | 1 (vaste schaal) |
| GRADD Hospital Room | 1 | 61,182 | 69 | 46 | 7x 512x512, 4x 512x256, 1x 32x32, 24x 256x32, 9x 256x256, 1x 128x128 | 1.8 MB | 5.21 x 2.75 x 5.21 | 0.0254 (vaste schaal) |
| Hospital Bed (ansh_artis) | 1 | 28,279 | 11 | 0 | geen | 0.0 MB | 2.10 x 2.08 x 1.02 | 0.4342 (BED ASSEST_2 was 4.837 eenheden, geschaald naar 2.1 m) |
| Hospital Door (anasrar) | 1 | 740 | 4 | 0 | geen | 0.0 MB | 1.22 x 2.10 x 0.32 | 0.1707 (untitled was 12.3 eenheden, geschaald naar 2.1 m) |
| Kenney Furniture Kit | 140 | 26,737 totaal, gem. 190/model | 15 | 0 | geen | 0.0 MB | 140 modellen | 0.1778 (bedSingle was 11.25 eenheden, geschaald naar 2 m) |

## In de testscene

Scene: `Assets/Scenes/liam_asset_check.unity`. Screenshots: `docs/Screenshots/asset_check.png` en `docs/Screenshots/asset_check_closeup.png`.

| Object | Driehoeken | Materialen | Afmetingen (m, b x h x d) |
| --- | --- | --- | --- |
| Hospital Room (UgurYksk): HospitalScene | 25,105 | 30 | 0.94 x 0.49 x 3.70 |
| GRADD Hospital Room: Medical-Hospital-Equipment-001 | 61,182 | 69 | 5.21 x 2.75 x 5.21 |
| Hospital Bed (ansh_artis): BED ASSEST_2 | 28,279 | 11 | 2.10 x 2.08 x 1.02 |
| Hospital Door (anasrar): untitled | 740 | 4 | 1.22 x 2.10 x 0.32 |
| Kenney Furniture Kit: bedSingle | 214 | 4 | 1.02 x 0.67 x 2.00 |
| Kenney Furniture Kit: cabinetBed | 72 | 2 | 0.47 x 0.41 x 0.38 |
| Kenney Furniture Kit: chair | 170 | 1 | 0.36 x 0.84 x 0.36 |
| Kenney Furniture Kit: televisionModern | 72 | 2 | 1.22 x 0.81 x 0.23 |
| Kenney Furniture Kit: wallWindow | 108 | 4 | 1.78 x 2.29 x 0.16 |
| Kenney Furniture Kit: wallDoorway | 60 | 3 | 1.78 x 2.29 x 0.16 |
| Referentie: Patient_Male | 470 | 5 | 1.45 x 1.93 x 0.32 |
| Referentie: Nurse_Female_Scrubs | 470 | 5 | 1.27 x 1.78 x 0.29 |

## Materialen van de deur

- `Frame`: deur (lichtgrijs)
- `Door`: deur (lichtgrijs)
- `Glasses`: deur (lichtgrijs)
- `Handle`: klink (donkergrijs)
