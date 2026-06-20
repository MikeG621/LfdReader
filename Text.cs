/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2026 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.2+
 */

/* CHANGE LOG
 * [ADD] Strings collection
 * [DEL] Strings[]
 * [ADD] Dispose
 * v1.2, 160712
 * [ADD] _isModified edits
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using Idmr.Common;
using System;
using System.IO;

namespace Idmr.LfdReader
{
	/// <summary>Object for "TEXT" string resources</summary>
	/// <remarks>The Text resource holds much, although not all of the text in the game. Line breaks are denoted by the '\0' character, blanks lines are '\n \0'.</remarks>
	/// <example><h4>Raw Data definition</h4>
	/// <code>
	/// RawData
	/// {
	///   /* 0x00 */ short NumberOfStrings;
	///   /* 0x02 */ TextString[NumberOfStrings] Strings;
	/// }
	/// 
	/// TextString
	/// {
	///   /* 0x00 */ short Length;
	///   /* 0x02 */ string[] SubStrings;
	///				 byte EndMarker = 0x00;
	/// }</code>
	/// <para>The Text's RawData block is one of the simplest of resource types.
	/// The first value, <see cref="NumberOfStrings"/> tells you the number of <i>TextString</i> items contained in the resource.
	/// Each TextString has a <i>Length</i> value, which is the total length of all <i>SubStrings</i> and the final <i>EndMarker</i> value.
	/// <u>All SubStrings are null-terminated (<c>'\0'</c>)</u>.
	/// This is useful for listing similar strings in a single group, or by listing "pages" in a single definition, with the zero bytes sometimes being used as line breaks.</para>
	/// <para>Note that when using SubStrings, there is no indication of the individual lengths or quantity. This is determined by the program itself and is context-specific.<br/>
	/// As of v3.0, the class provides members to work with substrings, alleviating the need for each implementation to duplicate that effort.</para></example>
	public partial class Text : Resource
	{
		short _numberOfStrings;
		TextStringCollection _strings;

		#region constructors
		/// <summary>Blank constructor.</summary>
		public Text()
		{
			_type = ResourceType.Text;
			_strings = new TextStringCollection(this);
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

		/// <summary>Clean up any resources being used.</summary>
		/// <param name="disposing"><see langword="true"/> if managed resources should be disposed; otherwise, <see langword="false"/>.</param>
		protected override void Dispose(bool disposing)
		{
			if (_disposed) return;

			if (disposing)
			{
				for (int i = 0; i < _strings.Count; i++) _strings[i].Dispose();
				_strings.Clear();
			}
			_strings = null;
			base.Dispose(disposing);
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
			if (_strings == null) _strings = new TextStringCollection(this, _numberOfStrings);
			else
			{
				_strings.Clear();
				_strings.setCount(_numberOfStrings);
			}
			for (int i = 0; i < _numberOfStrings; i++)
			{
				short len = BitConverter.ToInt16(_rawData, offset);
				_strings[i].Value = ArrayFunctions.ReadStringFromArray(_rawData, offset + 2, len);
				offset += len + 2;
			}
			_isModified = false;
		}

		/// <summary>Prepares the resource for writing and updates <see cref="Resource.RawData"/>.</summary>
		public override void EncodeResource()
		{
			int len = 2;
			foreach (var s in _strings)
			{
				len += 2;
				len += s.Length;
			}
			byte[] raw = new byte[len];
			ArrayFunctions.WriteToArray(_numberOfStrings, raw, 0);
			int position = 2;
			foreach (var s in _strings)
			{
				ArrayFunctions.WriteToArray(s.Length, raw, ref position);
				ArrayFunctions.WriteToArray(s.Value + "\0\0", raw, ref position);
			}
			_rawData = raw;
		}
		#endregion public methods

		/// <summary>Gets or sets the number of strings in the resource.</summary>
		/// <remarks><see cref="Strings"/> expands and contracts as needed. If new value is less than original, it will truncate with data loss.</remarks>
		public short NumberOfStrings
		{
			get => _numberOfStrings;
			set
			{
				if (value == _numberOfStrings) return;

				_numberOfStrings = value;
				_strings.setCount(_numberOfStrings);
				Dirty();
			}
		}

		/// <summary>Gets the accessor for the string collection.</summary>
		public TextStringCollection Strings => _strings;
	}
}
