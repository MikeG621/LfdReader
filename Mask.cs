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
	/// Reads LFD files and interprets cockpit transparency MASK data
	/// </remarks>
	public class Mask : Resource
	{
		private Bitmap _image;

		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Mask(FileStream stream, long filePosition)
		{
			Read(stream, filePosition);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Mask(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			Read(stream, filePosition);
			stream.Close();
		}
		
		private void Read(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			_fileName = stream.Name;	// Resource._fileName
			_offset = filePosition;	// Resource._offset
			stream.Position = _offset + NameOffset;
			_name = new string(br.ReadChars(8)).Trim('\0');	// Resource._name
			_length = br.ReadInt32();	// Resource._length
			// okay, now determine mask width...
			byte[] raw = Resource.GetRawData(stream, _offset);
			short width = 0, height;
			for (int i=1;;i++)
			{
				if (raw[i] == 0) width += 0x100;
				else if (raw[i] == raw[0]) break;	// this assumes there's no 1px or 255px lengths in the top row
				else width += raw[i];
			}
			// ...and the mask height
			int pos = 0, x, y;
			for (height=0;pos<_length;height++)
			{
				if (raw[pos] == 0) break;
				if (pos > 0x710) pos++;
				else pos++;
				for(x=0;x<width;pos++)
				{
					if (raw[pos] == 0) { x += 0x100; if (x==width) pos++; }	// won't end on 00, so there's an extra pixel
					else x += raw[pos];
				}
			}
			// start ze image!
			_image = new Bitmap(width, height, PixelFormat.Format1bppIndexed);
			BitmapData bd = _image.LockBits(new Rectangle(new Point(), _image.Size), ImageLockMode.ReadWrite, _image.PixelFormat);
			bool draw;
			byte[] pixels = new byte[bd.Stride*bd.Height];
			for (y=0, pos=0, draw=false;y<height;y++, draw=false)
			{
				if (raw[pos] == 0xFF) draw = true;
				pos++;
				int len = 0;
				int w = bd.Stride*y;
				for (x=0;x<width;pos++)
				{
					if (raw[pos] == 0) { len += 0x100; continue; }
					len += raw[pos];
					if (draw)
					{
						for (int x0 = x;x<x0+len-1;x++) pixels[w+x/8] |= (byte)(0x80 >> (x&7));
						if (x != width) pixels[w+x/8] |= (byte)(0x80 >> (x&7));	// will not fire for "throw away" pixel
						x++;
					}
					else x += len;
					draw = !draw;
					len = 0;
				}
			}
			Idmr.Common.Graphics.CopyBytesToImage(pixels, bd);
			_image.UnlockBits(bd);
		}

		/// <returns>True if successfull, False if failed</returns>
		public bool Write()
		{
			try
			{
				FileStream fs = File.Open(_fileName, FileMode.Open, FileAccess.ReadWrite);
				BinaryWriter bw = new BinaryWriter(fs);
				BinaryReader br = new BinaryReader(fs);
				// grab the rest of the file
				fs.Position = _offset + HeaderLength + _length;
				byte[] big = null;
				try { big = br.ReadBytes((int)(fs.Length-fs.Position)); }	// just in case it's the last object and can't read 0 bytes
				catch { /* do nothing */ }
				fs.Position = _offset + HeaderLength;
				BitmapData bd = _image.LockBits(new Rectangle(new Point(), _image.Size), ImageLockMode.ReadWrite, _image.PixelFormat);
				byte[] pixels = new byte[bd.Stride*bd.Height];
				Idmr.Common.Graphics.CopyImageToBytes(bd, pixels);
				for(int y=0; y<_image.Height; y++)
				{
					for(int x=0, numBlack=0, numWhite=0, pos=y*bd.Stride; x<_image.Width; x++)
					{
						byte shift = (byte)(0x80 >> (x&7));
						if ((pixels[pos+x/8] & shift) == shift)
						{	// white
							if (x == 0) fs.WriteByte(0xFF);
							if (numBlack != 0)
							{
								if (numBlack >= 512) fs.WriteByte(0);
								if (numBlack >= 256) fs.WriteByte(0);
								if ((numBlack & 0xFF) != 0) fs.WriteByte((byte)(numBlack & 0xFF));
								else fs.WriteByte(1);	// SetMask ensures this is a "throw away" pixel, effectively x=Width
							}
							numWhite++;
							numBlack = 0;
						}
						else
						{	// black
							if (x == 0) fs.WriteByte(1);
							if (numWhite != 0)
							{
								if (numWhite >= 512) fs.WriteByte(0);
								if (numWhite >= 256) fs.WriteByte(0);
								if ((numWhite & 0xFF) != 0) fs.WriteByte((byte)(numWhite & 0xFF));
								else fs.WriteByte(1);	// SetMask ensures this is a "throw away" pixel, effectively x=Width
							}
							numWhite = 0;
							numBlack++;
						}
					}
				}
				bw.Write((short)0);
				_length = (int)(fs.Position - _offset - HeaderLength);
				fs.Position = _offset + LengthOffset;
				bw.Write(_length);
				if (big != null) { fs.Position = _offset + HeaderLength + _length; bw.Write(big); fs.SetLength(fs.Position); }
				UpdateRmap(fs, "MASK", _name, _length);
				fs.Close();
				return true;
			}
			catch { return false; }
		}

		/// <param name="image">New image mask. Converts to Format1bppIndexed, must be 640x480 or smaller</param>
		/// <exception cref="ArgumentException"></exception>
		public void SetMask(Bitmap image)
		{
			SetMask(image, Color.FromArgb(0,0,0));
		}
		/// <param name="image">New image mask. Converts to Format1bppIndexed, must be 640x480 or smaller</param>
		/// <param name="transparentColor">The Color to be used as transparent</param>
		/// <exception cref="ArgumentException"></exception>
		public void SetMask(Bitmap image, Color transparentColor)
		{
			if (image.Width > 640) throw new ArgumentException("Error, image width exceeds 640px");
			if (image.Height > 480) throw new ArgumentException("Error, image height exceeds 480px");
			Bitmap temp = _image;
			try
			{
				// due to special length checks, can't use Common.Functions.ConvertTo1bpp
				image = new Bitmap(image);	// force to 32bbpRGB
				_image = new Bitmap(image.Width, image.Height, PixelFormat.Format1bppIndexed);
				// import image data
				BitmapData bd32 = image.LockBits(new Rectangle(new Point(), image.Size), ImageLockMode.ReadWrite, image.PixelFormat);
				byte[] pix32 = new byte[bd32.Stride*bd32.Height];
				Idmr.Common.Graphics.CopyImageToBytes(bd32, pix32);
				BitmapData bd1 = _image.LockBits(new Rectangle(new Point(), _image.Size), ImageLockMode.ReadWrite, _image.PixelFormat);
				byte[] pix1 = new byte[bd1.Stride*bd1.Height];
				for (int y=0;y<image.Height;y++)
					for (int x=0, numBlack=0, numWhite=0, pos32=y*bd32.Stride, pos1=y*bd1.Stride;x<bd32.Width;x++)
						if (pix32[pos32+x*4] != transparentColor.B || pix32[pos32+x*4+1] != transparentColor.G || pix32[pos32+x*4+2] != transparentColor.R)
						{	// white
							pix1[pos1+x/8] |= (byte)(0x80 >> (x & 7));
							// throw if 256px detected, and not end of row (which format can handle)
							if ((numBlack%256) == 0 && x != image.Width-1) throw new ArgumentException("Image contains line length of 256 or 512 pixels");
							numBlack = 0;
							numWhite++;
						}
						else
						{	// black
							if ((numWhite%256) == 0 && x != image.Width-1) throw new ArgumentException("Image contains line length of 256 or 512 pixels");
							numBlack++;
							numWhite = 0;
						}
				Idmr.Common.Graphics.CopyBytesToImage(pix1, bd1);
				image.UnlockBits(bd32);
				_image.UnlockBits(bd1);
			}
			catch (Exception x) { _image = temp; throw x; }
		}

		/// <value>Gets the monochrome mask image</value>
		public Bitmap Image { get { return _image; } }
		/// <value>Gets the width of the Mask image</value>
		public short Width { get { return (short)_image.Width; } }
		/// <value>Gets the height of the Mask image</value>
		public short Height { get { return (short)_image.Height; } }
	}
}
