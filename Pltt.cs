/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2014 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.1
 */

/* CHANGE LOG
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Idmr.LfdReader
{
	/// <summary>Object for "PLTT" color palette resources</summary>
	/// <remarks>The Pltt resource is the definition of most colors used for LFD image formats. It uses 24-bit RGB to define the colors, up to 256 colors total.<hr/>
	/// <h4>Raw Data definition</h4>
	/// <code>// Pseudo-code resource structure
	/// struct RawData
	/// {
	///   /* 0x00 */ byte StartIndex;
	///   /* 0x01 */ byte EndIndex;
	///   /* 0x02 */ PlttColor[EndIndex - StartIndex + 1];
	///   /* 0x?? */ byte Reserved = 0x00;
	/// }
	/// 
	/// struct PlttColor
	/// {
	///   /* 0x00 */ byte Red;
	///   /* 0x01 */ byte Green;
	///   /* 0x02 */ byte Blue;
	/// }</code>
	/// The <i>StartIndex</i> defines the first ColorIndex that Color values are given for. The <i>EndIndex</i> likewise defines the last ColorIndex that is being defined. For example, the palette for the Battle Selection screen is defined in TOURDESK.LFD and begins at <i>StartIndex</i> = <b>0x20</b> and runs through <i>EndIndex</i> = <b>0xFF</b>. Within the program Pltts are loaded sequentially to create the working palette (hence <see cref="ConvertToPalette"/>). ColorIndexes in reality are called from the working palette, not from the individual Pltts.<br/><br/>
	/// The Color values are simply RGB values ranging from <b>0x00-0xFF</b>.<br/><br/>
	/// The typical values for the beginning range appear to be the standard 16 colors for <b>0x00-0x0F</b>, with greyscale values for <b>0x10-0x1F</b> as defined in EMPIRE.LFD:PLTTstandard.</remarks>
	public partial class Pltt : Resource
	{
		byte _startIndex = 0;
		byte _endIndex = 0;
		Color[] _entries = new Color[256];
		ColorIndexer _colorIndexer;

		#region constructors
		/// <summary>Blank constructor</summary>
		/// <remarks>Defaults to a single color, set to <see cref="Color.Black"/>. Unused colors are initialized to <see cref="Color.Transparent"/>.</remarks>
		public Pltt()
		{
			_type = ResourceType.Pltt;
			_entries[0] = Color.Black;
			for (int i = 1; i < 256; i++) _entries[i] = Color.Transparent;
			_colorIndexer = new ColorIndexer(this);
		}
		/// <summary>Creates a new instance from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Pltt(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an existing file</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Pltt(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			_read(fsLFD, filePosition);
			fsLFD.Close();
		}
		#endregion constructors
		
		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new Common.LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to populate the resource</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Whether or not <i>raw</i> contains the resource Header information</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="ResourceType.Pltt"/></exception>
		/// <remarks>Unused entries are initialized to <see cref="Color.Transparent"/>.</remarks>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Pltt) throw new ArgumentException("Raw header is not for a Pltt resource");
			int offset = 0;
			_startIndex = _rawData[offset++];
			_endIndex = _rawData[offset++];
			for (int i = 0; i < _startIndex; i++) _entries[i] = Color.Transparent;
			for (int i = _startIndex; i <= _endIndex; i++, offset += 3) _entries[i] = Color.FromArgb(_rawData[offset], _rawData[offset + 1], _rawData[offset + 2]);
			for (int i = _endIndex + 1; i < 256; i++) _entries[i] = Color.Transparent;
			_colorIndexer = new ColorIndexer(this);
		}
		
		/// <summary>Prepares the resource for writing and updates <see cref="RawData"/></summary>
		public override void EncodeResource()
		{
			byte[] raw = new byte[_entries.Length * 3 + 3];
			raw[0] = _startIndex;
			raw[1] = _endIndex;
			int offset = 2;
			for (int i = _startIndex; i <= _endIndex; i++, offset += 3)
			{
				raw[offset] = _entries[i].R;
				raw[offset + 1] = _entries[i].G;
				raw[offset + 2] = _entries[i].B;
			}
			_rawData = raw;
		}
		
		/// <summary>Generates a ColorPalette from multiple Pltt resources</summary>
		/// <param name="resources">The resources used to create the palette</param>
		/// <returns>A single ColorPalette to be applied to an image</returns>
		/// <remarks>Unused colors are set to <see cref="Color.Transparent"/>.</remarks>
		public static ColorPalette ConvertToPalette(Pltt[] resources)
		{
			ColorPalette palette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
			for (int i = 0; i < 256; i++) palette.Entries[i] = Color.Transparent;
			foreach (Pltt p in resources)
				for (int i = p.StartIndex; i <= p.EndIndex; i++)
					palette.Entries[i] = p.Entries[i];
			return palette;
		}
		#endregion public methods

		#region public properties
		/// <summary>Gets or sets the starting index of the color definitions</summary>
		/// <remarks>If StartIndex is set to a lower value, new colors are initialized to <see cref="Color.Black"/>. If being set to a higher value, unused colors are re-initialized to <see cref="Color.Transparent"/>.</remarks>
		/// <exception cref="ArgumentOutOfRangeException">Value is greater than <see cref="EndIndex"/></exception>
		public byte StartIndex
		{
			get { return _startIndex; }
			set
			{
				if (value > _endIndex) throw new ArgumentOutOfRangeException("value", "value cannot be greater than EndIndex");
				if (value < _startIndex) for (int i = value; i < _startIndex; i++) _entries[i] = Color.Black;
				else for (int i = _startIndex; i < value; i++) _entries[i] = Color.Transparent;
				_startIndex = value;
			}
		}
		/// <summary>Gets or sets the ending index of the color definitions</summary>
		/// <remarks>If EndIndex is set to a higher value, new colors are initialized to <see cref="Color.Black"/>. If being set to a lower value, unused colors are re-initialized to <see cref="Color.Transparent"/>.</remarks>
		/// <exception cref="ArgumentOutOfRangeException">Value is less than <see cref="StartIndex"/></exception>
		public byte EndIndex
		{
			get { return _endIndex; }
			set
			{
				if (value < _startIndex) throw new ArgumentOutOfRangeException("value", "value cannot be less than StartIndex");
				if (value > _endIndex) for (int i = _endIndex + 1; i <= value; i++) _entries[i] = Color.Black;
				else for (int i = value + 1; i <= _endIndex; i++) _entries[i] = Color.Transparent;
				_endIndex = value;
			}
		}
		/// <summary>Gets the total number of colors defined in the resource</summary>
		public byte NumberOfColors { get { return (byte)(_endIndex - _startIndex - 1); } }
		/// <summary>Gets the indexer for the colors</summary>
		/// <remarks>Unused colors are <see cref="Color.Transparent"/>.</remarks>
		public ColorIndexer Entries { get { return _colorIndexer; } }
		/// <summary>Gets the ColorPalette reprenstation of the resource</summary>
		/// <remarks>Indexes not used by the resource are <see cref="Color.Transparent"/>.</remarks>
		public ColorPalette Palette
		{
			get
			{
				ColorPalette pal = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
				for (int i = 0; i < _startIndex; i++) pal.Entries[i] = Color.Transparent;
				for (int i = _startIndex; i <= _endIndex; i++) pal.Entries[i] = _entries[i - _startIndex];
				for (int i = _endIndex + 1; i < 256; i++) pal.Entries[i] = Color.Transparent;
				return pal;
			}
		}
		#endregion public properties
	}
}
