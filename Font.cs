/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2016 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.2
 */

/* CHANGE LOG
 * v1.2,
 * [ADD] _isModified edits
 * [ADD] _baseLine
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "FONT" typeface resources</summary>
	/// <remarks>The Font resource controls all of the typefaces used outside of the flight engine. Menus, briefing text, even the scrolling text during the intro.<hr/>
	/// <h4>Raw Data definition</h4>
	/// <code>// Pseudo-code resource structure
	/// struct RawData
	/// {
	///   /* 0x00 */ short StartingChar;	// typically 0x20 (space)
	///   /* 0x02 */ short NumberOfGlyphs;
	///   /* 0x04 */ short BitsPerScanLine;	// multiple of 8
	///   /* 0x06 */ short Height;
	///   /* 0x08 */ short BaseLine;
	///   /* 0x0A */ short Reserved = 0x00;
	///   /* 0x0C */ byte[NumberOfGlyphs] GlyphWidths;
	///   /* 0x0C + NumberOfGlyphs */ Glyph[NumberOfGlyphs] Glyphs;
	/// }
	/// 
	/// struct Glyph
	/// {
	///   /* 0x00 */ byte[BitsPerScanLine * Height / 8] Rows;
	/// }</code>
	/// In TIE95 there are a total of seven FONT resources, all of which have a <i>StartingChar</i> of <b>0x20</b>, which makes sense since you kinda need a space, and there's nothing really before that anyway. Only "TITLE.LFD:FONThelv-20" has a non-0x60 value for <i>NumberOfGlyphs</i>, which is the scrolling text in the title crawl.<br/><br/>
	/// <i>BitsPerScanLine</i> tells you how much you have to read before going on to the next line. <u>Must be a multiple of 8</u>. <i>Height</i> is just that, how many rows per glyph. <i>BaseLine</i> is a zero-indexed row that is defined as the bottom of the glyphs. This is so letters such as 'j' and 'g' hang below the "bottom" as they should.<br/><br/>
	/// The <i>GlyphWidth</i> values are in pixels, starting from the left of the glyph.<br/><br/>
	/// -- Row --<br/><br/>
	/// The Row values within the actual glyph data are bit fields.  Monochrome bitmaps, so '0' is transparent, '1' is solid.<br/><br/>
	/// EMPIRE.LFD:FONTfont6 has <i>BitsPerScanLine</i> = <b>0x08</b>, <i>Height</i> = <b>0x06</b> and <i>BaseLine</i> = <b>0x04</b>. The letter 'A' in that resource has its <i>Rows</i> array as <c>E0 A0 E0 A0 A0 00</c>.<br/><br/>
	/// <c>XXX.....</c>		E0 = b11100000<br/>
	/// <c>X.X.....</c>		A0 = b10100000<br/>
	/// <c>XXX.....</c>		b11100000<br/>
	/// <c>X.X.....</c>		b10100000<br/>
	/// <c>X.X.....</c>		b10100000<br/>
	/// <c>........</c>		b00000000<br/><br/>
	/// If <i>BitsPerScanLine</i> is <b>0x10</b>, then the rows are 16 pixels wide, <b>0x18</b> is 24 pix wide, etc.  The Width value for the letter 'A' in this case is <b>0x03</b>, which as shown above is the occupied width of the character.  Spaces between glyphs are automatically counted as 1 pixel.</remarks>
	public partial class Font : Resource
	{
		short _startingChar;
		short _bitsPerScanLine;
		short _height;
        short _baseLine;
		Bitmap[] _glyphs;
		GlyphIndexer _glyphIndexer;

		#region constructors
		/// <summary>Creates a new instance and prepares for a new character set</summary>
		/// <param name="startChar">First defined ASCII value (normally <b>32</b>)</param>
		/// <param name="numberOfChars">Number of characters to be defined</param>
		/// <param name="height">Height of the character set in pixels</param>
		/// <remarks><see cref="BaseLine"/> is set between 2/3 and 3/4 of <i>height</i>.<br/>
		/// <see cref="BitsPerScanLine"/> is set to round up from square characters.
		/// A <i>height</i> of <b>8</b> produces a maximum width of <b>8</b>,
		/// while a <i>height</i> of <b>12</b> produces a width of <b>16</b>.<br/>
		/// All images in <see cref="Glyphs"/> are initialized to blank <see cref="PixelFormat.Format1bppIndexed"/> images,
		/// <see cref="BitsPerScanLine"/>x<see cref="Height"/> in size.</remarks>
		public Font(short startChar, short numberOfChars, short height)
		{
			_startingChar = startChar;
			_height = height;
			BaseLine = (short)Math.Ceiling((double)_height * .67);
			_bitsPerScanLine = (short)(_height + (_height % 8 == 0 ? 0 : 8 - (_height % 8)));
			_glyphs = new Bitmap[numberOfChars];
			for (int i = 0; i < _glyphs.Length; i++) _glyphs[i] = new Bitmap(_bitsPerScanLine, _height, PixelFormat.Format1bppIndexed);
			_glyphIndexer = new GlyphIndexer(this);
		}
		/// <summary>Creates a new instance and prepares for a new character set starting from ASCII 32 (space)</summary>
		/// <param name="numberOfChars">Number of characters to be defined</param>
		/// <param name="height">Height of the character set in pixels</param>
		/// <remarks><see cref="BaseLine"/> is set between 2/3 and 3/4 of <i>height</i>.<br/>
		/// <see cref="BitsPerScanLine"/> is set to round up from square characters.
		/// A <i>height</i> of <b>8</b> produces a maximum width of <b>8</b>,
		/// while a <i>height</i> of <b>12</b> produces a width of <b>16</b>.<br/>
		/// All images in <see cref="Glyphs"/> are initialized to blank <see cref="PixelFormat.Format1bppIndexed"/> images,
		/// <see cref="BitsPerScanLine"/>x<see cref="Height"/> in size.<br/>
		/// <see cref="StartingChar"/> defaults to <b>32</b>.</remarks>
		public Font(short numberOfChars, short height)
		{
			_startingChar = 32;
			_height = height;
			BaseLine = (short)Math.Ceiling((double)_height * .67);
			_bitsPerScanLine = (short)(_height + (_height % 8 == 0 ? 0 : 8 - (_height % 8)));
			_glyphs = new Bitmap[numberOfChars];
			for (int i = 0; i < _glyphs.Length; i++) _glyphs[i] = new Bitmap(_bitsPerScanLine, _height, PixelFormat.Format1bppIndexed);
			_glyphIndexer = new GlyphIndexer(this);
		}
		/// <summary>Creates a new instance from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Font(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Font(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			_read(fsLFD, filePosition);
			fsLFD.Close();
		}
		#endregion constructors
		
		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to populate the resource</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Whether or not <i>raw</i> contains the resource Header information</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="ResourceType.Font"/></exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Font) throw new ArgumentException("Raw header is not for a Font resource");
			int offset = 0;
			_startingChar = BitConverter.ToInt16(_rawData, offset);
			_glyphs = new Bitmap[BitConverter.ToInt16(_rawData, offset + 2)];
			_bitsPerScanLine = BitConverter.ToInt16(_rawData, offset + 4);
			_height = BitConverter.ToInt16(_rawData, offset + 6);
			_baseLine = BitConverter.ToInt16(_rawData, offset + 8);
			offset += 12;
			for (int i = 0; i < _glyphs.Length; i++)
				_glyphs[i] = new Bitmap(_rawData[offset++], _height, PixelFormat.Format1bppIndexed);
			for (int i = 0; i < _glyphs.Length; i++)
			{
				BitmapData bd1 = GraphicsFunctions.GetBitmapData(_glyphs[i]);
				byte[] pix1 = new byte[bd1.Stride * bd1.Height];
				for (int y = 0; y < _height; y++)
					for (int s = 0; s < (_bitsPerScanLine / 8); s++)
						pix1[y * bd1.Stride + s] = _rawData[offset++];
				GraphicsFunctions.CopyBytesToImage(pix1, bd1);
				_glyphs[i].UnlockBits(bd1);
			}
			_glyphIndexer = new GlyphIndexer(this);
		}

		/// <summary>Prepares the resource for writing and updates <see cref="RawData"/></summary>
		public override void EncodeResource()
		{
			byte[] raw = new byte[12 + (_bitsPerScanLine / 8 * _height + 1) * _glyphs.Length];
			ArrayFunctions.WriteToArray(_startingChar, raw, 0);
			ArrayFunctions.WriteToArray((short)_glyphs.Length, raw, 2);
			ArrayFunctions.WriteToArray(_bitsPerScanLine, raw, 4);
			ArrayFunctions.WriteToArray(_height, raw, 6);
			ArrayFunctions.WriteToArray(BaseLine, raw, 8);
			for (int i = 0; i < _glyphs.Length; i++) raw[12 + i] = (byte)_glyphs[i].Width;
			for (int i = 0, offset = 12 + _glyphs.Length; i < _glyphs.Length; i++)
			{
				BitmapData bd1 = GraphicsFunctions.GetBitmapData(_glyphs[i]);
				byte[] pix1 = new byte[bd1.Stride * bd1.Height];
				GraphicsFunctions.CopyImageToBytes(bd1, pix1);
				ArrayFunctions.WriteToArray(pix1, raw, ref offset);
				_glyphs[i].UnlockBits(bd1);
			}
			_rawData = raw;
		}
		
		/// <summary>Changes the primary color of the font</summary>
		/// <remarks>Background color will be <see cref="Color.Transparent"/></remarks>
		/// <param name="glyphColor">The new font color</param>
		public void SetColor(Color glyphColor) { SetColor(glyphColor, true); }

		/// <summary>Changes the primary color of the font</summary>
		/// <remarks>If <i>transparent</i> is <b>false</b>, the background color will be <see cref="Color.Black"/>.<br/>
		/// If <i>glyphColor</i> is Black, then the background will be <see cref="Color.White"/></remarks>
		/// <param name="glyphColor">The new font color</param>
		/// <param name="transparent">If the background will be <see cref="Color.Transparent"/></param>
		public void SetColor(Color glyphColor, bool transparent)
		{
			ColorPalette newpal = _glyphs[0].Palette;
			if (transparent) newpal.Entries[0] = Color.Transparent;
			else
			{
				if (glyphColor == Color.Black) newpal.Entries[0] = Color.White;
				else newpal.Entries[0] = Color.Black;
			}
			newpal.Entries[1] = glyphColor;
			for (int i = 0; i < _glyphs.Length; i++) _glyphs[i].Palette = newpal;
		}
		#endregion public methods

		#region public properties
		/// <summary>Gets the indexer for the glyphs</summary>
		public GlyphIndexer Glyphs { get { return _glyphIndexer; } }
		/// <summary>Gets the ASCII value of the first character within the resource</summary>
		/// <remarks>Typically <b>32</b> (space).</remarks>
		public short StartingChar {	get { return _startingChar; } }
		/// <summary>Gets the number of characters contained within the resource</summary>
		public short TotalChars { get { return (short)_glyphs.Length; } }
		/// <summary>Gets or sets the length of bits required per scanline</summary>
		/// <remarks>Must be a multiple of <b>8</b>.</remarks>
		/// <exception cref="ArgumentException"><i>value</i> is not a positive multiple of 8</exception>
		public short BitsPerScanLine
		{
			get { return _bitsPerScanLine; }
			set 
			{
				if ((value % 8) != 0 || value <= 0) throw new ArgumentException("Value must be a positive multiple of 8", "value");
				_bitsPerScanLine = value;
                _isModifed = true;
			}	// this is left as write-enabled to allow wider characters
		}
		/// <summary>Gets the total height of the font, also number of ScanLines</summary>
		public short Height { get { return _height; } }
		/// <summary>Gets or sets the zero-indexed ScanLine that is used as the "bottom" of the font</summary>
		/// <remarks>Characters such as 'j' typically drop below this line. Is typically 2/3 to 3/4 the value of <see cref="Height"/>.</remarks>
		public short BaseLine
        {
            get { return _baseLine; }
            set
            {
                _baseLine = value;
                _isModifed = true;
            }
        }
		#endregion public properties
	}
}
