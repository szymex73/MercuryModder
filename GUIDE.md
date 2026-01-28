# Usage Guide
> [!CAUTION]
> If you're not running the game offline, and are not hosting your own server, make sure your network op allows for custom songs to be used on their network.

First prepare the folder structure for the custom songs, there's a subcommand to make it easier
```
$ MercuryModder prepare --tracks ./custom_tracks/
```

Populate the category directories according to the following scheme:
```
custom_tracks
|-Anipop
  |-Song1 // Example song with .mer+.toml
    |-meta.toml
    |-jacket.png
    |-normal.mer
    |-hard.mer
    |-expert.mer
    |-inferno.mer
    |-track.wav
  |-Song2 // Example song with just .sat
    |-jacket.png
    |-normal.sat
    |-hard.sat
    |-expert.sat
    |-inferno.sat
    |-track.wav
  |-Song3 // Example song with only the expert diff in .mer
    |-meta.toml
    |-jacket.png
    |-expert.mer
    |-track.wav
|-Vocaloid
|-Touhou
  |-Song4
  |-Song5
|-2_5D
|-Variety
  |-Song1
  |-Song3
|-Original
|-TanoC
  |-Song1
  |-Song2
  |-Song3
  |-Song4
  |-Song5
|-Inferno // Separate dir for Inferno diffs on existing songs
  |-1002 // ID of an existing song
    |-inferno.sat
    |-track.wav // Optional, only if a different audio cut is used
  |-1003
    |-inferno.mer
```

For folders that contain `.mer` charts as opposed to `.sat` files with populated metadata, an extra file called `meta.toml` needs to be provided following this format:
```toml
title = "Song title"
rubi = "ＳＯＮＧ　ＴＩＴＬＥ" # Use https://dencode.com/string/character-width
artist = "Artist"
bpm = 100
preview_start = 12.3
preview_length = 23.4

[[difficulties]]
name = "normal"
designer = "Charter1"
difficulty = 2.0

[[difficulties]]
name = "hard"
designer = "Charter2"
difficulty = 5.4

[[difficulties]]
name = "expert"
designer = "Charter3"
difficulty = 10.2

[[difficulties]]
name = "inferno"
designer = "Charter1 vs Charter2"
difficulty = 12
```

In general, a song folder needs to have at least one chart, a jacket (`jacket.png`, 256x256px) and the audio (`track.wav`). Missing difficulties will be filled out with dummy charts so single difficulties can still be played. 

In case charts in the `.mer` format are provided, the `difficulties` entries for missing diffs can be ommited.

With a ready songs directory, a check can be performed:
```
$ MercuryModder check --tracks ~/custom_tracks/
[ OK ] /home/szymex/custom_tracks/Anipop/Song1
[ OK ] /home/szymex/custom_tracks/Anipop/Song2
[WARN] /home/szymex/custom_tracks/Anipop/Song3
        [W] Expert diff was not provided
[ OK ] /home/szymex/custom_tracks/Touhou/Song4
[ OK ] /home/szymex/custom_tracks/Touhou/Song5
[ OK ] /home/szymex/custom_tracks/Variety/Song1
[ OK ] /home/szymex/custom_tracks/Variety/Song3
[FAIL] /home/szymex/custom_tracks/TanoC/Song1
        [E] Song title is not defined
        [E] Song artist is not defined
[ OK ] /home/szymex/custom_tracks/TanoC/Song2
[ OK ] /home/szymex/custom_tracks/TanoC/Song3
[ OK ] /home/szymex/custom_tracks/TanoC/Song4
[ OK ] /home/szymex/custom_tracks/TanoC/Song5
[ OK ] Inferno for song 1002
[ OK ] Inferno for song 1003
```
The tool will list out any warnings (`[W]`) or errors (`[E]`). Generally warnings can be ommited and only serve to provide information about potential missing files to the user, errors will cause next stages to fail and need to be fixed.

To perform deeper inspection of files, the `--info` flag can be used toget additional details about each song:
```
$ MercuryModder check --tracks ./custom_tracks/ --info
[...]
[ OK ] /home/szymex/custom_tracks/Variety/Song3
        Load type: LOAD_SAT
        Title: Song3
        Rubi: ＳＯＮＧ３
        Artist: ARTist
        Bpm: 175
        Genre: TanoC
        Chart Normal : 04,00 - Name
        Chart Hard   : 09,60 - Name
        Chart Expert : 11,00 - Name
[FAIL] /home/szymex/custom_tracks/TanoC/Song1
        Load type: LOAD_MER
        Title: 
        Rubi: 
        Artist: Artist0
        Bpm: 123
        Genre: TanoC
        Chart Normal : 03,00 - Mapper
        Chart Hard   : 08,90 - Mapper
        Chart Expert : 12,40 - Mapper
        [E] Song title is not defined
        [E] Song artist is not defined
[...]
```
> [!IMPORTANT]
> Watch out for any lines with `[FAIL]` and `[E]`, as these are things that need to be fixed before you can proceed.

If all songs pass with `OK` or `WARN`, modification can be performed:
```
$ MercuryModder modify --tracks ./custom_tracks/ --output ./path/to/mod/output --gameDir ./path/to/clean/WindowsNoEditor
```

The output can be either pointed at a new folder, and will result in only the new modded files being produced, or can be pointed at the `WindowsNoEditor` folder of the modded copy of the game to put the assets directly into it.

That said, the `WindowsNoEditor` folder in the "new modded files being produced" can be directly combined with an existing `WindowsNoEditor` folder with overriding any existing files.

Please do not use the same copy of the game as source and destination, this is not a supported use-case.
