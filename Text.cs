/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010 Michael Gaisser (mjgaisser@gmail.com)
 * 
 * Full notice in Resource.cs
 */

using System;
using System.IO;

namespace Idmr.LfdReader
{
	/// <remarks>
	/// Reads LFD files and interprets TEXT resources
	/// </remarks>
	public class Text : Resource
	{
		private short _numStrings;
		private string[] _strings;

		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Text(FileStream stream, long filePosition)
		{
			Read(stream, filePosition);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Text(string path, long filePosition)
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
			_numStrings = br.ReadInt16();
			_strings = new string[_numStrings];
			for (int i=0;i<_numStrings;i++)
			{
				short l = br.ReadInt16();
				_strings[i] = new string(br.ReadChars(l)).Trim('\0');
			}
		}

		/// <returns>True if successfull, False if failure</returns>
		public bool Write()
		{
			try
			{
				FileStream stream = File.Open(_fileName,FileMode.Open,FileAccess.ReadWrite);
				BinaryWriter bw = new BinaryWriter(stream);
				int len = 2;
				for(int i=0;i<_numStrings;i++)
				{
					len += 2;
					_strings[i] = _strings[i].Trim('\0') + "\0\0";
					len += _strings[i].Length;
				}
				stream.Position = _offset + LengthOffset;
				bw.Write(len);
				bw.Write(_numStrings);
				byte[] big = null;
				if (len != _length)	// if needed, read rest of file...
				{
					stream.Position = _offset + HeaderLength + _length;
					big = new byte[stream.Length-stream.Position];
					for(int i=0;i<big.Length;i++) big[i] = (byte)stream.ReadByte();
					stream.Position = _offset + HeaderLength;
				}
				bw.Write(_numStrings);
				for(int i=0;i<_numStrings;i++)
				{
					bw.Write((short)_strings[i].Length);	// yes, this assumes the string is <= 0xFFFF. hell, TIE might think that's -1 anyway :P
					bw.Write(_strings[i].ToCharArray());
				}
				if (len != _length)
				{
					bw.Write(big);	// ...and then write it in afterward
					stream.SetLength(stream.Position);
				}
				_length = len;
				UpdateRmap(stream, "TEXT", _name, _length);
				stream.Close();
				return true;
			}
			catch { return false;  }
		}

		/// <value>Gets or Sets the number of strings in the TEXT. Strings[] expands and contracts as needed</value>
		public short NumStrings 
		{ 
			get { return _numStrings; } 
			set 
			{ 
				string[] temp = _strings;
				_strings = new string[value];
				if (value > _numStrings) for(int i=0;i<_numStrings;i++) _strings[i] = temp[i];
				else for(int i=0;i<value;i++) _strings[i] = temp[i];
				_numStrings = value;
			} 
		}
		/// <value>The strings contained within the TEXT. NumStrings is updated</value>
		public string[] Strings 
		{ 
			get { return _strings; }
			set { _numStrings = (short)value.Length; _strings = value; }
		}
	}
}
