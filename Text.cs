/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2012 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.0
 */

/* CHANGELOG
 * 110922 - housekeeping, added added LoadFileException and SaveFileException throws, Write() return void
 * 110924 - implemented Decode/EncodeResource(), NumStrings to NumberOfStrings
 * 111108 - added ArrayFunctions calls
 * 120425 - Num.set returns if value unchanged, ResourceType check
 */

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "TEXT" string resources</summary>
	/// <remarks>The Text resource holds much, although not all of the text in the game. Line breaks are denoted by the '\0' character.<hr/>
	/// <h4>Raw Data definition</h4>
	/// <code>// Pseudo-code resource structure
	/// struct RawData
	/// {
	///   /* 0x00 */ short NumberOfStrings;
	///   /* 0x02 */ LfdString[NumberOfStrings] Strings;
	/// }
	/// 
	/// struct LfdString
	/// {
	///   /* 0x00 */ short Length;
	///   /* 0x02 */ string[] SubStrings;
	///   /* 0x?? */ byte Reserved = 0x00;
	/// }</code>
	/// The Text's RawData block is one of the simplest of resource types. The first value, <i>NumberOfStrings</i> tells you the number of <i>LfdString</i> items contained in the resource. Each LfdString has a <i>Length</i> value, which is the total length of all <i>SubStrings</i> and the final <i>Reserved</i> value. <u>All SubStrings are null-terminated (<c>'\0'</c>)</u>. This is useful for listing similar strings in a single group, or by listing "pages" in a single definition, with the zero bytes sometimes being used as line breaks.<br/><br/>
	/// Note however that when using SubStrings, there is no indication of the individual lengths or quantity. This is determined by the program itself and is context-specific.</remarks>
	public class Text : Resource
	{
		short _numberOfStrings;
		string[] _strings;

		#region constructors
		/// <summary>Blank constructor</summary>
		public Text()
		{
			_type = ResourceType.Text;
		}
		/// <summary>Creates a new instance from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Text(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
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

		#region public methods
		/// <summary>Processes raw data to populate the resource</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Whether or not <i>raw</i> contains the resource Header information</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="ResourceType.Text"/></exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Text) throw new ArgumentException("Raw header is not for a Text resource");
			int offset = 2;
			_numberOfStrings = BitConverter.ToInt16(_rawData, 0);
			_strings = new string[_numberOfStrings];
			for (int i = 0; i < _numberOfStrings; i++)
			{
				short len = BitConverter.ToInt16(_rawData, offset);
				_strings[i] = ArrayFunctions.ReadStringFromArray(_rawData, offset + 2, len);
				offset += len + 2;
			}
		}

		/// <summary>Prepares the resource for writing and updates <see cref="RawData"/></summary>
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
		#endregion public methods

		/// <summary>Gets or sets the number of strings in the resource</summary>
		/// <remarks><see cref="Strings"/> expands and contracts as needed. If new value is less than original, <see cref="Strings"/> will truncate with data loss.</remarks>
		public short NumberOfStrings 
		{ 
			get { return _numberOfStrings; } 
			set 
			{
				if (value == _numberOfStrings) return;
				string[] temp = _strings;
				_strings = new string[value];
				for (int i = 0; i < (value > _numberOfStrings ? _numberOfStrings : value); i++) _strings[i] = temp[i];
				_numberOfStrings = value;
			} 
		}
		/// <summary>Gets or sets the strings contained within the resource</summary>
		public string[] Strings 
		{ 
			get { return _strings; }
			set { _numberOfStrings = (short)value.Length; _strings = value; }
		}
	}
}
