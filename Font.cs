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
	/// <remarks>Reads LFD files and interprets FONT resources</remarks>
	public class Font : Resource
	{
		private short _startingChar;
		private short _bitsPerScanLine;
		private short _numScanLines;
		private short _baseLine;
		private Bitmap[] _glyphs;

		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Font(FileStream stream, long filePosition)
		{
			Read(stream, filePosition);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Font(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			Read(fsLFD, filePosition);
			fsLFD.Close();
		}
		
		private void Read(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			_fileName = stream.Name;	// Resource.filename
			_offset = filePosition;	// Resource.offset
			stream.Position = _offset + NameOffset;
			_name = new string(br.ReadChars(8)).Trim('\0');	// Resource.name
			_length = br.ReadInt32();	// Resource.length
			_startingChar = br.ReadInt16();
			_glyphs = new Bitmap[br.ReadInt16()];
			_bitsPerScanLine = br.ReadInt16();
			_numScanLines = br.ReadInt16();
			_baseLine = br.ReadInt16();
			stream.Position += 2;	// 00 00
			for (int i=0;i<_glyphs.Length;i++)	// loop once to initialize glyph sizes
			{
				int w = br.ReadByte();
				_glyphs[i] = new Bitmap(w, _numScanLines, PixelFormat.Format1bppIndexed);
			}
			for (int i=0;i<_glyphs.Length;i++)	// now loop through and make ze glyphs
			{
				BitmapData bd1 = _glyphs[i].LockBits(new Rectangle(new Point(), _glyphs[i].Size), ImageLockMode.ReadWrite, _glyphs[i].PixelFormat);
				byte[] pix1 = new byte[bd1.Stride*bd1.Height];
				for (int y=0;y<_numScanLines;y++)
					for (int s=0;s<(_bitsPerScanLine/8);s++)
						pix1[y*bd1.Stride+s] =  br.ReadByte();	// it's reading 1bpp, don't need to process it :D
				Idmr.Common.Graphics.CopyBytesToImage(pix1, bd1);	// byte[] to image
				_glyphs[i].UnlockBits(bd1);
			}
		}

		/// <returns>True if successfull, false otherwise</returns>
		public bool Write()
		{
			try
			{
				FileStream fs = File.Open(_fileName, FileMode.Open, FileAccess.ReadWrite);
				BinaryWriter bw = new BinaryWriter(fs);
				BinaryReader br = new BinaryReader(fs);
				fs.Position = _offset + HeaderLength + 4;
				byte[] big = null;
				if (br.ReadInt16() != _bitsPerScanLine)
				{
					fs.Position = _offset + HeaderLength + _length;
					try { big = br.ReadBytes((int)(fs.Length-fs.Position)); }	// just in case it's the last object and can't read 0 bytes
					catch { /* do nothing */ }
				}
				fs.Position = _offset + HeaderLength + 4;	// skip start, count
				bw.Write(_bitsPerScanLine);
				fs.Position += 2;	// skip height
				bw.Write(_baseLine);	// sure, allow editing this
				fs.Position += 2;	// 00 00
				for (int i=0;i<_glyphs.Length;i++) bw.Write((byte)_glyphs[i].Width);
				for (int i=0;i<_glyphs.Length;i++)
				{
					BitmapData bd1 = _glyphs[i].LockBits(new Rectangle(new Point(), _glyphs[i].Size), ImageLockMode.ReadWrite, _glyphs[i].PixelFormat);
					byte[] pix1 = new byte[bd1.Stride*bd1.Height];
					Idmr.Common.Graphics.CopyImageToBytes(bd1, pix1);	// image to byte[]
					for (int y=0;y<_numScanLines;y++)
						for (int s=0;s<(_bitsPerScanLine/8);s++)
							bw.Write(pix1[y*bd1.Stride+s]);
					_glyphs[i].UnlockBits(bd1);
				}
				_length = (int)(fs.Position - _offset - HeaderLength);
				fs.Position = _offset + LengthOffset;
				bw.Write(_length);
				if (big != null) { fs.Position = _offset + HeaderLength + _length; bw.Write(big); fs.SetLength(fs.Position); }
				UpdateRmap(fs, "FONT", _name, _length);
				fs.Close();
				return true;
			}
			catch { return false; }
		}

		/// <value>Gets the ASCII value of the first character within the resource</value>
		public short StartingChar {	get { return _startingChar; } }
		/// <value>Gets the number of characters contained within the resource</value>
		public short TotalChars { get { return (short)_glyphs.Length; } }
		/// <value>Gets or Sets the length of values required per scanline</value>
		/// <exception cref="ArgumentException"><i>value</i> is not positive and a multiple of eight</exception>
		public short BitsPerScanLine
		{
			get { return _bitsPerScanLine; }
			set 
			{
				if ((value % 8) != 0) throw new ArgumentException("Error, BitsPerScanLine must be a multiple of 8.");
				if (value <= 0) throw new ArgumentException("Error, BitsPerScanLine cannot be zero or negative");
				_bitsPerScanLine = value;
			}	// this is left as write-enabled to allow wider characters
		}
		/// <value>Gets the total height of the font</value>
		public short NumScanLines { get { return _numScanLines; } }
		/// <value>Gets or Sets the zero-indexed ScanLine that is used as the "bottom" for the font.</value>
		public short BaseLine
		{
			get { return _baseLine; }
			set { _baseLine = value; }
		}

		/// <returns>Selected glyph</returns>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid glyph</exception>
		public Bitmap GetGlyph(int index) { return _glyphs[index]; }
		/// <remarks>Overwrites selected glyph with given image</remarks>
		/// <param name="index">Glyph index</param>
		/// <param name="image">Bitmap, converts to Format1bppIndexed, must match Height</param>
		/// <exception cref="ArgumentException"><i>image.Height</i> does not equal NumScanLines or <i>image.Width</i> exceeds BitsPerScanLine</exception>
		/// <exception cref="IndexOutOfRangeException"><i>index</i> is not a valid glyph</exception>
		public void ReplaceGlyph(int index, Bitmap image)
		{
			if (image.Height != _numScanLines) throw new ArgumentException("Error, new image not required height (" + _numScanLines.ToString() + "px).");
			if (image.Width > _bitsPerScanLine) throw new ArgumentException("Error, new image exceeds maximum width (" + _bitsPerScanLine.ToString() + "px).");
			/*image = new Bitmap(image);	// convert to Format32bppRGB
			// import image data
			_glyphs[index] = new Bitmap(image.Width, image.Height, PixelFormat.Format1bppIndexed);
			BitmapData bd32 = image.LockBits(new Rectangle(new Point(), image.Size), ImageLockMode.ReadWrite, image.PixelFormat);
			byte[] pix32 = new byte[bd32.Stride*bd32.Height];
			System.Runtime.InteropServices.Marshal.Copy(bd32.Scan0, pix32, 0, pix32.Length);	// image to byte[]
			// setup _glyphs[index]
			BitmapData bd1 = _glyphs[index].LockBits(new Rectangle(new Point(), _glyphs[index].Size), ImageLockMode.ReadWrite, _glyphs[index].PixelFormat);
			byte[] pix1 = new byte[bd1.Stride*bd1.Height];
			for (int y=0;y<image.Height;y++)
				for (int x=0, pos32=y*bd32.Stride, pos1=y*bd1.Stride;x<image.Width;x++)
					if (pix32[pos32+x*4] != 0 || pix32[pos32+x*4+1] != 0 || pix32[pos32+x*4+2] != 0) pix1[pos1+x/8] |= (byte)(0x80 >> (x&7));
			System.Runtime.InteropServices.Marshal.Copy(pix1, 0, bd1.Scan0, pix1.Length);	// byte[] to image
			image.UnlockBits(bd32);
			_glyphs[index].UnlockBits(bd1);*/
			_glyphs[index] = Idmr.Common.Graphics.ConvertTo1bpp(image);
		}
	}
}
