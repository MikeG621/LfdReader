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
	/// Reads LFD files and interprets cockpit PANL data
	/// </remarks>
	public class Panl : Resource
	{
		private ColorPalette _palette;
		private Bitmap[] _image;
		private bool _cockpit = false;

		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Panl(FileStream stream, long filePosition, ColorPalette palette)
		{
			Read(stream, filePosition, palette);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Panl(string path, long filePosition, ColorPalette palette)
		{
			FileStream stream = File.OpenRead(path);
			Read(stream, filePosition, palette);
			stream.Close();
		}

		private void Read(FileStream stream, long filePosition, ColorPalette palette)
		{
			BinaryReader br = new BinaryReader(stream);
			_fileName = stream.Name;	// Resource._fileName
			if (_fileName.ToUpper().EndsWith(".PNL")) _cockpit = true;
			_offset = filePosition;	// Resource._offset
			_palette = palette;
			if (!_cockpit)
			{
				stream.Position = _offset + NameOffset;
				_name = new string(br.ReadChars(8)).Trim('\0');	// Resource._name
				_length = br.ReadInt32();	// Resource._length
				PanlInfo pi = readImage(Resource.GetRawData(stream, _offset));
				_image = new Bitmap[1];
				_image[0] = new Bitmap(pi.Width, pi.Height, PixelFormat.Format8bppIndexed);
				_image[0].Palette = _palette;
				BitmapData bmdata = _image[0].LockBits(new Rectangle(new Point(), _image[0].Size), ImageLockMode.ReadWrite, _image[0].PixelFormat);
				Idmr.Common.Graphics.CopyBytesToImage(pi.PixelData, bmdata);
				_image[0].UnlockBits(bmdata);
			}
			else
			{
				// *.PNL files do not contain headers, just the raw data
				_name = _fileName.Substring(_fileName.LastIndexOf("\\")+1);
				_length = (int)stream.Length;
				int count = 0;
				for (stream.Position=0; stream.Position<stream.Length;) if (stream.ReadByte() == 0xFF) count++;
				_image = new Bitmap[count];
				stream.Position = 0;
				long pos = 0;
				for (int i=0; i<count; i++)
				{
					PanlInfo pi = readImage(br.ReadBytes((int)(stream.Length - stream.Position)));
					pos += pi.RawLength;
					stream.Position = pos;
					_image[i] = new Bitmap(pi.Width, pi.Height, PixelFormat.Format8bppIndexed);
					_image[i].Palette = _palette;
					BitmapData bmdata = _image[i].LockBits(new Rectangle(new Point(), _image[i].Size), ImageLockMode.ReadWrite, _image[i].PixelFormat);
					Idmr.Common.Graphics.CopyBytesToImage(pi.PixelData, bmdata);
					_image[i].UnlockBits(bmdata);
				}
			}
		}

		/// <returns>True if successfull, False if failed</returns>
		public bool Write()
		{
			try
			{
				FileStream fs = File.Open(_fileName, FileMode.Open, FileAccess.ReadWrite);
				BinaryWriter bw = new BinaryWriter(fs);
				BinaryReader br = new BinaryReader(fs);
				byte[] big = null;
				if (!_cockpit)
				{
					// grab the rest of the file
					fs.Position = _offset + HeaderLength + _length;
					try { big = br.ReadBytes((int)(fs.Length-fs.Position)); }	// just in case it's the last object and can't read 0 bytes
					catch { /* do nothing */ }
					fs.Position = _offset + HeaderLength;
				}
				else fs.Position = 0;
				for (int i=0; i<_image.Length; i++)
				{
					BitmapData bd = _image[i].LockBits(new Rectangle(new Point(), _image[i].Size), ImageLockMode.ReadWrite, _image[i].PixelFormat);
					byte[] pixels = new byte[bd.Stride * bd.Height];
					Idmr.Common.Graphics.CopyImageToBytes(bd, pixels);	// from 0,0, index values
					int k;
					for (int y=0; y<_image[i].Height; y++)
					{
						for (int x=0, pos=y*bd.Stride; x<_image[i].Width;)
						{
							for(k=x+1; k<_image[i].Width; k++)
								if (pixels[pos+x] != pixels[pos+k]) break;	// determine length of single color = k
							if ((k-x) < 5 && pixels[pos+x] != 0x3F)
							{
								// SMALL_REPEAT : 1-4 px, can't use 3F since shift goes to FC-FF
								byte b = (byte)(pixels[pos+x] << 2 + (k-x-1));
								fs.WriteByte(b);
							}
							else
							{
								// REPEAT : only going to use FD, since I see no reason to use FC at all
								fs.WriteByte(0xFD);
								fs.WriteByte((byte)(k-x-1));
								fs.WriteByte(pixels[pos+x]);
							}
							x = k;
						}
						fs.WriteByte(0xFE);
					}
					fs.WriteByte(0xFF);
					_image[i].UnlockBits(bd);
				}
				if (!_cockpit)
				{
					_length = (int)(fs.Position - _offset - HeaderLength);
					fs.Position = _offset + LengthOffset;
					bw.Write(_length);
					if (big != null) { fs.Position = _offset + HeaderLength + _length; bw.Write(big); fs.SetLength(fs.Position); }
					UpdateRmap(fs, "PANL", _name, _length);
				}
				else
				{
					fs.SetLength(fs.Position);
					_length = (int)fs.Length;
				}
				fs.Close();
				return true;
			}
			catch { return false; }
		}

		private PanlInfo readImage(byte[] rawData)
		{
			PanlInfo pi;
			pi.Width = 0;
			for (int i=0;;)
			{
				if (rawData[i] == 0xFE || rawData[i] == 0xFF) break;
				else if (rawData[i] == 0xFD)
				{
					pi.Width += (short)(rawData[i+1] + 1);
					i += 3;
				}
				else if (rawData[i] == 0xFC)
				{
					pi.Width += (short)(rawData[i+2] + 1);
					i += 3;
				}
				else
				{
					pi.Width += (short)((rawData[i] & 3) + 1);
					i++;
				}
			}
			// ...and the mask height
			int pos = 0, x, y;
			pi.RawLength = 0;
			for (pi.Height=0; pos<_length; pi.Height++)
			{
				if (rawData[pos] == 0xFF) { pi.RawLength = (short)(pos+1); break; }
				if (rawData[pos] == 0xFE) { pos++; pi.Height--; continue; }
				for (x=0; x<pi.Width; pos++)
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
			pi.PixelData = new byte[(pi.Width % 4 == 0 ? pi.Width : pi.Width + 4 - pi.Width % 4) * pi.Height];	// Scan width is 4
			int px = 0;
			for (y=0, pos=0; y<pi.Height; y++)
			{
				for (;(px%4)!=0;) px++;
				if (rawData[pos] == 0xFE) pos++;
				for (x=0; x<pi.Width; pos++)
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
			return pi;
		}

		public int NumberOfImages { get { return _image.Length; } }

		/// <remarks>Sets the default Panl image</remarks>
		/// <param name="image">Bitmap, converted to Format8bppIndexed, must be 640x480 or smaller</param>
		/// <exception cref="ArgumentException"></exception>
		public void SetImage(Bitmap image)
		{
			SetImage(image, 0);
		}
		/// <remarks>Sets the indicated Panl image</remarks>
		/// <param name="image">Bitmap, converted to Format8bppIndexed, must be 640x480 or smaller</param>
		/// <param name="index">PANL image index</param>
		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="IndexOutOfRangeException"></exception>
		public void SetImage(Bitmap image, int index)
		{
			if (!_cockpit) index = 0;
			else if (index < 0 || index >= _image.Length) throw new IndexOutOfRangeException();
			if (image.Width > 640) throw new ArgumentException("Width exceeds 640px");
			if (image.Height > 480) throw new ArgumentException("Height exceeds 480px");
			Bitmap temp = _image[index];
			try
			{
				/*image = new Bitmap(image);	// force it to 32bppRGB
				_image[index] = new Bitmap(image.Width, image.Height, PixelFormat.Format8bppIndexed);
				// import image data
				BitmapData bd32 = image.LockBits(new Rectangle(new Point(), image.Size), ImageLockMode.ReadWrite, image.PixelFormat);
				byte[] pix32 = new byte[bd32.Stride*bd32.Height];
				System.Runtime.InteropServices.Marshal.Copy(bd32.Scan0, pix32, 0, pix32.Length);
				// setup _image[] data for overwrite
				BitmapData bd8 = _image[index].LockBits(new Rectangle(new Point(), _image[index].Size), ImageLockMode.ReadWrite, _image[index].PixelFormat);
				byte[] pix8 = new byte[bd8.Stride*bd8.Height];
				for (int y=0; y<image.Height; y++)
					for (int x=0, pos32=y*bd32.Stride, pos8=y*bd8.Stride; x<image.Width; x++)
						pix8[pos8+x] = paletteIndex(pix32[pos32+x*4+2], pix32[pos32+x*4+1], pix32[pos32+x*4]);
				System.Runtime.InteropServices.Marshal.Copy(pix8, 0, bd8.Scan0, pix8.Length);
				image.UnlockBits(bd32);
				_image[index].UnlockBits(bd8);
				_image[index].Palette = _palette;*/
				_image[index] = Idmr.Common.Graphics.ConvertTo8bpp(image, _palette);
			}
			catch (Exception x) { _image[index] = temp; throw x; }
		}

		/// <remarks>Gets 8bbp Indexed Bitmap of default Panl image</remarks>
		public Bitmap GetImage()
		{
			return _image[0];
		}
		/// <param name="index">PANL image index</param>
		/// <returns>8bbp Indexed Bitmap of indicated PANL image</returns>
		/// <exception cref="IndexOutOfRangeException"></exception>
		public Bitmap GetImage(int index)
		{
			if (!_cockpit) index = 0;
			return _image[index];
		}

		private struct PanlInfo
		{
			public byte[] PixelData;
			public short Width;
			public short Height;
			public short RawLength;
		}
	}
}
