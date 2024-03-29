Idmr.LfdReader.dll
=================

Author: Michael Gaisser (mjgaisser@gmail.com)
Version: 2.3
Date: 2023.07.16

Library for editing LucasArts *.LFD resource files.

=========
Version History

 - (MASK) Fixed the Height calculation
 - (MASK) Fixed the 00 processing and corner cases
 - (PANL) Fixed processing the FC Opcode to properly modify the ColorIndex

v2.3, 16 Jul 2023
 - (FILM) Chunk Opcodes added or renamed:
   - SetColorRange = 0x10, new for VIEW
   - Unknown11 is now ApplyPalette for VIEW
   - Unknown13 is now Toggle for VOIC
   - SetVolume = 0x15, new for VOIC
   - Unknown16 = 0x16, new for VOIC
     - Renames left originals in place as Depcrecated, not a breaking change.
 - (PLTT) Added the newly discovered IndexRotator as read only values, discussed in Issue #4.

v2.2, 01 Apr 2023
 - (CRFT, CPLX, SHIP) Added the IsTwoSided and IsGouraudShaded properties to Shape per discussion in Issue #3.
 - Minor XML updates

v2.1, 30 Oct 2022
 - (CRFT) Can now convert wireframe data to SHIP, but not raw data
 - (CPLX) Can now convert wireframe data to SHIP, but not raw data
 - (CRFT, CPLX, SHIP) Now calculates the Lines in a mesh during Decode.

v2.0, 09 Mar 2021
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

v1.2.1, 02 Sep 2019
 - (BLAS) Fixed a crash that occur during Decode for VOIC types. [Issue #1]

v1.2, 12 Jul 2016
 - _isModifed edits.
 - (ANIM) removed old code duplicating DELT data.
 - (FONT) Fixed EncodeResource with large Strides.
 - (FONT) added _baseLine.
 - (LfdFile) Always zeroes out name before write.
 - (LfdFile) Only encodes children if they report being modified.
 - various other tweaks.

v1.1, 14 Dec 2014
 - Changed license to MPL.
 - (ANIM) SetCount and IsModified implementation in FrameCollection.
 
v1.0, 05 Dec 2011
 - Release.

==========
Additional Information

Idmr.Common.dll (v1.1 or later) and System.Drawing are required references.

To use LfdReader.Xact, Idmr.ImageFormat.Act.dll (v2.0 or later) is a required
reference.

File structure information per resource is found in the individual class files.

Programmer's reference can be found in help/Idmr.LfdReader.chm.

==========
Copyright Information

Copyright � 2009-2021 Michael Gaisser
This library file and related files are licensed under the Mozilla Public License
v2.0 or later.  See MPL.txt for further details.

The Galactic Empire: Empire Reborn is Copyright � 2004- Tiberius Fel.

"Star Wars" and related items are trademarks of LucasFilm Ltd and
LucasArts Entertainment Co.

THESE FILES HAVE BEEN TESTED AND DECLARED FUNCTIONAL BY THE AUTHOR, AS SUCH THE
AUTHOR CANNOT BE HELD RESPONSIBLE OR LIABLE FOR UNWANTED EFFECTS DUE ITS USE OR
MISUSE. THIS SOFTWARE IS OFFERED AS IS WITHOUT WARRANTY OF ANY KIND.