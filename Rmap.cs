/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2011 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in Resource.cs
 * Version: 1.0
 */

/* CHANGELOG
 * 110919 - housekeeping, added added LoadFileException and SaveFileException throws, Write() return void
 * 110924 - implemented Decode/EncodeResource()
 * 110927 - NumHeaders to NumberOfHeaders
 * 111108 - added ArrayFunctions calls
 */

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Reads LFD files and interprets RMAP resources</summary>
	public class Rmap : Resource
	{
		string _defaultName = "resource";
		SubHeader[] _headers;	// only thing RMAP needs

		/// <summary>Create a new Rmap instance with the specified number of SubHeaders</summary>
		/// <param name="numberOfHeaders">The number of entries in the Rmap</param>
		/// <exception cref="ArgumentException"><i>numberOfHeaders</i> must be positive</exception>
		public Rmap(int numberOfHeaders)
		{
			if (numberOfHeaders < 1) throw new ArgumentException("Invalid number of headers", "numberOfHeaders");
			_name = _defaultName;
			_headers = new SubHeader[numberOfHeaders];
			_type = ResourceType.Rmap;
		}
		/// <summary>Create a new Rmap instance from an existing opened file</summary>
		/// <param name="stream">The FileStream of the opened LFD file</param>
		public Rmap(FileStream stream)
		{
			_read(stream);
		}
		/// <summary>Create a new Rmap instance from an existing file</summary>
		/// <param name="path">The full path to the unopened LFD file</param>
		public Rmap(string path)
		{
			FileStream stream = File.OpenRead(path);
			_read(stream);
			stream.Close();
		}
		/// <summary>Create a new Rmap instance with the specific LFD template</summary>
		/// <param name="category">The type of LFD file</param>
		/// <exception cref="System.ArgumentException">Cockpit LFDs do not contain Rmaps</exception>
		/// <remarks>Currently only effective for Battle categories</remarks>
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
			_type = ResourceType.Rmap;
		}

		void _read(FileStream stream)
		{
			try { _process(stream, 0); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		//===================
		/// <summary>Processes raw data to create Rmap information</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Header</param>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			int offset = 0;
			_decodeResource(raw, containsHeader);
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

		/// <summary>Prepare Rmap information for writing</summary>
		/// <returns>Raw data ready to write to file</returns>
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
			/// <remarks><i>Offset</i> initialized to 0, <i>name</i>truncated to 8 characters</remarks>
			public SubHeader(ResourceType type, string name, int length)
			{
				_type = type;
				string n = name.Trim('\0');
				_name = (n.Length > 8 ? n.Substring(0,8) : n);
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
				set
				{
					string n = value.Trim('\0');
					_name = (n.Length > 8 ? n.Substring(0,8) : n);
				}
			}
			/// <summary>Gets or sets the Length of the resource</summary>
			public int Length { get { return _length; } set { _length = value; } }
			/// <summary>Gets or sets the File.Position of the resource</summary>
			public int Offset { get { return _offset; } set { _offset = value; } }
		}
	}
}
