/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2021 Michael Gaisser (mjgaisser@gmail.com)
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
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "PANL" cockpit image resources</summary>
	/// <remarks>The Panl resource contains the in-flight images.
	/// Everything from your targeting reticule to the Goal Summary screen are all stored in these resources.
	/// Many items are in separate *.PNL files, which this class can also edit.<br/>
	/// For images stored in LFD files, the resource contains a single image being one of the cockpit views (cockpit, left, Goal summary, etc).
	/// The <see cref="Pltt"/> in that file determines the colors used.<br/>
	/// The separate *.PNL files contain all of the individual images that make up your display and instrumentation (targeting reticule, shield indicator, etc).
	/// The palette is defined by the controlling cockpit LFD.</remarks>
	/// <example><h4>Raw Data definition</h4>
	/// <code>
	/// struct RawData
	/// {
	///   /* 0x00 */ Image[] Images;
	/// }
	/// 
	/// struct Image
	/// {
	///   /* 0x00 */ Row[] Rows;
	/// 			 byte EndImage = 0xFF;
	/// }
	/// 
	/// struct Row
	/// {
	///   /* 0x00 */ OpCode[] Operations;
	/// 			 byte EndRow = 0xFE;
	/// }
	///
	/// struct OpCode
	/// {
	///   /* 0x00 */ byte Value;
	///   #if (Value == 0xFD) // repeat Type 1
	///     /* 0x01 */ byte NumberOfRepeats;
	///     /* 0x02 */ byte ColorIndex;
	///   #elseif (Value == 0xFC) // repeat Type 2
	///     /* 0x01 */ byte ColorIndex;
	///     /* 0x02 */ byte NumberOfRepeats;
	///   #else // repeat Type 3 (short)
	///   #endif
	/// }</code>
	/// <para>This resource is a little annoying to work with, as the thing is entirely iterative.
	/// There are no properties aside from <i>Images</i> itself, so to decode the resource requires an iterative approach to determine the number of images and the individual image dimensions.
	/// Thankfully the use of the <i>EndImage</i> and <i>EndRow</i> markers (special OpCodes) make this possible.</para>
	/// <para>For the layout of the format there's not a lot to it.
	/// Each Image has the usual <i>Rows</i> array followed by the <i>EndImage</i> OpCode.
	/// Iterating through <i>RawData</i> and counting these markers gets you the number of images.<br/>
	/// Each Row has the usual <i>Operations</i> array and is terminated by the <i>EndRow</i> OpCode.
	/// Iterating through <i>Rows</i> and counting these markers gets you the height of the image.</para>
	/// <h4>-- OpCode --</h4>
	/// <para>The <i>Operations</i> array contains the information for a full row of pixels, starting from the top left.
	/// The OpCodes in the Panl resource are all different forms of repeat codes.
	/// Type 1 and Type 2 are effectively the same thing, except the value order is reversed, I don't know why.
	/// I learned to stop questioning why they did things like this, I just shake my head and move on. <see cref="EncodeResource"/> does not use Type 2.<br/>
	/// The important thing about <i>NumberOfRepeats</i> is that it is zero-indexed (hence <u>Repeats</u> instead of <u>Pixels</u>).
	/// A value of <b>0x00</b> means the pixel occurs once, <b>0x01</b> is twice, etc.</para>
	/// <para>For the Type 3 or Short code, it has a fixed Shift value of <b>0x02</b> (Shift is used primarily for the *.ACT/XACT and *.DAT image formats). What this means is that <i>Value</i> combines the <i>NumberOfRepeats</i> and <i>ColorIndex</i> values into a single byte using two bits for <i>NumberOfRepeats</i> and the remaining six for the <i>ColorIndex</i>.<br/>
	/// <code>
	/// Value = 0x93;	// b10010011
	/// 
	/// NumberOfRepeats = (Value &#038; 3);	// b00000011
	/// ColorIndex = (Value >> 2);	// b00100100</code></para>
	/// <para>Because of the Shift value, a <i>ColorIndex</i> of <b>0x3F</b> (b11111100) or higher cannot be used for Type 3 codes.
	/// <b>0x3F</b> would cause false OpCodes to be detected while <b>0x40</b> and higher uses more than six bits.</para></example>
	public partial class Panl : Resource
	{
		ColorPalette _palette = null;
		Bitmap[] _images;
		ImageIndexer _imageIndexer;
		bool _isPnl { get { return _fileName.ToUpper().EndsWith(".PNL"); } }

		#region constructors
		/// <summary>Blank constructor.</summary>
		/// <param name="forLfd">If <b>true</b> the Panl is meant to be in stored in a LFD file, otherwise a standalone .PNL file.</param>
		public Panl(bool forLfd)
		{
			_type = ResourceType.Panl;
			if (forLfd) _images = new Bitmap[1];
			else _images = new Bitmap[104];
			_imageIndexer = new ImageIndexer(this);
		}
		/// <summary>Creates a new instance from an existing opened file with default 256 color Palette.</summary>
		/// <param name="stream">The opened LFD or PNL file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Panl(FileStream stream, long filePosition)
		{
			read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an existing opened file with the supplied Palette.</summary>
		/// <param name="stream">The opened LFD or PNL file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <param name="palette">The colors used for the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Panl(FileStream stream, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file with default 256 color Palette.</summary>
		/// <param name="path">The full path to the unopened LFD or PNL file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Panl(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Creates a new instance from an exsiting file with the supplied Palette.</summary>
		/// <param name="path">The full path to the unopened LFD or PNL file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <param name="palette">The colors used for the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Panl(string path, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			FileStream stream = File.OpenRead(path);
			read(stream, filePosition);
			stream.Close();
		}
		#endregion constructors

		void read(FileStream stream, long filePosition)
		{
			try
			{
				_fileName = stream.Name;	// Resource._fileName: even though _process gets it, _isPnl needs it first
				if (!_isPnl) _process(stream, filePosition);
				else
				{
					BinaryReader br = new BinaryReader(stream);
					_offset = filePosition;	// Resource._offset
					_type = ResourceType.Panl;
					// *.PNL files do not contain headers, just the raw data
					_name = StringFunctions.GetFileName(_fileName);
					DecodeResource(br.ReadBytes((int)stream.Length), false);
				}
			}
			catch (Exception x) { throw new LoadFileException(x); }
			_imageIndexer = new ImageIndexer(this);
		}

		PanlInfo decodeImage(byte[] rawData, int imageIndex)
		{
			PanlInfo pi;
			short width = 0, height;
			for (int i=0;;)
			{
				if (rawData[i] == 0xFE || rawData[i] == 0xFF) break;
				else if (rawData[i] == 0xFD)
				{
					width += (short)(rawData[i + 1] + 1);
					i += 3;
				}
				else if (rawData[i] == 0xFC)
				{
					width += (short)(rawData[i + 2] + 1);
					i += 3;
				}
				else
				{
					width += (short)((rawData[i] & 3) + 1);
					i++;
				}
			}
			// ...and the mask height
			int pos = 0, x, y;
			pi.RawLength = 0;
			for (height = 0; pos < _rawData.Length; height++)
			{
				if (rawData[pos] == 0xFF) { pi.RawLength = (short)(pos+1); break; }
				if (rawData[pos] == 0xFE) { pos++; height--; continue; }
				for (x = 0; x < width; pos++)
				{
					if (rawData[pos] == 0xFD)
					{
						x += rawData[pos+1] + 1;
						pos += 2;
					}
					else if (rawData[pos] == 0xFC)
					{
						x += rawData[pos+2] + 1;
						pos += 2;
					}
					else x += (rawData[pos] & 3) + 1;
				}
			}
			// start ze image!
			pi.PixelData = new byte[(width % 4 == 0 ? width : width + 4 - width % 4) * height];	// Scan width is 4
			int px = 0;
			for (y = 0, pos = 0; y < height; y++)
			{
				for (;(px%4)!=0;) px++;
				if (rawData[pos] == 0xFE) pos++;
				for (x = 0; x < width; pos++)
				{
					if (rawData[pos] == 0xFD)
					{
						for (int i=0; i<=rawData[pos+1]; i++, px++) pi.PixelData[px] = rawData[pos+2];
						x += rawData[pos+1] + 1;
						pos += 2;
					}
					else if (rawData[pos] == 0xFC)
					{
						for (int i=0; i<=rawData[pos+2]; i++, px++) pi.PixelData[px] = rawData[pos+1];
						x += rawData[pos+2] + 1;
						pos += 2;
					}
					else
					{
						byte p = (byte)(rawData[pos] >> 2);
						for (int i=0; i<=(rawData[pos]&3); i++, px++) pi.PixelData[px] = p;
						x += (rawData[pos] & 3) + 1;
					}
				}
			}
			_images[imageIndex] = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
			if (_palette != null) _images[imageIndex].Palette = _palette;
			BitmapData bmdata = GraphicsFunctions.GetBitmapData(_images[imageIndex]);
			GraphicsFunctions.CopyBytesToImage(pi.PixelData, bmdata);
			_images[imageIndex].UnlockBits(bmdata);
			return pi;
		}
		
		#region public methods
		/// <summary>Processes raw data to populate the resource.</summary>
		/// <param name="raw">Raw byte data.</param>
		/// <param name="containsHeader">Whether or not <paramref name="raw"/> contains the resource Header information.</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> from the LFD is not <see cref="Resource.ResourceType.Panl"/>.</exception>
		/// <remarks>If resource was created from a *.PNL file, <i>containsHeader</i> is ignored.</remarks>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			if (!_isPnl)
			{
				_decodeResource(raw, containsHeader);
				if (_type != ResourceType.Panl) throw new ArgumentException("Raw header is not for a Panl resource");
				_images = new Bitmap[1];
				decodeImage(_rawData, 0);
			}
			else
			{
				_rawData = raw;
				int count = 0, offset, pos = 0;
				for (offset = 0; offset < _rawData.Length; ) if (_rawData[offset++] == 0xFF) count++;
				_images = new Bitmap[count];
				offset = 0;
				for (int i = 0; i < count; i++)
				{
					byte[] remainder = new byte[_rawData.Length - offset];
					ArrayFunctions.TrimArray(_rawData, offset, remainder);
					PanlInfo pi = decodeImage(remainder, i);
					pos += pi.RawLength;
					offset = pos;
				}
			}
		}

		/// <summary>Preparess the resource for writing and updates <see cref="Resource.RawData"/>.</summary>
		public override void EncodeResource()
		{
			//TODO: test Panl.Write()
			byte[] raw = null;
			for (int i = 0; i < _images.Length; i++)
			{
				BitmapData bd = GraphicsFunctions.GetBitmapData(_images[i]);
				byte[] pixels = new byte[bd.Stride * bd.Height];
				byte[] tempRaw = new byte[pixels.Length];
				int offset = 0;
				GraphicsFunctions.CopyImageToBytes(bd, pixels);	// from 0,0, index values
				int k;
				for (int y = 0; y < _images[i].Height; y++)
				{
					for (int x = 0, pos = y * bd.Stride; x < _images[i].Width; )
					{
						for (k = x + 1; k < _images[i].Width; k++)
							if (pixels[pos + x] != pixels[pos + k]) break;	// determine length of single color = k
						if ((k - x) < 5 && pixels[pos + x] < 0x3F)
						{
							// TYPE_3 : 1-4 px, can't use 3F since shift goes to FC-FF
							byte b = (byte)(pixels[pos + x] << 2 + (k - x - 1));
							tempRaw[offset++] = b;
						}
						else
						{
							// TYPE_1 : only going to use FD, since I see no reason to use FC at all
							tempRaw[offset++] = 0xFD;
							tempRaw[offset++] = (byte)(k - x - 1);
							tempRaw[offset++] = pixels[pos + x];
						}
						x = k;
					}
					tempRaw[offset++] = 0xFE;
				}
				tempRaw[offset++] = 0xFF;
				_images[i].UnlockBits(bd);
				byte[] temp = raw;
				raw = new byte[(temp == null ? 0 : temp.Length) + offset];
				if (temp != null) ArrayFunctions.WriteToArray(temp, raw, 0);
				Buffer.BlockCopy(tempRaw, 0, raw, raw.Length - offset, offset);
			}
			_rawData = raw;
		}
		#endregion public methods

		#region public properties
		/// <summary>Gets the indexer for the images.</summary>
		public ImageIndexer Images { get { return _imageIndexer; } }
		
		/// <summary>Gets the number of images contained within the resource.</summary>
		public int NumberOfImages { get { return _images.Length; } }

		/// <summary>Maximum allowable image width.</summary>
		/// <remarks>Value is <b>640</b>.</remarks>
		public const short MaximumWidth = 640;
		/// <summary>Maximum allowable image height.</summary>
		/// <remarks>Value is <b>480</b>.</remarks>
		public const short MaximumHeight = 480;
		#endregion public properties

		struct PanlInfo
		{
			public byte[] PixelData;
			public short RawLength;
		}
	}
}
