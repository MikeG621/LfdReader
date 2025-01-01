# [Idmr.LfdReader.dll](https://github.com/MikeG621/LfdReader)

Author: [Michael Gaisser](mailto:mjgaisser@gmail.com)  
![GitHub Release](https://img.shields.io/github/v/release/MikeG621/LfdReader)
![GitHub Release Date](https://img.shields.io/github/release-date/MikeG621/LfdReader)
![GitHub License](https://img.shields.io/github/license/MikeG621/LfdReader)

Library for editing LucasArts *.LFD resource files.

## Latest Release
#### v2.3, 16 Jul 2023
- (FILM) Chunk Opcodes added or renamed:
  - SetColorRange = 0x10, new for VIEW
  - Unknown11 is now ApplyPalette for VIEW
  - Unknown13 is now Toggle for VOIC
  - SetVolume = 0x15, new for VOIC
  - Unknown16 = 0x16, new for VOIC
    - Renames left originals in place as Depcrecated, not a breaking change.
- (PLTT) Added the newly discovered IndexRotator as read only values, discussed in Issue #4.

#### WIP
- (MASK) Fixed the Height calculation
- (MASK) Fixed the 00 processing and corner cases
- (PANL) Fixed processing the FC Opcode to properly modify the ColorIndex

### Additional Information

#### Dependencies
- [Idmr.Common](https://github.com/MikeG621/Common) (v1.1 or later)
- [Idmr.ImageFormat.Act](https://github.com/MikeG621/ImageFormat-Act) (v2.0 or later), necessary only for LfdReader.Xact

File structure information per resource is found in the individual class files.

Programmer's reference can be found in the [help file](help/Idmr.LfdReader.chm).

### Version History
#### v2.2, 01 Apr 2023
- (CRFT, CPLX, SHIP) Added the IsTwoSided and IsGouraudShaded properties to Shape per discussion in Issue #3.
- Minor XML updates

#### v2.1, 30 Oct 2022
- (CRFT) Can now convert wireframe data to SHIP, but not raw data
- (CPLX) Can now convert wireframe data to SHIP, but not raw data
- (CRFT, CPLX, SHIP) Now calculates the Lines in a mesh during Decode.

#### v2.0, 09 Mar 2021
- (ANIM) Fixed the retrieval of Frames when RelativePosition is turned on.
- (BLAS) Added GetWavBytes() to make audio playback easier, reformats data to .WAV.
- (BLAS) Added Duration property to get audio length in seconds.
- (FILM) Added ToString() to Block and Chunk.
- (FONT) TotalChars renamed to NumberOfGlyphs (breaking change).
- (CRFT) New.
- (CPLX) New.
- (SHIP) New.
- (Resource) Adlb, Btmp, Crft, Cplx, Rlnd, and Ship added to ResourceType.
- various updates.

#### v1.2.1, 02 Sep 2019
- (BLAS) Fixed a crash that occur during Decode for VOIC types. [Issue #1]

#### v1.2, 12 Jul 2016
- _isModifed edits.
- (ANIM) removed old code duplicating DELT data.
- (FONT) Fixed EncodeResource with large Strides.
- (FONT) added _baseLine.
- (LfdFile) Always zeroes out name before write.
- (LfdFile) Only encodes children if they report being modified.
- various other tweaks.

#### v1.1, 14 Dec 2014
- Changed license to MPL.
- (ANIM) SetCount and IsModified implementation in FrameCollection.
 
#### v1.0, 05 Dec 2011
- Release.

---
#### Copyright Information

Copyright © 2009-2021 Michael Gaisser  
This library file and related files are licensed under the Mozilla Public License
v2.0 or later.  See [License.txt](License.txt) for further details.

"Star Wars" and related items are trademarks of LucasFilm Ltd and LucasArts Entertainment Co.