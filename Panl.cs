/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2011 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in Resource.cs
 * Version: 0.9
 */

/* CHANGELOG
 * 110922 - housekeeping, added added LoadFileException and SaveFileException throws, Write() return void
 * 111108 - added ArrayFunctions calls
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Reads LFD and PNL files and interprets cockpit PANL data</summary>
	/// <remarks>This resource is capable of reading *.PNL files in addition to those contained in LFD files</remarks>
	public class Panl : Resource
	{
		ColorPalette _palette = null;
		Bitmap[] _images;
		bool _isPnl { get { return _fileName.ToUpper().EndsWith(".PNL"); } }

		#region constructors
		public Panl()
		{
		}
		/// <summary>Create a new Panl instance from an existing opened file with default 8bpp Palette</summary>
		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Panl(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Create a new Panl instance from an existing opened file with the supplied Palette</summary>
		/// <param name="stream">This is the FileStream of the opened LFD or PNL file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Panl(FileStream stream, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			_read(stream, filePosition);
		}
		/// <summary>Create a new Panl instance from an existing opened file with the supplied Palette array</summary>
		/// <param name="stream">The FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palettes">The array of Pltts used to create the ColorPalette</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Panl(FileStream stream, long filePosition, Pltt[] palettes)
		{
			_palette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
			_read(stream, filePosition);
			SetPalette(palettes);
		}
		/// <summary>Create a new Panl instance from an existing file with default 8bpp Palette</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Panl(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Create a new Panl isntance from an existing file with the supplied Palette</summary>
		/// <param name="path">The full path to the unopened LFD or PNL file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Panl(string path, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Create a new Panl instance from an exsiting file with the supplied Palette array</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palettes">The array of Pltts used to create the ColorPalette</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Panl(string path, long filePosition, Pltt[] palettes)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
			SetPalette(palettes);
		}
		#endregion constructors

		void _read(FileStream stream, long filePosition)
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
		}

		//===================
		/// <summary>Processes raw data to create Panl information</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Header</param>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (!_isPnl)
			{
				_images = new Bitmap[1];
				decodeImage(_rawData, 0);
			}
			else
			{
				int count = 0, offset, pos = 0;
				for (offset = 0; offset < _rawData.Length; ) if (_rawData[offset++] == 0xFF) count++;
				_images = new Bitmap[count];
				offset = 0;
				for (int i = 0; i < count; i++)
				{
					byte[] remainder = new byte[_rawData.Length - offset];
					ArrayFunctions.TrimArray(_rawData, offset, remainder);
					panlInfo pi = decodeImage(remainder, i);
					pos += pi.RawLength;
					offset = pos;
				}
			}
		}

		/// <summary>Prepare Panl information for writing</summary>
		/// <returns>Raw data ready to write to file</returns>
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
						if ((k - x) < 5 && pixels[pos + x] != 0x3F)
						{
							// SMALL_REPEAT : 1-4 px, can't use 3F since shift goes to FC-FF
							byte b = (byte)(pixels[pos + x] << 2 + (k - x - 1));
							tempRaw[offset++] = b;
						}
						else
						{
							// REPEAT : only going to use FD, since I see no reason to use FC at all
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

		panlInfo decodeImage(byte[] rawData, int imageIndex)
		{
			panlInfo pi;
			short width = 0, height = 0;
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

		/// <summary>Set the image Palette</summary>
		/// <param name="palette">The Palette to be used</param>
		public void SetPalette(ColorPalette palette)
		{
			_palette = palette;
			for (int i = 0; i < _images.Length; i++) _images[i].Palette = _palette;
		}
		/// <summary>Set the image Palette</summary>
		/// <param name="palettes">The array from which to create the Palette</param>
		public void SetPalette(Pltt[] palettes)
		{
			_palette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
			foreach (Pltt p in palettes)
				for (int i = p.StartIndex; i <= p.EndIndex; i++)
					_palette.Entries[i] = p.Entries[i - p.StartIndex].Color;
			for (int i = 0; i < _images.Length; i++) _images[i].Palette = _palette;
		}

		/// <summary>Number of images contained within the resource</summary>
		public int NumberOfImages { get { return _images.Length; } }

		/// <summary>Sets the default Panl image</summary>
		/// <param name="image">Bitmap, converted to Format8bppIndexed, must be 640x480 or smaller</param>
		/// <exception cref="ArgumentException"></exception>
		public void SetImage(Bitmap image) { SetImage(image, 0); }
		/// <summary>Sets the indicated Panl image</summary>
		/// <param name="image">Bitmap, converted to Format8bppIndexed, must be 640x480 or smaller</param>
		/// <param name="index">PANL image index</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i> exceeds allowable dimensions</exception>
		/// <exception cref="IndexOutOfRangeException">Invalid <i>image</i> value</exception>
		public void SetImage(Bitmap image, int index)
		{
			if (!_isPnl) index = 0;
			if (image.Width > MaximumWidth) throw new BoundaryException("image.Width", MaximumWidth + "px max");
			if (image.Height > MaximumHeight) throw new BoundaryException("image.Height", MaximumHeight + "px max");
			Bitmap temp = _images[index];
			try { _images[index] = GraphicsFunctions.ConvertTo8bpp(image, _palette); }
			catch (Exception x) { _images[index] = temp; throw x; }
		}

		/// <summary>Gets 8bbp Indexed Bitmap of default Panl image</summary>
		public Bitmap GetImage() { return _images[0]; }
		/// <param name="index">PANL image index</param>
		/// <returns>8bbp Indexed Bitmap of indicated PANL image</returns>
		/// <exception cref="IndexOutOfRangeException"></exception>
		public Bitmap GetImage(int index)
		{
			if (!_isPnl) index = 0;
			return _images[index];
		}

		/// <summary>Maximum allowable image width</summary>
		public const short MaximumWidth = 640;
		/// <summary>Maximum allowable image height</summary>
		public const short MaximumHeight = 480;

		struct panlInfo
		{
			public byte[] PixelData;
			public short RawLength;
		}
	}
}
