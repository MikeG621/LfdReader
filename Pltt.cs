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
	/// Reads LFD files and interprets PLTT color palette resources
	/// </remarks>
	public class Pltt : Resource
	{
		private byte _startIndex;
		private byte _endIndex;
		private PlttColor[] _entries;

		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Pltt(FileStream stream, long filePosition)
		{
			Read(stream, filePosition);
		}
		/// <param name="path">This is the full path of the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Pltt(string path, long filePosition)
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
			_startIndex = br.ReadByte();
			_endIndex = br.ReadByte();
			_entries = new PlttColor[_endIndex-_startIndex+1];
			for (int i=0;i<_entries.Length;i++) _entries[i] = new PlttColor(br.ReadByte(), br.ReadByte(), br.ReadByte());
		}

		/// <returns>True if successfull, False if failure</returns>
		public bool Write()
		{
			try
			{
				// header information does not change, neither does start/end values, skip directly to color info
				FileStream fs = File.OpenWrite(_fileName);
				BinaryWriter bw = new BinaryWriter(fs);
				fs.Position = _offset + HeaderLength + 2;
				for(int i=0;i<_entries.Length;i++)
				{
					bw.Write(_entries[i].R);
					bw.Write(_entries[i].G);
					bw.Write(_entries[i].B);
				}
				fs.Close();	// no need to re-write final '\0' as size isn't changing
				return true;
			}
			catch { return false; }
		}

		/// <value>Gets the starting index of the color definitions</value>
		public byte StartIndex { get { return _startIndex; } }
		/// <value>Gets the ending index of the color definitions</value>
		public byte EndIndex { get { return _endIndex; } }
		/// <value>The Colors defined for the PLTT</value>
		public PlttColor[] Entries { get { return _entries; } set { if (value.Length == _entries.Length) _entries = value; } }
		/// <value>Quick ColorPalette access for application to images</value>
		public ColorPalette Palette
		{
			get
			{
				ColorPalette pal = new Bitmap(1, 1, PixelFormat.Format8bppIndexed).Palette;
				for (int i=_startIndex;i<=_endIndex;i++) pal.Entries[i] = _entries[i-_startIndex].Color;
				return pal;
			}
		}

		/// <remarks>The RGB container for the color definitions</remarks>
		public struct PlttColor
		{
			public byte R;
			public byte G;
			public byte B;

			/// <param name="rValue">The R index, 0x00-0xFF</param>
			/// <param name="gValue">The G index, 0x00-0xFF</param>
			/// <param name="bValue">The B index, 0x00-0xFF</param>
			public PlttColor(byte rValue, byte gValue, byte bValue)
			{
				R = rValue;
				G = gValue;
				B = bValue;
			}

			public Color Color
			{
				get { return Color.FromArgb(R, G, B); }
				set
				{
					R = value.R;
					G = value.G;
					B = value.B;
				}
			}
		}
	}
}
