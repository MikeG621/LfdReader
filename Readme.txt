Idmr.LfdReader.dll
=================

Author: Michael Gaisser (mjgaisser@gmail.com)
Version: 1.2.1
Date: 2019.09.02

Library for editing LucasArts *.LFD resource files

=========
Version History

 - (ANIM) Fixed the retrieval of Frames when RelativePosition is turned on.
 - (BLAS) Added GetWavBytes() to make audio playback easier, reformats data to .WAV
 - (FILM) Added ToString() to Block and Chunk
 - (CRFT) New
 - (Resource) Btmp, Crft and Cplx added to ResourceType
 - various updates

v1.2.1, 02 Sep 2019
 - (BLAS) Fixed a crash that occur during Decode for VOIC types [Issue #1]

v1.2, 12 Jul 2016
 - _isModifed edits
 - (ANIM) removed old code duplicating DELT data
 - (FONT) Fixed EncodeResource with large Strides
 - (FONT) added _baseLine
 - (LfdFile) Always zeroes out name before write
 - (LfdFile) Only encodes children if they report being modified
 - various other tweaks
v1.1, 14 Dec 2014
 - Changed license to MPL
 - (ANIM) SetCount and IsModified implementation in FrameCollection
 
v1.0, 05 Dec 2011
 - Release

==========
Additional Information

Idmr.Common.dll (v1.1 or later) and System.Drawing are required references

To use LfdReader.Xact, Idmr.ImageFormat.Act.dll (v2.0 or later) is a required
reference

File structure information per resource is found in the individual class files

Programmer's reference can be found in help/Idmr.LfdReader.chm

==========
Copyright Information

Copyright © 2009-2020 Michael Gaisser
This library file and related files are licensed under the Mozilla Public License
v2.0 or later.  See MPL.txt for further details.

The Galactic Empire: Empire Reborn is Copyright © 2004- Tiberius Fel

"Star Wars" and related items are trademarks of LucasFilm Ltd and
LucasArts Entertainment Co.

THESE FILES HAVE BEEN TESTED AND DECLARED FUNCTIONAL BY THE AUTHOR, AS SUCH THE
AUTHOR CANNOT BE HELD RESPONSIBLE OR LIABLE FOR UNWANTED EFFECTS DUE ITS USE OR
MISUSE. THIS SOFTWARE IS OFFERED AS IS WITHOUT WARRANTY OF ANY KIND.