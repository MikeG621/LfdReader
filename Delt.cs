/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2012 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.0
 */

/* CHANGELOG
 * 110829 - Additional Idmr.Common.Graphics implementations, added EnforceImageSize and EnforceLocation
 * 110908 - Left/Top validations
 * 110922 - added LoadFileException and SaveFileException throws, Write() return void
 * 110924 - implemented Decode/EncodeResource, added max dimensions
 * 111108 - added ArrayFunctions calls
 * 120329 - added ArrayFunctions calls
 * 120412 - add Palette prop, rem SetPalette()
 * 120425 - ResourceType check
 * 120523 - SetImage() to Image.set
 * 120524 - Left/Top/Image.set update _right/_bottom, InvalidOpX in Image.set
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "DELT" image resources</summary>
	/// <remarks>The Delt resource is the standard 256-color bitmap format used for outside the flight engine (cutscenes, concourses, main menu). It is the basis for the <see cref="Anim"/> resource. The <see cref="Pltt">Pltts</see> used are defined by the active <see cref="Film"/> resource.<hr/>
	/// <h4>Raw Data definition</h4>
	/// <code>// Pseudo-code resource structure
	/// struct RawData
	/// {
	///   /* 0x00 */ short Left;
	///   /* 0x02 */ short Top;
	///   /* 0x04 */ short Right;
	///   /* 0x06 */ short Bottom;
	///   /* 0x08 */ short Row[] Rows;
	///   /* 0x?? */ short Reserved = 0x00;
	/// }
	/// 
	/// struct Row
	/// {
	///   /* 0x00 */ short Length;
	///   /* 0x02 */ short Left;
	///   /* 0x04 */ short Top;
	///   // if (Length &#038; 1 == 0)	[straight read, long]
	///     /* 0x06 */ byte[Length] ColorIndexes;
	///   // else
	///     /* 0x06 */ OpCode[] Operations;
	///   // endif
	/// }
	///
	/// struct OpCode
	/// {
	///	  /* 0x00 */ byte Value;
	///	  // if (Value &#038; 1 == 0)	[straight read, short]
	///	    /* 0x01 */ byte[Value / 2] ColorIndexes;
	///	  // else	[color repeat]
	///	    /* 0x01 */ byte ColorIndex;
	///	  // endif
	/// }</code>
	/// In the beginning of <i>RawData</i>, we have the four values that define the outline of the image data. The Width and Height are derived values that are the difference plus one (to get from zero-indexed to true size).<br/><br/>
	/// -- Row --<br/><br/>
	/// Rows read from the top down, left to right. The first value for a given row is <i>Length</i>, which gives the number of pixels defined in that row, which can be defined in two different ways. If <i>Length</i> is even, then the entire set of row definitions are simply uncompressed indexed values (long read). If <i>Length</i> is odd, then the <i>Operations</i> array and everything after this paragraph applies. In either case, the <i>Length</i> value is evaluated to <c>(Length >> 1)</c> pixels. The use of <i>Row.Left</i> is used when "broken" images are used, creating blank spots in the image when overlaps with other resources are required. An example of this would be the galaxy image in the Tour of Duty screen. On the map itself, there is a blank spot where the officer's head is. Note that because of broken rows, it is possible to have two Row declarations with the same <i>Row.Top</i> value. <see cref="EncodeResource"/> does not permit the creation of broken rows, instead it will use transparent (<i>ColorIndex</i> = <b>0x00</b>) for the "missing" pixels. For Rows that do not have blank spots, <i>Length</i> is typically the full width of the image and <i>Row.Left</i> will match <i>Delt.Left</i>. For a long read Row, after it is positioned each pixel in the Row is assigned an index value to the palette that has been defined for that image.<br/><br/>
	/// For Rows that require <i>Operations</i>, there are two different types of OpCodes which are distinguished by the first-order bit. An odd <i>Value</i> is a  Repeat instruction, while an even <i>Value</i> is a Read instruction. The <i>Operations</i> array continues until the number of pixels processed equals <i>Row.Length</i>.<br/><br/>
	/// -- Repeat OpCode --<br/><br/>
	/// This operation takes one parameter, <i>ColorIndex</i>. The number of occurances is calculated by <c>(Value >> 1)</c>, such that<br/>
	/// 0x07	Occurs three times<br/>
	/// 0x09	Occurs four times<br/>
	/// 0x0B	Occurs five times<br/>
	/// ...<br/><br/>
	/// -- Read OpCode --<br/><br/>
	/// This OpCode instructs the application to read a given number of pixels and translate them directly to the image. The number of <i>ColorIndexes</i> to be read is given by (Value >> 1), such that<br/>
	/// 0x04	Reads two pixels<br/>
	/// 0x06	Reads three pixels<br/>
	/// 0x08	Reads four pixels<br/>
	/// ...<br/><br/>
	/// After the final pixel for either operation, the next <c>byte</c> will be another OpCode.</remarks>
	public class Delt : Resource
	{
		ColorPalette _palette = null;
		short _left = -1;
		short _top = -1;
		short _right = -1;
		short _bottom = -1;
		Bitmap _image = null;

		#region constructors
		/// <summary>Creates a blank Delt</summary>
		public Delt()
		{
			_type = ResourceType.Delt;
		}
		/// <summary>Creates a new instance from an existing opened file with default 8bpp Palette</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an existing opened file with the supplied Palette</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="palette">The colors used for the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(FileStream stream, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an existing opened file with the supplied Palette array</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="palettes">The colors used for the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(FileStream stream, long filePosition, Pltt[] palettes)
		{
			_palette = Pltt.ConvertToPalette(palettes);
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an existing file with default 8bpp Palette</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Creates a new instance from an existing file with the supplied Palette</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="palette">The colors used for the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(string path, long filePosition, ColorPalette palette)
		{
			_palette = palette;
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		/// <summary>Creates a new instance from an exsiting file with the supplied Palette array</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <param name="palettes">The colors used for the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Delt(string path, long filePosition, Pltt[] palettes)
		{
			_palette = Pltt.ConvertToPalette(palettes);
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
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="ResourceType.Delt"/></exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Delt) throw new ArgumentException("Raw header is not for a Delt resource");
			_left = BitConverter.ToInt16(_rawData, 0);
			_top = BitConverter.ToInt16(_rawData, 2);
			_right = BitConverter.ToInt16(_rawData, 4);
			_bottom = BitConverter.ToInt16(_rawData, 6);
			//System.Diagnostics.Debug.WriteLine("Image LTWH: " + _left + ", " + _top + ", " + Width + ", " + Height);
			byte[] imageData = new byte[_rawData.Length - 8];
			ArrayFunctions.TrimArray(_rawData, 8, imageData);
			try
			{
				_image = DecodeImage(_left, _top, Width, Height, imageData);
				if (HasDefinedPalette) _image.Palette = _palette;
			}
			catch { _image = ErrorImage; throw; }
		}

		/// <summary>Prepares the resource for writing and updates <see cref="RawData"/></summary>
		public override void EncodeResource()
		{
			byte[] image = EncodeImage(_image, _left, _top);
			byte[] raw = new byte[image.Length + 8];
			ArrayFunctions.WriteToArray(_left, raw, 0);
			ArrayFunctions.WriteToArray(_top, raw, 2);
			ArrayFunctions.WriteToArray(_right, raw, 4);
			ArrayFunctions.WriteToArray(_bottom, raw, 6);
			ArrayFunctions.WriteToArray(image, raw, 8);
			_rawData = raw;
		}

		/// <summary>Reads raw Delt-encoded image data and converts to a 256-color Bitmap</summary>
		/// <param name="left"><see cref="Delt.Left">Delt.Left</see> or <see cref="Anim.Frame.Left">Anim.Frame.Left</see></param>
		/// <param name="top"><see cref="Delt.Top">Delt.Top</see> or <see cref="Anim.Frame.Top">Anim.Frame.Top</see></param>
		/// <param name="width"><see cref="Delt.Width">Delt.Width</see> or <see cref="Anim.Frame.Width">Anim.Frame.Width</see></param>
		/// <param name="height"><see cref="Delt.Height">Delt.Height</see> or <see cref="Anim.Frame.Height">Anim.Frame.Height</see></param>
		/// <param name="rawData">Encoded pixel data from the LFD</param>
		/// <returns>256-color indexed Bitmap with default palette, <see cref="ErrorImage"/> on error</returns>
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
					if (y < 0 || y >= height) System.Diagnostics.Debug.WriteLine("r " + y.ToString());
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
		/// <param name="left"><see cref="Delt.Left">Delt.Left</see> or <see cref="Anim.Frame.Left">Anim.Frame.Left</see></param>
		/// <param name="top"><see cref="Delt.Top">Delt.Top</see> or <see cref="Anim.Frame.Top">Anim.Frame.Top</see></param>
		/// <exception cref="ArgumentException"><i>image</i> is not <see cref="PixelFormat.Format8bppIndexed"/></exception>
		/// <returns>Encoded byte array of raw data ready to be written to file</returns>
		/// <remarks><i>image</i> must be <see cref="PixelFormat.Format8bppIndexed"/></remarks>
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
				ArrayFunctions.WriteToArray((short)((image.Width << 1) + 1), tempRaw, pos);	// full-width row, compressed data
				ArrayFunctions.WriteToArray(left, tempRaw, pos);	// row LEFT
				ArrayFunctions.WriteToArray((short)(y + top), tempRaw, pos);	// row TOP
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
						Buffer.BlockCopy(pix, t+x, tempRaw, pos, k-x); pos += (k-x);
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
		/// <param name="left"><see cref="Delt.Left">Delt.Left</see> or <see cref="Anim.Frame.Left">Anim.Frame.Left</see></param>
		/// <param name="top"><see cref="Delt.Top">Delt.Top</see> or <see cref="Anim.Frame.Top">Anim.Frame.Top</see></param>
		/// <param name="palette">ColorPalette to be used</param>
		/// <returns>Encoded byte array of raw data ready to be written to file</returns>
		public static byte[] EncodeImage(Bitmap image, short left, short top, ColorPalette palette)
		{
			return EncodeImage(GraphicsFunctions.ConvertTo8bpp(image, palette), left, top);
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
		/// <exception cref="InvalidOperationException"><see cref="EnforceLocation"/> is true, making <i>Left</i> read-only</exception>
		/// <exception cref="Idmr.Common.BoundaryException"><i>Left</i> results in portions of the image being located off-screen</exception>
		public short Left
		{
			get { return _left; }
			set
			{
				if (EnforceLocation) throw new InvalidOperationException("Value is currently read-only");
				if (value < 0 || value >= (MaximumWidth - _image.Width)) throw new BoundaryException("Left", "0-" + (MaximumWidth-_image.Width));
				_left = value;
				_right = (short)(Left + _image.Width - 1);
			}
		}

		/// <summary>Gets or sets the Top location</summary>
		/// <exception cref="InvalidOperationException"><see cref="EnforceLocation"/> is true, making <i>Top</i> read-only</exception>
		/// <exception cref="Idmr.Common.BoundaryException"><i>Top</i> results in portions of the image being located off-screen</exception>
		public short Top
		{
			get { return _top; }
			set
			{
				if (EnforceLocation) throw new InvalidOperationException("Value is currently read-only");
				if (value < 0 || value >= (MaximumHeight - _image.Width)) throw new BoundaryException("Top", "0-" + (MaximumHeight-_image.Width));
				_top = value;
				_bottom = (short)(Top + _image.Height - 1);
			}
		}
		
		/// <summary>Gets the image width</summary>
		public short Width { get { return (short)(_right - _left + 1); } }
		/// <summary>Gets the image height</summary>
		public short Height { get { return (short)(_bottom - _top + 1); } }

		/// <summary>Gets if the Delt palette has been defined</summary>
		public bool HasDefinedPalette { get { return _palette != null; } }

		/// <summary>Gets or sets the <see cref="PixelFormat.Format8bppIndexed"/> image</summary>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image</i> does not meet size requirements</exception>
		/// <exception cref="ArgumentException"><i>Image</i> is not <see cref="PixelFormat.Format8bppIndexed"/> and <see cref="Palette"/> is undefined</exception>
		/// <exception cref="ArgumentException"><i>Image</i> could not be converted to <see cref="PixelFormat.Format8bppIndexed"/></exception>
		/// <remarks><i>Image.Size</i> must be <b>640x480</b> or smaller.</remarks>
		public Bitmap Image
		{
			get { return _image; }
			set
			{
				if (EnforceImageSize && value.Size != _image.Size) throw new BoundaryException("New image size must match existing image size");
				if (Left + value.Width > MaximumWidth || Top + value.Height > MaximumHeight)
					throw new BoundaryException("Image.Size", (MaximumWidth - Left) + "x" + (MaximumHeight - Top) + " max");
				if (value.PixelFormat == PixelFormat.Format8bppIndexed && !HasDefinedPalette) _palette = value.Palette;
				if (!HasDefinedPalette) throw new InvalidOperationException("Image is not Format8bppIndexed and Palette is undefined");
				Bitmap temp = _image;
				try { _image = GraphicsFunctions.ConvertTo8bpp(value, _palette); }
				catch (Exception x) { _image = temp; throw new ArgumentException("Could not convert image to 8bpp", "Image", x); }
				_right = (short)(Left + _image.Width - 1);
				_bottom = (short)(Top + _image.Height - 1);
			}
		}

		/// <summary>Gets ot sets the palette for the Delt</summary>
		public ColorPalette Palette
		{
			get { return _palette; }
			set
			{
				_palette = value;
				if (_image != null) _image.Palette = _palette;
			}
		}
		/// <summary>Fixes <see cref="Width"/> and <see cref="Height"/></summary>
		/// <remarks>Defaults to <b>false</b></remarks>
		public bool EnforceImageSize { get; set; }

		/// <summary>Sets <see cref="Left"/> and <see cref="Top"/> properties to read-only</summary>
		/// <remarks>Defaults to <b>false</b></remarks>
		public bool EnforceLocation { get; set; }

		/// <summary>Maximum allowable width of the image</summary>
		/// <remarks>Value is <b>640</b></remarks>
		public const short MaximumWidth = 640;
		/// <summary>Maximum allowable height of the image</summary>
		/// <remarks>Value is <b>480</b></remarks>
		public const short MaximumHeight = 480;
		#endregion public properties
	}
}
