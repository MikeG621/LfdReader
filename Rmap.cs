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
	/// Reads LFD files and interprets RMAP resources
	/// </remarks>
	public class Rmap : Resource
	{
		private SubHeader[] _headers;	// only thing RMAP needs

		/// <param name="stream">The FileStream of the opened LFD file</param>
		public Rmap(FileStream stream)
		{
			Read(stream);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		public Rmap(string path)
		{
			FileStream stream = File.OpenRead(path);
			Read(stream);
			stream.Close();
		}

		private void Read(FileStream stream)
		{
			BinaryReader br = new BinaryReader(stream);
			_fileName = stream.Name;	// Resource.filename
			_offset = 0;	// Resource.offset
			stream.Position = NameOffset;
			_name = new string(br.ReadChars(8));	// Resource.name
			_length = br.ReadInt32();	// Resource.length
			_headers = new SubHeader[_length >> 4];
			int pos = _length + HeaderLength;
			for (int i=0;i<_headers.Length;i++)
			{
				_headers[i].Type = new string(br.ReadChars(4));
				_headers[i].Name = new string(br.ReadChars(8));
				_headers[i].Length = br.ReadInt32();
				_headers[i].Offset = pos;	// store the position of the actual resource
				pos += _headers[i].Length + HeaderLength;
			}
		}

		/// <returns>True if successfull, False if failure</returns>
		public bool Write()
		{
			// Length will be fixed, as we're not changing the contents, just the subheader lengths
			try
			{
				FileStream fs = File.OpenWrite(_fileName);
				BinaryWriter bw = new BinaryWriter(fs);
				fs.Position = HeaderLength;
				for(int i=0;i<_headers.Length;i++)
				{
					fs.Position += LengthOffset;
					bw.Write(_headers[i].Length);
				}
				fs.Close();
				return true;
			}
			catch { return false; }
		}

		/// <value>Gets the number of headers contained within the RMAP</value>
		public int NumHeaders { get { return _headers.Length; } }
		/// <value>The SubHeader information within the RMAP</value>
		public SubHeader[] SubHeaders { get { return _headers; } }

		/// <remarks>Contains the information for a single SubHeader</remarks>
		public struct SubHeader
		{
			private string _type;
			private string _name;
			private int _length;
			private int _offset;

			/// <param name="t">Resource Type</param>
			/// <param name="n">Resource Name</param>
			/// <param name="l">Resource Length</param>
			public SubHeader(string type, string name, int length)
			{
				_type = type.Substring(0,4);
				string t = name.Trim('\0');
				_name = (t.Length > 8 ? t.Substring(0,8) : t);
				_length = length;
				_offset = 0;
			}

			/// <value>Gets or Sets the Type of the resource, 4 CHAR</value>
			public string Type { get { return _type; } set { _type = value.Substring(0,4); } }
			/// <value>Gets or Sets the Name of the resource, 8 CHAR</value>
			public string Name
			{
				get { return _name; } 
				set
				{
					string t = value.Trim('\0');
					_name = (t.Length > 8 ? t.Substring(0,8) : t);
				}
			}
			/// <value>Gets or Sets the Length of the resource</value>
			public int Length { get { return _length; } set { _length = value; } }
			/// <value>Gets or Sets the File.Position of the resource</value>
			public int Offset { get { return _offset; } set { _offset = value; } }
		}
	}
}
