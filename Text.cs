/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2021 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.2
 */

/* CHANGE LOG
 * v1.2, 160712
 * [ADD] _isModified edits
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "TEXT" string resources</summary>
	/// <remarks>The Text resource holds much, although not all of the text in the game. Line breaks are denoted by the '\0' character.</remarks>
	/// <example><h4>Raw Data definition</h4>
	/// <code>
	/// RawData
	/// {
	///   /* 0x00 */ short NumberOfStrings;
	///   /* 0x02 */ LfdString[NumberOfStrings] Strings;
	/// }
	/// 
	/// LfdString
	/// {
	///   /* 0x00 */ short Length;
	///   /* 0x02 */ string[] SubStrings;
	///				 byte Reserved = 0x00;
	/// }</code>
	/// <para>The Text's RawData block is one of the simplest of resource types.
	/// The first value, <see cref="NumberOfStrings"/> tells you the number of <i>LfdString</i> items contained in the resource.
	/// Each LfdString has a <i>Length</i> value, which is the total length of all <i>SubStrings</i> and the final <i>Reserved</i> value.
	/// <u>All SubStrings are null-terminated (<c>'\0'</c>)</u>.
	/// This is useful for listing similar strings in a single group, or by listing "pages" in a single definition, with the zero bytes sometimes being used as line breaks.<br/>
	/// Within the class, <i>SubStrings</i> is treated as a single string and it's up to the user to split it appropriately.</para>
	/// <para>Note however that when using SubStrings, there is no indication of the individual lengths or quantity.
	/// This is determined by the program itself and is context-specific.</para></example>
	public class Text : Resource
	{
		short _numberOfStrings;
		string[] _strings;

		#region constructors
		/// <summary>Blank constructor.</summary>
		public Text()
		{
			_type = ResourceType.Text;
		}
		/// <summary>Creates a new instance from an existing opened file.</summary>
		/// <param name="stream">The opened LFD file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Text(FileStream stream, long filePosition)
		{
			read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file.</summary>
		/// <param name="path">The full path to the unopened LFD file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Text(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			read(stream, filePosition);
			stream.Close();
		}
		#endregion

		void read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to populate the resource.</summary>
		/// <param name="raw">Raw byte data.</param>
		/// <param name="containsHeader">Whether or not <paramref name="raw"/> contains the resource Header information.</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="Resource.ResourceType.Text"/>.</exception>
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

		/// <summary>Prepares the resource for writing and updates <see cref="Resource.RawData"/>.</summary>
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

		/// <summary>Gets or sets the number of strings in the resource.</summary>
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
                _isModifed = true;
			} 
		}
		/// <summary>Gets or sets the strings contained within the resource.</summary>
		public string[] Strings 
		{ 
			get { return _strings; }
			set
            {
                _numberOfStrings = (short)value.Length;
                _strings = value;
                _isModifed = true;
            }
		}
	}
}
