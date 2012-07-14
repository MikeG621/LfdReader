/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2012 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.0
 */

/* CHANGELOG
 * 110919 - housekeeping, added added LoadFileException and SaveFileException throws, Write() return void
 * 110927 - implemented Decode/EncodeResource()
 * 111108 - added ArrayFunctions calls
 * 120402 - redid W/H, added cons to pre-define them to save processing during Decode
 * 120425 - ResourceType check
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "MASK" cockpit transparency resources</summary>
	/// <remarks>The Mask resource controls the windows for the in-flight cockpits. Sloppy application of the Mask can result in stars being visible through the solid parts of your cockpit, much like how the snowspeeder cockpits in Empire Strikes Back were transparent!<br/>
	/// Images are monochrome, white is solid and black is transparent. Maximum dimensions are controlled by <see cref="Panl"/>.<hr/>
	/// <h4>Raw Data definition</h4>
	/// <code>// Pseudo-code resource structure
	/// struct RawData
	/// {
	///   /* 0x00 */ Row[] Rows;
	/// }
	/// 
	/// struct Row
	/// {
	///   #if (first pixel is solid)
	///     /* 0x00 */ byte FirstColor = 0xFF;	// solid
	///   #else
	///     /* 0x00 */ byte FirstColor = 0x01;	// transparent
	///   #endif
	///   /* 0x01 */ byte[] Lengths;
	/// }
	/// }</code>
	/// If you know the dimensions of the Mask before you start, that's great. If not, it can be determined iteratively, however there is some degree of error.<br/><br/>
	/// The iterative method currently used by <see cred="DecodeResource"/> when the dimensions are unknown assumes the first two rows start with the same state (solid/transparent) and the top row does not have any Length values that equal <i>FirstColor</i>. It's not perfect, but it seems to work fine with the stock cockpits. Results may vary with custom cockpits.<br/>
	/// Quickly iterating through <i>Rows</i> using the width value until the end of RawData or until a Row starts with <b>0x00</b> will determine the height. Of course, if you know the dimensions beforehand, that would be for the best.<br/><br/>
	/// stuff</remarks>
	public class Mask : Resource
	{
		Bitmap _image = null;

		#region constructors
		/// <summary>Blank constructor</summary>
		/// <remarks><see cref="Image"/> is <b>null</b></remarks>
		public Mask()
		{
			_type = ResourceType.Mask;
		}
		/// <summary>Creates a new instance from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Mask(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Mask(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Creates a new instance from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="width">Predefined width of the mask</param>
		/// <param name="height">Predefined height of the mask</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>width</i> or <i>height</i> exceed maximum allowable dimensions</exception>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Mask(FileStream stream, long filePosition, short width, short height)
		{
			if (width > Panl.MaximumWidth || height > Panl.MaximumHeight) throw new Common.BoundaryException("dimensions", Panl.MaximumWidth + "x" + Panl.MaximumHeight);
			Width = width;
			Height = height;
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="width">Predefined width of the mask</param>
		/// <param name="height">Predefined height of the mask</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>width</i> or <i>height</i> exceed maximum allowable dimensions</exception>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Mask(string path, long filePosition, short width, short height)
		{
			if (width > Panl.MaximumWidth || height > Panl.MaximumHeight) throw new Common.BoundaryException("dimensions", Panl.MaximumWidth + "x" + Panl.MaximumHeight);
			Width = width;
			Height = height;
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Creates a new instance from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="dimensions">Predefined size of the mask</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>dimensions</i> exceeds maximum allowable dimensions</exception>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Mask(FileStream stream, long filePosition, Size dimensions)
		{
			if (dimensions.Width > Panl.MaximumWidth || dimensions.Height > Panl.MaximumHeight) throw new Common.BoundaryException("dimensions", Panl.MaximumWidth + "x" + Panl.MaximumHeight);
			Width = (short)dimensions.Width;
			Height = (short)dimensions.Height;
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="dimensions">Predefined size of the mask</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>dimensions</i> exceeds maximum allowable dimensions</exception>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Mask(string path, long filePosition, Size dimensions)
		{
			if (dimensions.Width > Panl.MaximumWidth || dimensions.Height > Panl.MaximumHeight) throw new Common.BoundaryException("dimensions", Panl.MaximumWidth + "x" + Panl.MaximumHeight);
			Width = (short)dimensions.Width;
			Height = (short)dimensions.Height;
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
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
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="ResourceType.Mask"/></exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Mask) throw new ArgumentException("Raw header is not for a Mask resource");
			if (Width == 0)
				for (int i = 1; ; i++)	// get mask width
				{
					// TODO: check all MASK resources, check size
					if (_rawData[i] == 0) Width += 0x100;
					else if (_rawData[i] == _rawData[0]) break;	// TODO: currently assumes there's no 1px or 255px lengths in the top row, try to account for it
					else Width += _rawData[i];
				}
			int pos = 0, x, y;
			if (Height == 0)
				for (Height = 0; pos < _rawData.Length; Height++)	// get mask height
				{
					if (_rawData[pos] == 0) break;
					/*if (pos > 0x710) pos++;
					else pos++;*/
					for (x = 0; x < Width; pos++)
					{
						if (_rawData[pos] == 0) { x += 0x100; if (x == Width) pos++; }	// won't end on 00, so there's an extra pixel
						else x += _rawData[pos];
					}
				}
			// start ze image!
			_image = new Bitmap(Width, Height, PixelFormat.Format1bppIndexed);
			BitmapData bd = GraphicsFunctions.GetBitmapData(_image);
			bool draw;
			byte[] pixels = new byte[bd.Stride * bd.Height];
			for (y = 0, pos = 0, draw = false; y < Height; y++, draw = false)
			{
				if (raw[pos] == 0xFF) draw = true;
				pos++;
				int len = 0;
				int w = bd.Stride * y;
				for (x = 0; x < Width; pos++)
				{
					if (_rawData[pos] == 0) { len += 0x100; continue; }
					len += _rawData[pos];
					if (draw)
					{
						for (int x0 = x; x < x0 + len - 1; x++) pixels[w + x / 8] |= (byte)(0x80 >> (x & 7));
						if (x != Width) pixels[w + x / 8] |= (byte)(0x80 >> (x & 7));	// will not fire for "throw away" pixel
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
		/// <summary>Prepares the resource for writing and updates <see cref="RawData"/></summary>
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
		/// <param name="image">New image mask. Converts to <see cref="PixelFormat.Format1bppIndexed"/>, must be <b>640x480</b> or smaller</param>
		/// <exception cref="ArgumentException"><i>image</i> contains a consecutive length of 256 or 512 pixels not located at the end of the image</exception>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i> exceeds maximum allowable dimensions</exception>
		/// <remarks>The MASK format cannot handle the 256 or 512px lengths in the middle of the image. It can however do that at the end of the image, via "throw away" pixels.<br/>
		/// Maximum size is defined by <see cref="Panl.MaximumWidth"/> and <see cref="Panl.MaximumHeight"/></remarks>
		public void SetMask(Bitmap image)
		{
			SetMask(image, Color.FromArgb(0,0,0));
		}
		/// <summary>Sets the transparency mask</summary>
		/// <param name="image">New image mask. Converts to <see cref="PixelFormat.Format1bppIndexed"/>, must be <b>640x480</b> or smaller</param>
		/// <param name="transparentColor">The Color to be used as transparent</param>
		/// <exception cref="ArgumentException"><i>image</i> contains a consecutive length of 256 or 512 pixels not located at the end of the image</exception>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i> exceeds maximum allowable dimensions</exception>
		/// <remarks>The MASK format cannot handle the 256 or 512px lengths in the middle of the image. It can however do that at the end of the image, via "throw away" pixels.<br/>
		/// Maximum size is defined by <see cref="Panl.MaximumWidth"/> and <see cref="Panl.MaximumHeight"/></remarks>
		public void SetMask(Bitmap image, Color transparentColor)
		{
			string message = "Image contains line length of 256 or 512 pixels";
			if (image.Width > Panl.MaximumWidth || image.Height > Panl.MaximumHeight) throw new Common.BoundaryException("image.Size", Panl.MaximumWidth + "x" + Panl.MaximumHeight);
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
				Width = (short)_image.Width;
				Height = (short)_image.Height;
			}
			catch (Exception x) { _image = temp; throw x; }
		}
		#endregion public methods
		
		#region public properties
		/// <summary>Gets the monochrome mask image</summary>
		public Bitmap Image { get { return _image; } }
		/// <summary>Gets the width of the Mask image</summary>
		public short Width { get; internal set; }
		/// <summary>Gets the height of the Mask image</summary>
		public short Height { get; internal set; }
		#endregion public properties
	}
}
