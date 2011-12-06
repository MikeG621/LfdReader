/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2011 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in Resource.cs
 * Version: 1.0
 */

/* CHANGELOG
 * 110922 - housekeeping, added added LoadFileException and SaveFileException throws, Write() return void
 * 110926 - implemented DecodeResource()
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Idmr.LfdReader
{
	/// <summary>Reads LFD files and interprets PLTT color palette resources</summary>
	public class Pltt : Resource
	{
		byte _startIndex;
		byte _endIndex;
		PlttColor[] _entries;

		public Pltt()
		{
		}
		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Pltt(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <param name="path">This is the full path of the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Pltt(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			_read(fsLFD, filePosition);
			fsLFD.Close();
		}
		
		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new Common.LoadFileException(x); }
		}

		//===================
		/// <summary>Processes raw data to create Pltt information</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Header</param>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			int offset = 0;
			_decodeResource(raw, containsHeader);
			_startIndex = _rawData[offset++];
			_endIndex = _rawData[offset++];
			_entries = new PlttColor[_endIndex - _startIndex + 1];
			for (int i = 0; i < _entries.Length; i++, offset += 3) _entries[i] = new PlttColor(_rawData[offset], _rawData[offset + 1], _rawData[offset + 2]);
		}
		
		/// <summary>Prepare Pltt information for writing</summary>
		/// <returns>Raw data ready to write to file</returns>
		public override void EncodeResource()
		{
			byte[] raw = new byte[_entries.Length * 3 + 3];
			raw[0] = _startIndex;
			raw[1] = _endIndex;
			int offset = 2;
			for (int i = 0; i < _entries.Length; i++, offset += 3)
			{
				raw[offset] = _entries[i].R;
				raw[offset + 1] = _entries[i].G;
				raw[offset + 2] = _entries[i].B;
			}
			_rawData = raw;
		}

		/// <summary>Gets the starting index of the color definitions</summary>
		public byte StartIndex { get { return _startIndex; } }
		/// <summary>Gets the ending index of the color definitions</summary>
		public byte EndIndex { get { return _endIndex; } }
		/// <summary>Gets the colors defined for the Pltt</summary>
		public PlttColor[] Entries { get { return _entries; } }
		/// <summary>Gets the ColorPalette reprenstation of the Pltt</summary>
		/// <remarks>Colors not used by the Pltt are default 8bpp Indexed colors</remarks>
		public ColorPalette Palette
		{
			get
			{
				ColorPalette pal = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
				for (int i=_startIndex;i<=_endIndex;i++) pal.Entries[i] = _entries[i-_startIndex].Color;
				return pal;
			}
		}

		/// <summary>The RGB container for the color definitions</summary>
		public struct PlttColor
		{
			/// <summary>Gets or sets the red value</summary>
			public byte R;
			/// <summary>Gets or sets the green value</summary>
			public byte G;
			/// <summary>Gets or sets the blue value</summary>
			public byte B;

			/// <summary>Initialize the PlttColor with the starting color values</summary>
			/// <param name="red">The Red index</param>
			/// <param name="green">The Green index</param>
			/// <param name="blue">The Blue index</param>
			public PlttColor(byte red, byte green, byte blue)
			{
				R = red;
				G = green;
				B = blue;
			}

			/// <summary>Gets or sets the color</summary>
			public Color Color
			{
				get { return Color.FromArgb(R, G, B); }
				set
				{
					R = value.R;
					G = value.G;
					B = value.B;
				}
			}
		}
	}
}
