/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2011 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in Resource.cs
 * Version: 1.0
 */

/* CHANGELOG
 * 110922 - housekeeping, added added LoadFileException and SaveFileException throws, Write() return void
 * 110924 - implemented Decode/EncodeResource(), NumStrings to NumberOfStrings
 * 111108 - added ArrayFunctions calls
 */

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Reads LFD files and interprets TEXT resources</summary>
	public class Text : Resource
	{
		short _numberOfStrings;
		string[] _strings;

		#region constructors
		public Text()
		{
			_type = ResourceType.Text;
		}
		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Text(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Text(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream, filePosition);
			stream.Close();
		}
		#endregion

		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		//===================
		/// <summary>Processes raw data to create Text information</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Header</param>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			int offset = 2;
			_decodeResource(raw, containsHeader);
			_numberOfStrings = BitConverter.ToInt16(_rawData, 0);
			_strings = new string[_numberOfStrings];
			for (int i = 0; i < _numberOfStrings; i++)
			{
				short len = BitConverter.ToInt16(_rawData, offset);
				_strings[i] = ArrayFunctions.ReadStringFromArray(_rawData, offset + 2, len);
				offset += len + 2;
			}
		}

		/// <summary>Prepare Text information for writing</summary>
		/// <returns>Raw data ready to write to file</returns>
		public override void EncodeResource()
		{
			int len = 2;
			for (int i = 0; i < _numberOfStrings; i++)
			{
				len += 2;
				_strings[i] = _strings[i].Trim('\0') + "\0\0";
				len += _strings[i].Length;
			}
			byte[] raw = new byte[len];
			ArrayFunctions.WriteToArray(_numberOfStrings, raw, 0);
			int position = 2;
			foreach (string s in _strings)
			{
				ArrayFunctions.WriteToArray((short)s.Length, raw, ref position);
				ArrayFunctions.WriteToArray(s, raw, ref position);
			}
			_rawData = raw;
		}

		/// <summary>Gets or sets the number of strings in the Text</summary>
		/// <remarks><i>Strings</i> expands and contracts as needed. If new value is less than original, <i>Strings</i> will truncate with data loss.</remarks>
		public short NumberOfStrings 
		{ 
			get { return _numberOfStrings; } 
			set 
			{ 
				string[] temp = _strings;
				_strings = new string[value];
				if (value > _numberOfStrings) for(int i=0;i<_numberOfStrings;i++) _strings[i] = temp[i];
				else for(int i=0;i<value;i++) _strings[i] = temp[i];
				_numberOfStrings = value;
			} 
		}
		/// <summary>The strings contained within the Text</summary>
		public string[] Strings 
		{ 
			get { return _strings; }
			set { _numberOfStrings = (short)value.Length; _strings = value; }
		}
	}
}
