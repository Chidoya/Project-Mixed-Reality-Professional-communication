# U-afdeling: maten

Bron van de maten voor `Assets/Hospital/Editor/UWard/UWardLayout.cs` (scene `Assets/Scenes/liam_u_afdeling.unity`).
Waar dit bestand niets zegt (bv. de plaats van de deuren van de kern), staat de gekozen waarde in `UWardLayout.cs`.

Bewuste afwijking: de scheiding tussen toilet man en toilet vrouw ligt op y 4,6 (niet 4,0). Zo past de deur van het
mannentoilet (y 3,5..4,3) in een vlakke muur, 45 cm vrij van de voormuur van de liften. De deur van het vrouwentoilet
ligt op y 6,65..7,45.

```text
U-AFDELING, versie 7. Eenheid: meter. 1 Unity-unit = 1 m. Plafond 3,0 m.
Plancoördinaten: x van links naar rechts (0..42), y van boven naar onder (0..28).
Omrekenen naar Unity: X = x - 21, Z = 14 - y (de bovenkant van het plan is +Z).
Buitenmuren 0,25 m dik, binnenmuren 0,16 m. Vloer op Y = 0.

GANGEN (2,4 m breed)
- Gang links:   x 6,0..8,4   y 0..22,0
- Gang rechts:  x 33,6..36,0 y 0..22,0
- Gang onder:   x 6,0..36,0  y 19,6..22,0
- Aan de bovenkant lopen de linker- en de rechtergang dood, met een raam in de buitenmuur.

PATIËNTKAMERS (6,0 m diep, raam in de buitenmuur, badkamer 1,95 x 2,2 m aan de gangkant)
Zone Floor1, linkerarm (x 0..6, deur in de muur x = 6), van boven naar onder:
- Room_1C  gedeeld, 2 bedden   y 0,00..7,20   deur y 2,60..3,90
- Room_1A  eenpersoons         y 7,20..12,13  deur y 10,58..11,88
- Room_1B  eenpersoons         y 12,13..17,07 deur y 15,52..16,82
- Room_1D  eenpersoons         y 17,07..22,00 deur y 20,45..21,75
Zone Floor3, rechterarm (x 36..42, deur in de muur x = 36), van boven naar onder:
- Room_3C  gedeeld, 2 bedden   y 0,00..7,20   deur y 2,60..3,90
- Room_3A  eenpersoons         y 7,20..12,13  deur y 10,58..11,88
- Room_3B  eenpersoons         y 12,13..17,07 deur y 15,52..16,82
- Room_3D  eenpersoons         y 17,07..22,00 deur y 20,45..21,75
Zone Floor2, onderste rij (y 22..28, deur in de muur y = 22), van links naar rechts:
- Room_2E  hoekkamer, gedeeld, 3 bedden  x 0,0..8,4    deur x 6,55..7,85 (komt uit in de hoek van de gang)
- Room_2A  eenpersoons                   x 8,4..12,9   deur x 11,35..12,65
- Room_2B  eenpersoons                   x 12,9..17,4  deur x 15,85..17,15
- Room_2G  gedeeld, 2 bedden             x 17,4..24,6  deur x 20,00..21,30
- Room_2C  eenpersoons                   x 24,6..29,1  deur x 27,55..28,85
- Room_2D  eenpersoons                   x 29,1..33,6  deur x 32,05..33,35
- Room_2F  hoekkamer, gedeeld, 3 bedden  x 33,6..42,0  deur x 34,15..35,45 (gespiegeld aan 2E)
Indeling van een kamer: de badkamer (toilet, lavabo, douche 0,9 x 0,9) zit tegen de gangmuur, in de
hoek aan het begin van de kamer (bij de kleinste y in de armen, bij de kleinste x in de onderste rij;
gespiegeld in 2F). Kijk in de svg voor de exacte plaats. De deur is 1,3 m breed (bed past erdoor). De bedden staan met het hoofdeinde tegen
de buitenmuur, met een nachtkastje en een stoel. In gedeelde kamers hangt een privacygordijn tussen de bedden.

KERN (midden, tegen de bovenmuur, x 8,4..33,6, y 0..19,6)
- Trappenhuis       x 8,4..17,0  y 0..7,0     NIET toegankelijk: deur dicht, zie fase 4
- Rustruimte        x 8,4..17,0  y 7,0..13,3  klein: zetel, fauteuil, salontafel, koffiehoek. Deur naar de hal
- Medicatiekamer    x 8,4..17,0  y 13,3..19,6 kasten in L-vorm, werktafel. Deur naar de hal
- Hal               x 17,0..25,0 y 0..19,6    aan de onderkant 6 m open naar de gang onder
    - 2 liften bovenaan: x 17,6..20,8 en 21,1..24,3, y 0,25..2,85. NIET toegankelijk
    - Balie (U-vorm, 5 x 2,4 m) op x 18,5..23,5, y 15,6..18,0, open kant naar de gang
    - wachtstoelen langs de wanden onder de liften
- Toilet man        x 25,0..33,6 y 0..4,0     deur naar de hal
- Toilet vrouw      x 25,0..33,6 y 4,0..8,0   deur naar de hal
- Briefingruimte    x 25,0..33,6 y 8,0..19,6  tafel 4,6 x 1,8 m met 8 stoelen, scherm aan de rechtermuur.
                    Deuren naar de hal (y 9..10) en naar de gang onder (x 30,5..31,5)
```
