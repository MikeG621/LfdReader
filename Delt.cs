/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010 Michael Gaisser (mjgaisser@gmail.com)
 * 
 * Full notice in Resource.cs
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Idmr.LfdReader
{
	/// <remarks>
	/// Reads LFD files and interprets DELT image types
	/// </remarks>
	public class Delt : Resource
	{
		private ColorPalette _palette;
		private short _left = -1;
		private short _top = -1;
		private Bitmap _image;

		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="deltPalette">The ColorPalette used for the DELT resource</param>
		public Delt(FileStream stream, long filePosition, ColorPalette deltPalette)
		{
			Read(stream, filePosition, deltPalette);
		}
		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="deltPalette">The ColorPalette used for the DELT resource</param>
		/// <param name="animX">The X value of the parent ANIM</param>
		/// <param name="animY">The Y value of the parnet ANIM</param>
		public Delt(FileStream stream, long filePosition, ColorPalette deltPalette, short animX, short animY)
		{
			_left = animX;
			_top = animY;
			Read(stream, filePosition, deltPalette);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="deltPalette">The ColorPalette used for the DELT resource</param>
		public Delt(string path, long filePosition, ColorPalette deltPalette)
		{
			FileStream stream = File.OpenRead(path);
			Read(stream, filePosition, deltPalette);
			stream.Close();
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="deltPalette">The ColorPalette used for the DELT resource</param>
		/// <param name="animX">The X value of the parent ANIM</param>
		/// <param name="animY">The Y value of the parnet ANIM</param>
		public Delt(string path, long filePosition, ColorPalette deltPalette, short animX, short animY)
		{
			_left = animX;
			_top = animY;
			FileStream stream = File.OpenRead(path);
			Read(stream, filePosition, deltPalette);
			stream.Close();
		}
		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palettes">The array of Pltts used to create the ColorPalette</param>
		public Delt(FileStream stream, long filePosition, Pltt[] palettes)
		{
			_palette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
			foreach (Pltt p in palettes)
				for (int i = p.StartIndex; i <= p.EndIndex; i++)
					_palette.Entries[i] = p.Entries[i - p.StartIndex].Color;
			Read(stream, filePosition, _palette);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <param name="palettes">The array of Pltts used to create the ColorPalette</param>
		public Delt(string path, long filePosition, Pltt[] palettes)
		{
			FileStream stream = File.OpenRead(path);
			_palette = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
			foreach (Pltt p in palettes)
				for (int i = p.StartIndex; i <= p.EndIndex; i++)
					_palette.Entries[i] = p.Entries[i - p.StartIndex].Color;
			Read(stream, filePosition, _palette);
			stream.Close();
		}
		
		private void Read(FileStream stream, long filePosition, ColorPalette deltPalette)
		{
			BinaryReader br = new BinaryReader(stream);
			_palette = deltPalette;
			_fileName = stream.Name;	// Resource._fileName
			_offset = filePosition;	// Resource._offset
			stream.Position = _offset + NameOffset;
			_name = new string(br.ReadChars(8)).Trim('\0');	// Resource._name
			stream.Position = _offset + LengthOffset;		// ANIM botches the name, needs explicit position reset
			_length = br.ReadInt32();	// Resource._length
			if (_left == -1) _left = br.ReadInt16(); else stream.Position += 2;	// this takes into account the Anim assignments
			if (_top == -1) _top = br.ReadInt16(); else stream.Position += 2;
			short width = (short)(br.ReadInt16() - _left + 1);
			short height = (short)(br.ReadInt16() - _top + 1);
			try
			{
				_image = DecodeImage(_left, _top, width, height, br.ReadBytes(_length - 8));
				_image.Palette = _palette;
			}
			catch { _image = ErrorImage; }
		}

		/// <returns>True if successfull, False if failed</returns>
		public bool Write()
		{
			try
			{
				FileStream fs = File.Open(_fileName,FileMode.Open,FileAccess.ReadWrite);
				BinaryWriter bw = new BinaryWriter(fs);
				BinaryReader br = new BinaryReader(fs);
				// get pixel data
				/*BitmapData bd = _image.LockBits(new Rectangle(new Point(), _image.Size), ImageLockMode.ReadWrite, _image.PixelFormat);
				byte[] pix = new byte[bd.Stride*bd.Height];
				Idmr.Common.Graphics.CopyImageToBytes(bd, pix);	// from 0,0, index values
				_image.UnlockBits(bd);*/
				// grab the rest of the file, because there's no way in hell the length will match
				fs.Position = _offset + HeaderLength + _length;
				byte[] big = null;
				try { big = br.ReadBytes((int)(fs.Length-fs.Position)); }	// just in case it's the last object and can't read 0 bytes
				catch { /* do nothing */ }
				// LEFT and TOP will be the same, only adjust RIGHT and BOTTOM
				fs.Position = _offset + HeaderLength + 4;
				bw.Write((short)(_image.Width-1));		// RIGHT
				bw.Write((short)(_image.Height-1));	// BOTTOM
				/*int w = bd.Stride;
				int k, t=0;
				for (int y=0;y<_image.Height;y++)
				{
					bw.Write((short)((_image.Width<<1)+1));	// full-width row, compressed data
					bw.Write((short)0);	// row LEFT
					bw.Write((short)y);	// row TOP
					t = y*w;
					for (int x=0;x<_image.Width;)
					{
						for (k=x+1;k<_image.Width;k++)
						{
							if (pix[t+x] != pix[t+k]) break;	// find next pixel that doesn't match current
							if ((k-x) == 127) break;	// max repeat, 127<<1+1 == 255
						}
						if ((k-x) >= 3)	// need 3+ for REPEAT to be effective
						{
							fs.WriteByte((byte)(((k-x)<<1)+1));		// odd OP_CODE
							fs.WriteByte(pix[t+x]);		// pixel to be repeated
						}
						else
						{
							for (k=x+1;k<_image.Width;k++)	// determine length of straight read
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
							fs.WriteByte((byte)((k-x)<<1));		// even OP_CODE
							fs.Write(pix,t+x,k-x);
						}
						x = k;
					}
				}
				bw.Write((short)0);	// end null*/
				bw.Write(EncodeImage(_image, _left, _top));
				_length = (int)(fs.Position - _offset - HeaderLength);
				fs.Position = _offset + LengthOffset;
				bw.Write(_length);
				if (big != null) { fs.Position = _offset + HeaderLength + _length; bw.Write(big); fs.SetLength(fs.Position); }
				UpdateRmap(fs, "DELT", _name, _length);
				fs.Close();
				return true;
			}
			catch { return false; }
		}

		/// <value>Gets the standard error image placeholder</value>
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

		/// <remarks>Replaces the current image. No change upon error</remarks>
		/// <param name="image">Must be 640x480 or smaller</param>
		/// <exception cref="Idmr.Common.BoundaryException"><i>image.Height</i> > 480 or <i>image.Width</i> > 640</exception>
		public void SetImage(Bitmap image)
		{
			if (image.Width > 640) throw new Idmr.Common.BoundaryException("image.Width", "640px max");
			if (image.Height > 480) throw new Idmr.Common.BoundaryException("image.Height", "480px max");
			Bitmap temp = _image;
			try { _image = Idmr.Common.Graphics.ConvertTo8bpp(image, _palette); }
			catch (Exception x) { _image = temp; throw x; }
		}
		
		/// <remarks>Reads raw byte data from LFD and converts to a 256-color Bitmap</remarks>
		/// <param name="left">The Delt or Anim.Frame Left value</param>
		/// <param name="top">The Delt or Anim.Frame Top value</param>
		/// <param name="width">The Delt or Anim.Frame Width value</param>
		/// <param name="height">The Delt or Anim.Frame Height value</param>
		/// <param name="rawData">Delt pixel data from the LFD</param>
		/// <returns>256-color indexed Bitmap data with default palette</returns>
		public static Bitmap DecodeImage(short left, short top, short width, short height, byte[] rawData)
		{
			int w = (width % 4 == 0 ? width : width + (4-width%4));	// w has to be a multiple of 4, round up
			byte[] pixels = new byte[w*height];
			for (int y=0, pos=0;y<height-1;)
			{
				int l = BitConverter.ToInt16(rawData, pos); pos += 2;		// row data value
				bool compressed = Convert.ToBoolean(l%2);	// get storage type
				l >>= 1;		// number of pixels in row
				if (l==0) { l++; continue; }	// if we wind up at the end, try next row
				int x = BitConverter.ToInt16(rawData, pos) - left; pos += 2;	// row starting column
				y = BitConverter.ToInt16(rawData, pos) - top; pos += 2;	// starting row
				if (y < 0 || y >= height) throw new Exception("r " + y.ToString());	// Exception messages are there for debugging
				if (x < 0 || x >= width) throw new Exception("c " + x.ToString());
				int startCol = x;
				for (;x<l+startCol;)
				{
					byte b = rawData[pos++];
					if (((b%2) == 1) && compressed)		// odd OP_CODE odd LENGTH = Repeat
					{
						b >>= 1;	// number of repeats
						byte p = rawData[pos++];
						for (int k=0;k<b;k++, x++) pixels[y*w + x] = p;
					}
					else if (!compressed)	// even LENGTH = Straight Read
					{
						pos--;	// not actually an OP_CODE, need to read again
						for (int k=0;k<l;k++, x++) pixels[y*w + x] = rawData[pos++];
					}
					else	// even OP_CODE = Read
					{
						b >>= 1;
						for (int k=0;k<b;k++, x++) pixels[y*w + x] = rawData[pos++];
					}
				}
			}
			Bitmap image = new Bitmap (width, height, PixelFormat.Format8bppIndexed);
			BitmapData bmdata = image.LockBits(new Rectangle(new Point(), image.Size), ImageLockMode.ReadWrite, image.PixelFormat);
			Idmr.Common.Graphics.CopyBytesToImage(pixels, bmdata);
			image.UnlockBits(bmdata);
			return image;
		}
		
		/// <remarks>Converts image to compressed DELT data</remarks>
		/// <param name="image">Bitmap to be encoded</param>
		/// <param name="left">Delt or Anim.Frame Left value</param>
		/// <param name="top">Delt or Anim.Frame Top value</param>
		/// <exception cref="ArgumentException"><i>image</i> is not PixelFormat.Format8bppIndexed</exception>
		/// <returns>Byte array of raw data ready to be written to file</returns>
		public static byte[] EncodeImage(Bitmap image, short left, short top)
		{
			if (image.PixelFormat != PixelFormat.Format8bppIndexed) throw new ArgumentException("image must be 8bppIndexed");
			BitmapData bd = image.LockBits(new Rectangle(new Point(), image.Size), ImageLockMode.ReadWrite, image.PixelFormat);
			byte[] pix = new byte[bd.Stride*bd.Height];
			Idmr.Common.Graphics.CopyImageToBytes(bd, pix);
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
			Buffer.BlockCopy(tempRaw, 0, raw, 0, pos);	// copy all of the used data into the final, correctly-sized array
			return raw;
		}
		/// <remarks>Converts the provided image to 256-colors using the given palette before converting to compressed DELT data</remarks>
		/// <param name="image">Bitmap to be encoded</param>
		/// <param name="left">Delt or Anim.Frame Left value</param>
		/// <param name="top">Delt or Anim.Frame Top value</param>
		/// <param name="palette">ColorPalette to be used</param>
		/// <returns>Byte array of raw data ready to be written to file</returns>
		public static byte[] EncodeImage(Bitmap image, short left, short top, ColorPalette palette)
		{
			return EncodeImage(Idmr.Common.Graphics.ConvertTo8bpp(image, palette), left, top);
		}

		/// <value>Gets or sets the Left location</value>
		/// <remarks>Will only update if value is 0 to (640-Width)</remarks>
		public short Left
		{
			get { return _left; }
			set { if (value >= 0 && value < (640 - _image.Width)) _left = value; }
		}
		/// <value>Gets or sets the Top location</value>
		/// <remarks>Will only update if value is 0 to (480-Height)</remarks>
		public short Top
		{
			get { return _top; }
			set { if (value >= 0 && value < (480 - _image.height)) _top = value; }
		}
		public short Width { get { return (short)_image.Width; } }
		public short Height { get { return (short)_image.Height; } }
		/// <value>Gets the Format8bppIndexed image</value>
		public Bitmap Image { get { return _image; } }
	}
}
