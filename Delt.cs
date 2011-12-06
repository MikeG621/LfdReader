/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2011 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in Resource.cs
 * Version: 1.0
 */

/* CHANGELOG
 * 110829 - Additional Idmr.Common.Graphics implementations, added EnforceImageSize and EnforceLocation
 * 110908 - Left/Top validations
 * 110922 - added LoadFileException and SaveFileException throws, Write() return void
 * 110924 - implemented Decode/EncodeResource, added max dimensions
 * 111108 - added ArrayFunctions calls
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Reads LFD files and interprets DELT image types</summary>
	public class Delt : Resource
	{
		ColorPalette _palette = null;
		short _left = -1;
		short _top = -1;
		Bitmap _image = null;

		#region constructors
		/// <summary>Creates a blank Delt</summary>
		public Delt()
		{
			_type = ResourceType.Delt;
		}
		/// <summary>Create a new Delt instance from an existing opened file with default 8bpp Palette</summary>
		/// <param name="stream">The FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Create a new Delt instance from an existing opened file with the supplied Palette</summary>
		/// <param name="stream">The FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palette">The ColorPalette used for the DELT resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(FileStream stream, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			_read(stream, filePosition);
		}
		/// <summary>Create a new Delt instance from an existing opened file with the supplied Palette array</summary>
		/// <param name="stream">The FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palettes">The array of Pltts used to create the ColorPalette</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(FileStream stream, long filePosition, Pltt[] palettes)
		{
			_palette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
			_read(stream, filePosition);
			SetPalette(palettes);
		}
		/// <summary>Create a new Delt instance from an existing file with default 8bpp Palette</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Create a new Delt instance from an existing file with the supplied Palette</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palette">The ColorPalette used for the DELT resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(string path, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Create a new Delt instance from an exsiting file with the supplied Palette array</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palettes">The array of Pltts used to create the ColorPalette</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(string path, long filePosition, Pltt[] palettes)
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
				EnforceImageSize = false;
				EnforceLocation = false;
				_process(stream, filePosition);
			}
			catch (Exception x) { throw new LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to create Delt information</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Header</param>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			int offset = 0;
			_decodeResource(raw, containsHeader);
			_left = BitConverter.ToInt16(_rawData, offset);
			_top = BitConverter.ToInt16(_rawData, offset + 2);
			short width = (short)(BitConverter.ToInt16(_rawData, offset + 4) - _left + 1);
			short height = (short)(BitConverter.ToInt16(_rawData, offset + 6) - _top + 1);
			byte[] imageData = new byte[_rawData.Length - 8];
			ArrayFunctions.TrimArray(_rawData, offset + 8, imageData);
			try
			{
				_image = DecodeImage(_left, _top, width, height, imageData);
				if (_palette != null) _image.Palette = _palette;
			}
			catch { _image = ErrorImage; throw; }
		}

		/// <summary>Prepare Delt information for writing</summary>
		/// <returns>Raw data ready to write to file</returns>
		public override void EncodeResource()
		{
			byte[] image = EncodeImage(_image, _left, _top);
			byte[] raw = new byte[image.Length + 8];
			Buffer.BlockCopy(BitConverter.GetBytes(_left), 0, raw, 0, 2);
			Buffer.BlockCopy(BitConverter.GetBytes(_top), 0, raw, 2, 2);
			Buffer.BlockCopy(BitConverter.GetBytes((short)(_image.Width - 1)), 0, raw, 4, 2);
			Buffer.BlockCopy(BitConverter.GetBytes((short)(_image.Height - 1)), 0, raw, 6, 2);
			Buffer.BlockCopy(image, 0, raw, 8, image.Length);
			_rawData = raw;
		}

		/// <summary>Replaces the current image</summary>
		/// <param name="image">Must be 640x480 or smaller</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i> does not meet size requirements</exception>
		/// <exception cref="ArgumentException"><i>image</i> could not be converted to 8bpp Indexed</exception>
		public void SetImage(Bitmap image)
		{
			if (EnforceImageSize && image.Size != _image.Size) throw new BoundaryException("New image size must match existing image size");
			if (image.Width > MaximumWidth) throw new BoundaryException("image.Width", MaximumWidth + "px max");
			if (image.Height > MaximumHeight) throw new BoundaryException("image.Height", MaximumHeight + "px max");
			if (image.PixelFormat == PixelFormat.Format8bppIndexed && _palette == null) _palette = image.Palette;
			if (_palette == null) throw new ArgumentException("Palette must be created before image import");
			Bitmap temp = _image;
			try { _image = GraphicsFunctions.ConvertTo8bpp(image, _palette); }
			catch (Exception x) { _image = temp; throw new ArgumentException("Could not convert image to 8bpp", "image", x); }
		}
		
		/// <summary>Reads raw Delt-encoded image data and converts to a 256-color Bitmap</summary>
		/// <param name="left">The Delt or Anim.Frame Left value</param>
		/// <param name="top">The Delt or Anim.Frame Top value</param>
		/// <param name="width">The Delt or Anim.Frame Width value</param>
		/// <param name="height">The Delt or Anim.Frame Height value</param>
		/// <param name="rawData">Delt pixel data from the LFD</param>
		/// <returns>256-color indexed Bitmap with default palette</returns>
		public static Bitmap DecodeImage(short left, short top, short width, short height, byte[] rawData)
		{
			//System.Diagnostics.Debug.WriteLine("Image LTWH: " + left + ", " + top + ", " + width + ", " + height);
			try
			{
				int w = (width % 4 == 0 ? width : width + (4 - width % 4));	// w has to be a multiple of 4, round up
				byte[] pixels = new byte[w * height];
				for (int y = 0, pos = 0; y < height - 1; )
				{
					int l = BitConverter.ToInt16(rawData, pos); pos += 2;		// row data value
					bool compressed = Convert.ToBoolean(l % 2);	// get storage type
					l >>= 1;		// number of pixels in row
					if (l == 0) { l++; continue; }	// if we wind up at the end, try next row
					int x = BitConverter.ToInt16(rawData, pos) - left; pos += 2;	// row starting column
					y = BitConverter.ToInt16(rawData, pos) - top; pos += 2;	// starting row
					if (y < 0 || y >= height) System.Diagnostics.Debug.WriteLine("r " + y.ToString());	// Exception messages are there for debugging
					if (x < 0 || x >= width) System.Diagnostics.Debug.WriteLine("c " + x.ToString());
					int startCol = x;
					for (; x < l + startCol; )
					{
						byte b = rawData[pos++];
						if (((b % 2) == 1) && compressed)		// odd OP_CODE odd LENGTH = Repeat
						{
							b >>= 1;	// number of repeats
							byte p = rawData[pos++];
							for (int k = 0; k < b; k++, x++) pixels[y * w + x] = p;
						}
						else if (!compressed)	// even LENGTH = Straight Read
						{
							pos--;	// not actually an OP_CODE, need to read again
							for (int k = 0; k < l; k++, x++) pixels[y * w + x] = rawData[pos++];
						}
						else	// even OP_CODE = Read
						{
							b >>= 1;
							for (int k = 0; k < b; k++, x++) pixels[y * w + x] = rawData[pos++];
						}
					}
				}
				Bitmap image = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
				BitmapData bmdata = GraphicsFunctions.GetBitmapData(image);
				GraphicsFunctions.CopyBytesToImage(pixels, bmdata);
				image.UnlockBits(bmdata);
				return image;
			}
			catch { return Delt.ErrorImage; }
		}
		
		/// <summary>Converts image to compressed DELT data</summary>
		/// <param name="image">Bitmap to be encoded</param>
		/// <param name="left">Delt or Anim.Frame Left value</param>
		/// <param name="top">Delt or Anim.Frame Top value</param>
		/// <exception cref="ArgumentException"><i>image</i> is not PixelFormat.Format8bppIndexed</exception>
		/// <returns>Byte array of raw data ready to be written to file</returns>
		/// <remarks><i>image</i> must be 8bppIndexed</remarks>
		public static byte[] EncodeImage(Bitmap image, short left, short top)
		{
			if (image.PixelFormat != PixelFormat.Format8bppIndexed) throw new ArgumentException("image must be 8bppIndexed");
			BitmapData bd = GraphicsFunctions.GetBitmapData(image);
			byte[] pix = new byte[bd.Stride*bd.Height];
			GraphicsFunctions.CopyImageToBytes(bd, pix);
			image.UnlockBits(bd);	// don't actually need image anymore other than .Width
			byte[] tempRaw = new byte[312960];	// max length; no repeats, 640x480 (6 read operations req'ed for 640px plus type/left/top per row [640+6 + 3*2], times 480 rows)
			int w = bd.Stride;
			int k, t=0, pos=0;
			for (int y=0;y<image.Height;y++)
			{
				// okay, so BlockCopy lines; explicit short conversions into GetBytes returns byte[2], which BlockCopy uses as src to copy over into tempRaw[]
				Buffer.BlockCopy(BitConverter.GetBytes((short)((image.Width<<1)+1)), 0, tempRaw, pos, 2); pos += 2;	// full-width row, compressed data
				Buffer.BlockCopy(BitConverter.GetBytes(left), 0, tempRaw, pos, 2); pos += 2;	// row LEFT
				Buffer.BlockCopy(BitConverter.GetBytes((short)(y+top)), 0, tempRaw, pos, 2); pos += 2;	// row TOP
				t = y*w;
				for (int x=0;x<image.Width;)
				{
					for (k=x+1;k<image.Width;k++)
					{
						if (pix[t+x] != pix[t+k]) break;	// find next pixel that doesn't match current
						if ((k-x) == 127) break;	// max repeat, 127<<1+1 == 255
					}
					if ((k-x) >= 3)	// need 3+ for REPEAT to be effective
					{
						tempRaw[pos++] = (byte)(((k-x)<<1)+1);		// odd OP_CODE
						tempRaw[pos++] = pix[t+x];		// pixel to be repeated
					}
					else
					{
						for (k=x+1;k<image.Width;k++)	// determine length of straight read
						{
							if ((k-x) == 127) break;
							try { if (pix[t+k] != pix[t+k+1]) continue; }	// throws on end of row at k+1
							catch { k++; break; }
							// at this point, we're at two-in-a-row
							try { if (pix[t+k+1] != pix[t+k+2]) continue; }	// throws on end of row at k+2
							catch { k += 2; break; }
							// three-in-a-row, cut it off!
							break;
						}
						tempRaw[pos++] = (byte)((k-x)<<1);		// even OP_CODE
						Buffer.BlockCopy(pix,t+x,tempRaw, pos, k-x); pos += (k-x);
					}
					x = k;
				}
			}
			pos += 2;	// EOI short = 0, pos now equals the used length of tempRaw
			byte[] raw = new byte[pos];
			ArrayFunctions.TrimArray(tempRaw, 0, raw);	// copy all of the used data into the final, correctly-sized array
			return raw;
		}
		/// <summary>Converts the provided image to 256-colors using the given palette before converting to compressed DELT data</summary>
		/// <param name="image">Bitmap to be encoded</param>
		/// <param name="left">Delt or Anim.Frame Left value</param>
		/// <param name="top">Delt or Anim.Frame Top value</param>
		/// <param name="palette">ColorPalette to be used</param>
		/// <returns>Byte array of raw data ready to be written to file</returns>
		public static byte[] EncodeImage(Bitmap image, short left, short top, ColorPalette palette)
		{
			return EncodeImage(GraphicsFunctions.ConvertTo8bpp(image, palette), left, top);
		}

		/// <summary>Sets the image Palette</summary>
		/// <param name="palette">The Palette to be used</param>
		public void SetPalette(ColorPalette palette)
		{
			_palette = palette;
			if (_image != null) _image.Palette = _palette;
		}
		/// <summary>Sets the image Palette</summary>
		/// <param name="palettes">The array from which to create the Palette</param>
		public void SetPalette(Pltt[] palettes)
		{
			_palette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
			foreach (Pltt p in palettes)
				for (int i = p.StartIndex; i <= p.EndIndex; i++)
					_palette.Entries[i] = p.Entries[i - p.StartIndex].Color;
			if (_image != null) _image.Palette = _palette;
		}
		#endregion public methods
		
		#region public properties
		/// <summary>Gets the standard error image placeholder</summary>
		public static Bitmap ErrorImage
		{
			get
			{
				Bitmap bmError = new Bitmap(120, 120);
				Graphics g = Graphics.FromImage(bmError);
				g.DrawString("An error has\noccured and\nthe image can't\nbe processed", new System.Drawing.Font("Tahoma", 12), Brushes.Black, 0, 0);
				return bmError;
			}
		}
		
		/// <summary>Gets or sets the Left location</summary>
		/// <exception cref="ArgumentException">EnforceLocation is true, making value read-only</exception>
		/// <exception cref="Idmr.Common.BoundaryException"><i>value</i> results in portions of the image being located off-screen</exception>
		public short Left
		{
			get { return _left; }
			set
			{
				if (EnforceLocation) throw new ArgumentException("Value is currently read-only", "EnforceLocation");
				if (value < 0 || value >= (640 - _image.Width)) throw new BoundaryException("value", "0-" + (640-_image.Width).ToString());
				_left = value;
			}
		}

		/// <summary>Gets or sets the Top location</summary>
		/// <exception cref="ArgumentException">EnforceLocation is true, making value read-only</exception>
		/// <exception cref="Idmr.Common.BoundaryException"><i>value</i> results in portions of the image being located off-screen</exception>
		public short Top
		{
			get { return _top; }
			set
			{
				if (EnforceLocation) throw new ArgumentException("Value is currently read-only", "EnforceLocation");
				if (value < 0 || value >= (480 - _image.Width)) throw new BoundaryException("value", "0-" + (480-_image.Width).ToString());
				_top = value;
			}
		}
		
		/// <summary>Gets the image width</summary>
		public short Width { get { return (short)_image.Width; } }
		/// <summary>Gets the image height</summary>
		public short Height { get { return (short)_image.Height; } }

		/// <summary>Gets if the Delt palette has been defined</summary>
		public bool HasDefinedPalette { get { return _palette != null; } }

		/// <summary>Gets the Format8bppIndexed image</summary>
		public Bitmap Image { get { return _image; } }

		/// <summary>Fixes <i>Image.Size</i></summary>
		/// <remarks>Defaults to <i>false</i></remarks>
		public bool EnforceImageSize { get; set; }

		/// <summary>Sets <i>Left</i> and <i>Top</i> properties are read-only</summary>
		/// <remarks>Defaults to <i>false</i></remarks>
		public bool EnforceLocation { get; set; }

		/// <summary>Maximum allowable width of the image</summary>
		public const short MaximumWidth = 640;
		/// <summary>Maximum allowable height of the image</summary>
		public const short MaximumHeight = 480;
		#endregion public properties
	}
}
