/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2014 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.1
 */

/* CHANGE LOG
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "RMAP" header resources</summary>
	/// <remarks>The Rmap resource is the Resource Map for the file. This contains the <see cref="Resource.Type"/>, <see cref="Resource.Name"/> and <see cref="Resource.Length"/> of every Resource in the file. Reading through the Rmap can produce a jump table for the file.<hr/>
	/// <h4>Raw Data definition</h4>
	/// <code>// Pseudo-code resource structure
	/// struct RawData
	/// {
	///   /* 0x00 */ SubHeader[Rmap.Length / 16] SubHeaders;
	/// }
	/// 
	/// struct SubHeader
	/// {
	///   /* 0x00 */ char[4] Type;
	///   /* 0x04 */ char[8] Name;
	///   /* 0x0C */ int Length;
	/// }</code>
	/// The Rmap's RawData block contains no unique information, as it is merely a listing of the Headers from the other resources in the file.<br/>
	/// The Rmap's <see cref="Resource.Name"/> is typically "<b>resource</b>" and <see cref="Resource.Length"/> is <c>(NumberOfHeaders * 16)</c>.</remarks>
	public class Rmap : Resource
	{
		string _defaultName = "resource";
		SubHeader[] _headers;	// only thing RMAP needs

		#region constructors
		/// <summary>Creates a new instance with the specified number of SubHeaders</summary>
		/// <param name="numberOfHeaders">The number of entries</param>
		/// <exception cref="ArgumentException"><i>numberOfHeaders</i> must be positive</exception>
		public Rmap(int numberOfHeaders)
		{
			if (numberOfHeaders < 1) throw new ArgumentException("Invalid number of headers", "numberOfHeaders");
			_name = _defaultName;
			_headers = new SubHeader[numberOfHeaders];
			_type = ResourceType.Rmap;
		}
		/// <summary>Creates a new instance from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Rmap(FileStream stream)
		{
			_read(stream);
		}
		/// <summary>Creates a new instance from an exsiting file</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Rmap(string path)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream);
			stream.Close();
		}
		/// <summary>Create a new Rmap instance with the specific LFD template</summary>
		/// <param name="category">The type of LFD file</param>
		/// <exception cref="System.ArgumentException">Cockpit LFDs do not contain Rmaps<br/><b>-or-</b><br/>Normal LFDs must be initialized with <see cref="Rmap(int)"/></exception>
		/// <remarks>Only usable with <see cref="LfdFile.LfdCategory.Battle"/> files, initializes for a <see cref="Text"/> and <see cref="Delt"/> resources.</remarks>
		public Rmap(LfdFile.LfdCategory category)
		{
			if (category == LfdFile.LfdCategory.Battle)
			{
				_name = _defaultName;
				_headers = new SubHeader[2];
				_headers[0].Type = Resource.ResourceType.Text;
				_headers[0].Name = "battle#";
				_headers[0].Offset = HeaderLength;
				_headers[0].Length = -1;
				_headers[1].Type = Resource.ResourceType.Delt;
				_headers[1].Name = "b#gal";
				_headers[1].Offset = -1;
				_headers[1].Length = -1;
			}
			else if (category == LfdFile.LfdCategory.Cockpit) throw new ArgumentException("Cockpit LFDs do not use RMAP", "category");
			else throw new ArgumentException("Normal LFDs must be initialized with Rmap(int)", "category");
			_type = ResourceType.Rmap;
		}
		#endregion constructors

		void _read(FileStream stream)
		{
			try { _process(stream, 0); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to populate the resource</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Whether or not <i>raw</i> contains the resource Header information</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="Resource.ResourceType.Rmap"/></exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Rmap) throw new ArgumentException("Raw header is not for a Rmap resource");
			int offset = 0;
			_headers = new SubHeader[_rawData.Length >> 4];
			int resourceOffset = _rawData.Length + HeaderLength;
			for (int i = 0; i < _headers.Length; i++)
			{
				_headers[i].Type = (ResourceType)BitConverter.ToInt32(raw, offset + TypeOffset);
				_headers[i].Name = ArrayFunctions.ReadStringFromArray(_rawData, offset + NameOffset, 8);
				_headers[i].Length = BitConverter.ToInt32(_rawData, offset + LengthOffset);
				_headers[i].Offset = resourceOffset;	// store the position of the actual resource
				resourceOffset += _headers[i].Length + HeaderLength;
				offset += HeaderLength;
				//System.Diagnostics.Debug.WriteLine(_headers[i].Type + " " + _headers[i].Name);
			}
		}

		/// <summary>Prepares the resource for writing and updates <see cref="Resource.RawData"/></summary>
		public override void EncodeResource()
		{
			byte[] raw = new byte[_headers.Length * HeaderLength];
			for (int i = 0; i < _headers.Length; i++)
			{
				ArrayFunctions.WriteToArray((int)_headers[i].Type, raw, i * HeaderLength + TypeOffset);
				ArrayFunctions.WriteToArray(_headers[i].Name, raw, i * HeaderLength + NameOffset);
				ArrayFunctions.WriteToArray(_headers[i].Length, raw, i * HeaderLength + LengthOffset);
			}
			_rawData = raw;
		}
		#endregion public methods

		/// <summary>Gets the number of headers contained within the Rmap</summary>
		public int NumberOfHeaders { get { return _headers.Length; } }
		/// <summary>Gets the SubHeader information within the Rmap</summary>
		public SubHeader[] SubHeaders { get { return _headers; } }

		/// <summary>Represents the information for a single SubHeader</summary>
		public struct SubHeader
		{
			ResourceType _type;
			string _name;
			int _length;
			int _offset;

			/// <summary>Initialize the SubHeader with the provided details</summary>
			/// <param name="type">Resource Type</param>
			/// <param name="name">Resource Name</param>
			/// <param name="length">Resource Length</param>
			/// <remarks><i>Offset</i> initialized to <b>0</b>, <i>name</i> truncated to 8 characters</remarks>
			public SubHeader(ResourceType type, string name, int length)
			{
				_type = type;
				_name = StringFunctions.GetTrimmed(name, 8);
				_length = length;
				_offset = 0;
			}

			/// <summary>Gets or sets the Type of the resource</summary>
			public ResourceType Type { get { return _type; } set { _type = value; } }
			/// <summary>Gets or sets the Name of the resource</summary>
			/// <remarks>Value is trimmed to 8 characters</remarks>
			public string Name
			{
				get { return _name; } 
				set { _name = StringFunctions.GetTrimmed(value, 8); }
			}
			/// <summary>Gets or sets the Length of the resource</summary>
			public int Length { get { return _length; } set { _length = value; } }
			/// <summary>Gets or sets the File.Position of the resource</summary>
			public int Offset { get { return _offset; } set { _offset = value; } }
		}
	}
}
