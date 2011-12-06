/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2011 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in Resource.cs
 * Version: 0.9
 */

/* CHANGELOG
 * 110919 - housekeeping, added added LoadFileException and SaveFileException throws, Write() return void
 * 110927 - implemented Decode/EncodeResource()
 * 111108 - added ArrayFunctions calls
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Reads LFD files and interprets cockpit transparency MASK data</summary>
	public class Mask : Resource
	{
		// TODO: need constructors and Decode modifications to pre-define MASK width/height
		Bitmap _image;

		public Mask()
		{
		}
		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Mask(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Mask(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		
		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		//===================
		/// <summary>Processes raw data to create Mask information</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Header</param>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			int offset = 0;
			_decodeResource(raw, containsHeader);
			short width = 0, height;
			for (int i = 1 + offset; ; i++)	// get mask width
			{
				// TODO: check all MASK resources, check size
				if (_rawData[i] == 0) width += 0x100;
				else if (_rawData[i] == _rawData[offset]) break;	// TODO: currently assumes there's no 1px or 255px lengths in the top row, try to account for it or require W/H in cons()
				else width += _rawData[i];
			}
			int pos = offset, x, y;
			for (height = 0; pos < _rawData.Length; height++)	// get mask height
			{
				if (_rawData[pos] == 0) break;
				if (pos > 0x710) pos++;
				else pos++;
				for (x = 0; x < width; pos++)
				{
					if (_rawData[pos] == 0) { x += 0x100; if (x == width) pos++; }	// won't end on 00, so there's an extra pixel
					else x += _rawData[pos];
				}
			}
			// start ze image!
			_image = new Bitmap(width, height, PixelFormat.Format1bppIndexed);
			BitmapData bd = GraphicsFunctions.GetBitmapData(_image);
			bool draw;
			byte[] pixels = new byte[bd.Stride * bd.Height];
			for (y = 0, pos = offset, draw = false; y < height; y++, draw = false)
			{
				if (raw[pos] == 0xFF) draw = true;
				pos++;
				int len = 0;
				int w = bd.Stride * y;
				for (x = 0; x < width; pos++)
				{
					if (_rawData[pos] == 0) { len += 0x100; continue; }
					len += _rawData[pos];
					if (draw)
					{
						for (int x0 = x; x < x0 + len - 1; x++) pixels[w + x / 8] |= (byte)(0x80 >> (x & 7));
						if (x != width) pixels[w + x / 8] |= (byte)(0x80 >> (x & 7));	// will not fire for "throw away" pixel
						x++;
					}
					else x += len;
					draw = !draw;
					len = 0;
				}
			}
			GraphicsFunctions.CopyBytesToImage(pixels, bd);
			_image.UnlockBits(bd);
		}
		/// <summary>Prepare Mask information for writing</summary>
		/// <returns>Raw data ready to write to file</returns>
		public override void EncodeResource()
		{
			BitmapData bd = GraphicsFunctions.GetBitmapData(_image);
			byte[] pixels = new byte[bd.Stride * bd.Height];
			byte[] tempRaw = new byte[pixels.Length];
			// assuming 640 width, pixels[] is 80 bytes wide. for MASK to consume 80 bytes a row, it requires 79 color switches. Not happening
			// (although a pure alternating static mask would be 641 bytes wide, at that point wtf)
			GraphicsFunctions.CopyImageToBytes(bd, pixels);
			int len = 0;
			for (int y = 0; y < _image.Height; y++)
			{
				for (int x = 0, numBlack = 0, numWhite = 0, pos = y * bd.Stride; x < _image.Width; x++, len++)
				{
					byte shift = (byte)(0x80 >> (x & 7));
					if ((pixels[pos + x / 8] & shift) == shift)
					{	// white
						if (x == 0) tempRaw[len++] = 0xFF;
						if (numBlack != 0)
						{
							if (numBlack >= 512) len++;
							if (numBlack >= 256) len++;
							if ((numBlack & 0xFF) != 0) tempRaw[len] = (byte)(numBlack & 0xFF);
							else tempRaw[len] = 1;	// SetMask ensures this is a "throw away" pixel, effectively x=Width
						}
						numWhite++;
						numBlack = 0;
					}
					else
					{	// black
						if (x == 0) tempRaw[len++] = 1;
						if (numWhite != 0)
						{
							if (numWhite >= 512) len++;
							if (numWhite >= 256) len++;
							if ((numWhite & 0xFF) != 0) tempRaw[len] = (byte)(numWhite & 0xFF);
							else tempRaw[len] = 1;	// SetMask ensures this is a "throw away" pixel, effectively x=Width
						}
						numWhite = 0;
						numBlack++;
					}
				}
			}
			byte[] raw = new byte[len + 2];
			ArrayFunctions.TrimArray(tempRaw, 0, raw);
			_rawData = raw;
		}

		/// <summary>Sets the transparency mask</summary>
		/// <param name="image">New image mask. Converts to Format1bppIndexed, must be 640x480 or smaller</param>
		/// <exception cref="ArgumentException"><i>image</i> contains a consecutive length of 256 or 512 pixels not located at the end of the image</exception>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i> exceeds maximum allowable dimensions</exception>
		/// <remarks>The MASK format cannot handle the 256 or 512px lengths in the middle of the image. It can however do that at the end of the image, via "throw away" pixels</remarks>
		public void SetMask(Bitmap image)
		{
			SetMask(image, Color.FromArgb(0,0,0));
		}
		/// <summary>Sets the transparency mask</summary>
		/// <param name="image">New image mask. Converts to Format1bppIndexed, must be 640x480 or smaller</param>
		/// <param name="transparentColor">The Color to be used as transparent</param>
		/// <exception cref="ArgumentException"><i>image</i> contains a consecutive length of 256 or 512 pixels not located at the end of the image</exception>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i> exceeds maximum allowable dimensions</exception>
		/// <remarks>The MASK format cannot handle the 256 or 512px lengths in the middle of the image. It can however do that at the end of the image, via "throw away" pixels</remarks>
		public void SetMask(Bitmap image, Color transparentColor)
		{
			string message = "Image contains line length of 256 or 512 pixels";
			if (image.Width > MaximumWidth || image.Height > MaximumHeight) throw new Common.BoundaryException("image.Size", MaximumWidth + "x" + MaximumHeight);
			Bitmap temp = _image;
			try
			{
				// due to special length checks, can't use Common.Functions.ConvertTo1bpp
				image = new Bitmap(image);	// force to 32bbpRGB
				_image = new Bitmap(image.Width, image.Height, PixelFormat.Format1bppIndexed);
				// import image data
				BitmapData bd32 = GraphicsFunctions.GetBitmapData(image);
				byte[] pix32 = new byte[bd32.Stride * bd32.Height];
				GraphicsFunctions.CopyImageToBytes(bd32, pix32);
				BitmapData bd1 = GraphicsFunctions.GetBitmapData(_image);
				byte[] pix1 = new byte[bd1.Stride * bd1.Height];
				for (int y = 0; y < image.Height; y++)
					for (int x = 0, numBlack = 0, numWhite = 0, pos32 = y * bd32.Stride, pos1 = y * bd1.Stride; x < bd32.Width; x++)
						if (pix32[pos32 + x * 4] != transparentColor.B || pix32[pos32 + x * 4 + 1] != transparentColor.G || pix32[pos32 + x * 4 + 2] != transparentColor.R)
						{	// white
							pix1[pos1 + x / 8] |= (byte)(0x80 >> (x & 7));
							// throw if 256px detected, and not end of row (which format can handle)
							if ((numBlack % 256) == 0 && x != image.Width - 1) throw new ArgumentException(message, "image");
							numBlack = 0;
							numWhite++;
						}
						else
						{	// black
							if ((numWhite % 256) == 0 && x != image.Width - 1) throw new ArgumentException(message, "image");
							numBlack++;
							numWhite = 0;
						}
				GraphicsFunctions.CopyBytesToImage(pix1, bd1);
				image.UnlockBits(bd32);
				_image.UnlockBits(bd1);
			}
			catch (Exception x) { _image = temp; throw x; }
		}

		/// <summary>Gets the monochrome mask image</summary>
		public Bitmap Image { get { return _image; } }
		/// <summary>Gets the width of the Mask image</summary>
		public short Width { get { return (short)_image.Width; } }
		/// <summary>Gets the height of the Mask image</summary>
		public short Height { get { return (short)_image.Height; } }
		
		/// <summary>The maximum allowable width</summary>
		public const short MaximumWidth = 640;
		/// <summary>The maximum allowable height</summary>
		public const short MaximumHeight = 480;
	}
}
