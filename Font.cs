/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2011 Michael Gaisser (mjgaisser@gmail.com)
 * * Licensed under the GPL v3.0 or later
 * 
 * Full notice in Resource.cs
 * Version: 1.0
 */

/* CHANGELOG
 * 110922 - housekeeping, added added LoadFileException and SaveFileException throws, Write() return void
 * 110925 - implemented Decode/EncodeResource(), NumScanLines to Height
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Reads LFD files and interprets FONT resources</summary>
	public class Font : Resource
	{
		short _startingChar;
		short _bitsPerScanLine;
		short _height;
		short _baseLine;
		Bitmap[] _glyphs;

		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Font(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Font(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			_read(fsLFD, filePosition);
			fsLFD.Close();
		}
		
		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		//===================
		/// <summary>Processes raw data to create Font information</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Header</param>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			int offset = 0;
			_decodeResource(raw, containsHeader);
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
				for(int y=0;y<_height;y++)
					for(int s=0;s<(_bitsPerScanLine/8);s++)
						pix1[y * bd1.Stride + s] = _rawData[offset++];
				GraphicsFunctions.CopyBytesToImage(pix1, bd1);
				_glyphs[i].UnlockBits(bd1);
			}
		}

		/// <summary>Prepare Font information for writing</summary>
		/// <returns>Raw data ready to write to file</returns>
		public override void EncodeResource()
		{
			byte[] raw = new byte[12 + (_bitsPerScanLine / 8 * _height + 1) * _glyphs.Length];
			ArrayFunctions.WriteToArray(_startingChar, raw, 0);
			ArrayFunctions.WriteToArray((short)_glyphs.Length, raw, 2);
			ArrayFunctions.WriteToArray(_bitsPerScanLine, raw, 4);
			ArrayFunctions.WriteToArray(_height, raw, 6);
			ArrayFunctions.WriteToArray(_baseLine, raw, 8);
			for(int i = 0; i < _glyphs.Length; i++) raw[12 + i] = (byte)_glyphs[i].Width;
			int offset = 12 + _glyphs.Length;
			for(int i = 0; i < _glyphs.Length; i++)
			{
				BitmapData bd1 = GraphicsFunctions.GetBitmapData(_glyphs[i]);
				byte[] pix1 = new byte[bd1.Stride * bd1.Height];
				GraphicsFunctions.CopyImageToBytes(bd1, pix1);
				ArrayFunctions.WriteToArray(pix1, raw, ref offset);
				_glyphs[i].UnlockBits(bd1);
			}
			_rawData = raw;
		}

		/// <summary>Gets the ASCII value of the first character within the resource</summary>
		public short StartingChar {	get { return _startingChar; } }
		/// <summary>Gets the number of characters contained within the resource</summary>
		public short TotalChars { get { return (short)_glyphs.Length; } }
		/// <summary>Gets or sets the length of values required per scanline</summary>
		/// <exception cref="ArgumentException"><i>value</i> is not a positive multiple of eight</exception>
		public short BitsPerScanLine
		{
			get { return _bitsPerScanLine; }
			set 
			{
				if ((value % 8) != 0 || value <= 0) throw new ArgumentException("Value must be a positive multiple of 8.", "value");
				_bitsPerScanLine = value;
			}	// this is left as write-enabled to allow wider characters
		}
		/// <summary>Gets the total height of the font, also number of ScanLines</summary>
		public short Height { get { return _height; } }
		/// <summary>Gets or sets the zero-indexed ScanLine that is used as the "bottom" for the font.</summary>
		public short BaseLine
		{
			get { return _baseLine; }
			set { _baseLine = value; }
		}

		/// <returns>Selected glyph</returns>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid glyph</exception>
		public Bitmap GetGlyph(int index) { return _glyphs[index]; }
		/// <summary>Overwrites selected glyph with given image</summary>
		/// <param name="index">Glyph index</param>
		/// <param name="image">Bitmap, converts to Format1bppIndexed, must match Height</param>
		/// <exception cref="ArgumentException"><i>image.Height</i> does not equal Height or <i>image.Width</i> exceeds BitsPerScanLine</exception>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid glyph</exception>
		public void ReplaceGlyph(int index, Bitmap image)
		{
			if (image.Height != _height) throw new ArgumentException("New image not required height (" + _height.ToString() + "px).", "image");
			if (image.Width > _bitsPerScanLine) throw new ArgumentException("New image exceeds maximum width (" + _bitsPerScanLine.ToString() + "px).", "image");
			_glyphs[index] = GraphicsFunctions.ConvertTo1bpp(image);
		}
	}
}
