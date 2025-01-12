-- Najważniejsze rzeczy:

Rivers/RiversGenerator.cs

na podstawie tekstury rzek tworzy idealna ścieżki źródło->ujście

Fields/FieldsGenerator.cs

1. tworzy środki pól z wierzchołków podzielonego dwudziestościanu foremnego (subdivided icosahedron)
2. tworzy pola ze środków, ograniczając się rzekami i regionami, algorytm dijkstry

Finalizer/FinalizerGenerator.cs

1. tworzy granice między polami, tj. listy pikseli granicznych między dwoma polami, sortuje je
2. tworzy dane rzek na podstawie tekstury rzek, tj. listy pikseli rzek + ich połączenia z innymi
3. sprawdza czy pola stykające się są sąsiadami (czy jest połączenie między środkami tych pól), jeśli tak oblicza dystans i ewentualne przejście przez rzekę
4. tworzy wierzchołk i krawędzie (node & edges) do pathfindera, na podstawie pól i rzek

to jest najbardziej złożony skrypt w projekcie, dzięki optymalizacjom czas to ~3 minut

Heighter/Heighter.cs

1. tworzy teksturę normalnych (normalMap) na podstawie tekstur wysokości lądu i oceanów + tekstury lądów (landMap)
2. tworzy dane o wysokości każdego pola, wyliczając średnią z pikseli tego pola

DistancesToBorder/DistancesToBorderGenerator2.cs

oblicza teksturę dystansów (distanceField) do granic pól, algorytm dijkstry.
jest to najdłuższy skrypt, czas wykonania to >20 minut.
wynika to z tego że działa na 2x większym grid niż tekstura (czyli 32_768 * 16_384)
aby wyliczyć dystanse środka, rogów i środków boków każdego piksela.

Areas/AreasGenerator.cs

przypisuje każdemu polu jego Area (makro obszar), na podstawie tekstury obszarów

Entities/EntitiesGenerator.cs

przypisuje każdemu polu jego Entity (państwo), na podstawie tekstury państw

Catchments/CatchmentsGenerator.cs

przypisuje każdemu polu zlewnię (najbliższą rzekę)

-- Inne skrypty generujące konwertują istniejące dane na odpowiedni format do gry

Pops/PopsGenerator.cs

na podstawie tekstury wielkości populacji dla każdego pikselu (https://www.worldpop.org/)
przypisuje każdemu polu jego liczbę populacji

LandForm/LandFormGenerator.cs

na podstawie danych binarnych o rodzaju terenu (https://gisstar.gsi.go.jp/terrain2018/)
przypisuje każdemu polu enum z rodzajem terenu

LandCover/LandCoverGenerator.cs

na podstawie danych o pokryciu terenu (https://globalmaps.github.io/glcnmo.html#reference)
wylicza ciągłe (procentowe) wartości tych cech terenu: wegetacji, pustynnienia, zlodowacenia, wilgotności, zabudowania, kultywacji.

Soils/SoilsGenerator.cs

na podstawie danych binarnych (https://www.isric.org/explore/soilgrids)
przypisuje każdemu polu enum z rodzajem gleby

Temperatures/TemperatureGenerator.cs i Precipitation/PrecipitationGenerator.cs

oba działają na danych z tego samego źródła (https://chelsa-climate.org/)
przypisują polom średnie temperatur i opadów (1 średnia per miesiąc)

Wszystkie dane są na licencjach otwartych, mam do nich linki.

-- Tekstury

https://drive.google.com/drive/folders/10OOxV6zqm3bP9HIljoDVNBxYpaLtiEez?usp=sharing

regions.png, entities.png, areas.png są zrobione ręcznie,
reszta tekstur to wyjście algorytmów.
żadna z tych tekstur tak naprawdę nie jest potrzebna do gry, faktyczne dane są w postaci binarnej (.bin),
tekstury służą tylko do wizualizacji danych.
