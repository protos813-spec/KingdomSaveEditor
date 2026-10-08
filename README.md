![Kingdom Save Editor](docs/banner.png)

---

| Supported games                | Console               | Region |
|--------------------------------| ----------------------|--------|
| Kingdom Hearts I               | PS2/PS3/PS4/PC/Switch | All    |
| Kingdom Hearts Re: CoM         | PS2/PS4/PC/Switch     | All    |
| Kingdom Hearts II              | PS2/PS3/PS4/PC/Switch | US/EU/FM |
| Kingdom Hearts: Birth By Sleep | PSP/PS3/PS4/PC/Switch | FM     |
| Kingdom Hearts: Dream Drop Distance | 3DS/PC           | All    |
| Kingdom Hearts 0.2             | PS4                   | All    |
| Kingdom Hearts III             | PS4/PC                | All    |
| Final Fantasy VII Remake       | PS4/PC                | All    |
| Persona 5, Persona 5 Royal     | PS3/PS4               | US/EU  |

[![Download](https://img.shields.io/github/downloads/BFlorry/KingdomSaveEditor/total.svg?)](https://github.com/BFlorry/KingdomSaveEditor/releases)
![Last commit](https://img.shields.io/github/last-commit/BFlorry/KingdomSaveEditor.svg)
![Tests status](https://github.com/BFlorry/KingdomSaveEditor/workflows/Tests/badge.svg)

## User guide

If reading/editing a console save, you need to decrypt your save before opening it with Kingdom Save Editor. Please refer to [this guide](docs/decryption.md) to know how to decrypt your save. 
PC versions do not need decrypting and eg. for KH HD1.5+2.5 REMIX Steam version (in Windows) the saves can be found in `C:\Users\{$USERNAME}\Documents\My Games\KINGDOM HEARTS HD 1.5+2.5 ReMIX\Steam\{$ID}\` and the save files are .png files.

### Nintendo Switch (KH HD 1.5+2.5 ReMIX, native version)

Saves from the native Switch version use the same `.png` container as the PC
version, so no decryption step was needed in testing.

- Tested: KH2 Final Mix. Opening, editing and restoring the save on the Switch
  worked, and a Steam save loaded on Switch after being renamed.
- File naming: the Switch file is named `KHIIFM.png`; the Steam file is
  `KHIIFM_WW.png`. Rename to match what the Switch expects.
- Always back up the original file before editing and edit a copy.

## Contribution

### Contribute to make it better

This software is **free and open-source**, and every contribution is more than welcome!

If you want to add missing names, improve it or add new offsets, just clone the repository, do your change, test if it does work and create a pull request: we will review your change (no needs to be scared here) and we will merge it to this repo! Do not be shy on contribute, even for the smallest thing 😃

## Special thanks

* xeeynamo for the project and codebase creation and maintaining up until 2022
* Rikux3 for the incredible support of Kingdom Hearts 1 and Birth By Sleep Final Mix, the PC release of Kingdom Hearts games, the CBS PSU and PSV support
* Keytotruth for additional coding and offset findings for Kingdom Hearts III
* Delta-47 for the incredible support of Dream Drop Distance for 3DS, PS4 and PC and the European/Japanese support for Kingdom Hearts 1
* Skiller for the multiple offsets and values for Persona 5 / Royal and the tips for fix a Kingdom Hearts III checksum and decrypt the 1.5+2.5 ReMIX PC encrypted header
* Troopah to provide the icons used in the very first version of the editor
* Sonicshadowsilver2 for the early findings of story flags and records offsets for Kingdom Hearts III
* 13th Vessel to have found the complete story flags list for Kingdom Hearts III
* TALESIOFIFREAK for the ability list and DLC inventory for Kingdom Hearts III
* Silvercam for the list of gummiship inventory items for Kingdom Hearts III
* Luseu to have provided the majority of Final Fantasy VII Remake offsets
* fungualtissue1230 for the code to support the PC versions of Kingdom Hearts III
* All the sponsors / donators who contributed to xeeynamo for the initial project
* Minty123 for codebase cleanup after project archiving
* u/k23usa for creating an extensive save collection of 1.5+2.5 [on r/KingdomHearts](https://www.reddit.com/r/KingdomHearts/comments/plwisw/ive_created_a_collection_of_save_points_pc/)

## License

The code itself, the interface and the codes inside it are protected by GPL 3.0 license, unless specified differently in the root of a specific folder. In short, that means that for every change you made or code that you take from here, you need to make it open source somewhere, adding the original copyright statement and specify where the original code has been taken.

If you have more doubts about the GPL license, have a read to the following links:

[LICENSE info](https://tldrlegal.com/license/gnu-general-public-license-v3-(gpl-3))

[LICENSE Wikipedia](https://simple.wikipedia.org/wiki/GNU_General_Public_License)

## Privacy

The application will have full access to the file you will open by using "File\Open" in order to be able to modify your save game data.
