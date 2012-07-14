/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2012 Michael Gaisser (mjgaisser@gmail.com)
 * 
 * This program is free software; you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the
 * Free Software Foundation; either version 3.0 of the License, or (at your
 * option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
 * FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details.
 *
 * You should have received a copy of the GNU General Public License along
 * with this program; if not, write to:
 * Free Software Foundation, Inc.
 * 59 Temple Place, Suite 330
 * Boston, MA 02111-1307 USA
 *
 * Version: 1.0
 */

/* CHANGELOG
 * 110924 - housekeeping, added _tempFile, Decode/EncodeResource
 * 110927 - removed abstract, added _rawData
 * 111001 - ResourceType
 * 111130 - _parse to Parse and public
 * 120412 - default _fileName
 * 120416 - add ToString override
 * 120425 - rem _tempFile
 */

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Base class for LFD resources, also provides ability to retrieve raw data from unknown resources</summary>
	/// <remarks>All LFD resources are derived from this class. Resource types without a defined class are left as a generic Resource and read-only.</remarks>
	public class Resource
	{
		/// <summary>Full path to the lfd file</summary>
		protected string _fileName = "untitled.lfd";
		/// <summary>File.Position of the beginning of the Resource</summary>
		protected long _offset = 0;
		/// <summary>Resource name</summary>
		protected string _name = "resource";
		/// <summary>Resource type</summary>
		protected ResourceType _type = ResourceType.Undefined;
		/// <summary>Resource raw byte data</summary>
		protected byte[] _rawData = null;

		/// <summary>Enumeration of all known LFD Resource Types</summary>
		/// <remarks>Values are simply taken from the raw data (<b>int</b> or 4-byte ASCII <b>string</b>)</remarks>
		public enum ResourceType : int {
			/// <summary>Unknown or uninitialized</summary>
			Undefined,
			/// <summary>Animation</summary>
			Anim = 0x4D494E41,
			/// <summary>Sound effect</summary>
			Blas = 0x53414C42,
			/// <summary>Image format</summary>
			Bmap = 0x50414D42,
			/// <summary>Image format, similar to Delt?</summary>
			Cust = 0x54535543,
			/// <summary>Static image format</summary>
			Delt = 0x544c4544,
			/// <summary>Layout</summary>
			Film = 0x4D4C4946,
			/// <summary>Font</summary>
			Font = 0x544E4F46,
			/// <summary>MIDI sound</summary>
			Gmid = 0x44494D47,
			/// <summary>Cockpit transparency</summary>
			Mask = 0x4B53414D,
			/// <summary>Has something to do with flight mechanics?</summary>
			Mtrx = 0x5852544D,
			/// <summary>Cockpit component images</summary>
			Panl = 0x4C4E4150,
			/// <summary>Color palette</summary>
			Pltt = 0x54544C50,
			/// <summary>File structure map</summary>
			Rmap = 0x50414D52,
			/// <summary>Craft AI behaviour?</summary>
			Ship = 0x50494853,
			/// <summary>Strings</summary>
			Text = 0x54584554,
			/// <summary>Sound effect, typically voice data</summary>
			Voic = 0x43494F56,
			/// <summary>***DEPRECATED***, ACT image format</summary>
			Xact = 0x54434158 }

		#region constructors
		/// <summary>Blank constructor</summary>
		public Resource()
		{
			// blank constructor for derived classes
		}
		/// <summary>Creates a new generic resource from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Resource(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Creates a new generic resource from an existing file</summary>
		/// <param name="filePath">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Resource(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			_read(stream, filePosition);
			stream.Close();
		}
		#endregion constructors

		#region public methods
		/// <summary>Processes raw data to populate the resource</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Header</param>
		/// <remarks>For generic Resources, populates <see cref="RawData"/> and header information if necessary</remarks>
		public virtual void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
		}
		
		/// <summary>Prepares the resource for writing and updates <see cref="RawData"/></summary>
		/// <remarks>For generic Resources, does nothing as <see cref="RawData"/> is the only property</remarks>
		public virtual void EncodeResource()
		{
		}

		/// <summary>Gets the Type of the Resource</summary>
		/// <param name="stream">Open FileStream to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <returns>Known resource type, otherwise <see cref="ResourceType.Undefined"/></returns>
		public static ResourceType GetType(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + TypeOffset;
			return ParseResourceType(br.ReadInt32());
		}
		/// <summary>Gets the Type of the Resource</summary>
		/// <param name="filePath">Full path to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <returns>Known resource type, otherwise <see cref="ResourceType.Undefined"/></returns>
		public static ResourceType GetType(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			ResourceType type = GetType(stream, filePosition);
			stream.Close();
			return type;
		}

		/// <summary>Gets the name of the Resource</summary>
		/// <param name="stream">Open FileStream to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <returns>Trimmed name of the Resource, 8 char max</returns>
		public static string GetName(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + NameOffset;
			return new string(br.ReadChars(8)).Trim('\0');
		}
		/// <summary>Gets the name of the Resource</summary>
		/// <param name="filePath">Full path to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <returns>Trimmed name of the Resource, 8 char max</returns>
		public static string GetName(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			string str = GetName(stream, filePosition);
			stream.Close();
			return str;
		}

		/// <summary>Gets the length of the Resource</summary>
		/// <param name="stream">Open FileStream to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <returns>Byte length of the resource, not including the header</returns>
		public static int GetLength(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + LengthOffset;
			return br.ReadInt32();
		}
		/// <summary>Gets the length of the Resource</summary>
		/// <param name="filePath">Full path to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <returns>Byte length of the resource, not including the header</returns>
		public static int GetLength(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			int len = GetLength(stream, filePosition);
			stream.Close();
			return len;
		}

		/// <summary>Gets the raw byte data of the Resource</summary>
		/// <param name="stream">Open FileStream to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <returns>Full byte array of all information after the header</returns>
		public static byte[] GetRawData(FileStream stream, long filePosition)
		{
			int len = GetLength(stream, filePosition);
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + HeaderLength;
			return br.ReadBytes(len);
		}
		/// <summary>Gets the raw byte data of the Resource</summary>
		/// <param name="filePath">Full path to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <returns>Full byte array of all information after the header</returns>
		public static byte[] GetRawData(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			byte[] data = GetRawData(stream, filePosition);
			stream.Close();
			return data;
		}
		
		/// <summary>Gets the ResourceType specified by <i>type</i></summary>
		/// <param name="type">32-bit definition of the Type</param>
		/// <returns>Known resource type, otherwise <see cref="ResourceType.Undefined"/></returns>
		public static ResourceType ParseResourceType(string type)
		{
			try { return (ResourceType)Enum.Parse(typeof(ResourceType), type, true); }
			catch { System.Diagnostics.Debug.WriteLine("ResourceType Parse failure: " + type); return ResourceType.Undefined; }
		}
		/// <summary>Gets the ResourceType specified by <i>type</i></summary>
		/// <param name="type">32-bit definition of the Type</param>
		/// <returns>Known resource type, otherwise <see cref="ResourceType.Undefined"/></returns>
		public static ResourceType ParseResourceType(int type)
		{
			try { return (ResourceType)type; }
			catch
			{
				string typeText = ArrayFunctions.ReadStringFromArray(BitConverter.GetBytes(type), 0, 4);
				System.Diagnostics.Debug.WriteLine("ResourceType Parse failure: " + typeText + "(0x" + type.ToString("X") + ")");
				return ResourceType.Undefined;
			}
		}

		/// <summary>Gets a representative string of the Resource</summary>
		/// <returns>Resource in the format <see cref="Type">TYPE</see> <see cref="Name"/></returns>
		public override string ToString()
		{
			return _type.ToString().ToUpper() + _name;
		}
		#endregion
		
		#region public properties
		/// <summary>Gets the full path to the LFD file</summary>
		public string FileName { get { return _fileName; } }
		/// <summary>Gets the offset of the beginning of the Resource</summary>
		public long Offset { get { return _offset; } }
		/// <summary>Gets or sets the name of the Resource</summary>
		/// <remarks>Truncated to 8 characters, null characters trimmed</remarks>
		public string Name
		{
			get { return _name; }
			set { _name = Common.StringFunctions.GetTrimmed(value, 8); }
		}
		/// <summary>Gets the length of the raw byte data, not including header</summary>
		public int Length { get { return (_rawData != null ? _rawData.Length : 0); } }
		/// <summary>Gets the Resource type</summary>
		public ResourceType Type { get { return _type; } }
		/// <summary>Gets a copy of the raw byte data</summary>
		public byte[] RawData { get { return (byte[])_rawData.Clone(); } }
		/// <summary>Gets or sets the object that contains user-defined information</summary>
		public object Tag { get; set; }
		
		/// <summary>Total length of header information</summary>
		/// <remarks>Value is <b>16</b>.</remarks>
		public const int HeaderLength = 16;
		/// <summary>Location of <see cref="Type"/> within the header</summary>
		/// <remarks>Value is <b>0</b>.</remarks>
		public const int TypeOffset = 0;
		/// <summary>Location of <see cref="Offset"/> within the header</summary>
		/// <remarks>Value is <b>4</b>.</remarks>
		public const int NameOffset = 4;
		/// <summary>Location of <see cref="Length"/> within the header</summary>
		/// <remarks>Value is <b>12</b>.</remarks>
		public const int LengthOffset = 12;
		#endregion public properties

		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new Common.LoadFileException(x); }
		}
		
		#region protected methods
		/// <summary>Updates the appropriate subheader</summary>
		/// <param name="stream">FileStream to the opened Read/Write-enabled LFD</param>
		/// <param name="type">Resource Type</param>
		/// <param name="name">Resource Name</param>
		/// <param name="length">Resource Length</param>
		/// <remarks>To be used during individual Write functions. Looks for <i>type</i> and <i>name</i> within the Rmap if it exists, updates <i>length</i></remarks>
		protected static void _updateRmap(FileStream stream, ResourceType type, string name, int length)
		{
			if (GetType(stream, 0) == ResourceType.Rmap)
			{
				for (int i=1;i<=(GetLength(stream, 0)/HeaderLength);i++)
					if ((GetType(stream, i*HeaderLength) == type) && (GetName(stream, i*HeaderLength) == name))
					{
						stream.Position = i*HeaderLength + LengthOffset;
						new BinaryWriter(stream).Write(length);
						break;
					}
			}
		}

		/// <summary>Processes the LFD and initializes the object</summary>
		/// <param name="stream">FileStream to the opened Read-enabled LFD</param>
		/// <param name="position">Beginning of the Resource</param>
		/// <remarks>To be used during individual read functions. Calls <see cref="DecodeResource"/></remarks>
		protected void _process(FileStream stream, long position)
		{
			//System.Diagnostics.Debug.WriteLine("process");
			BinaryReader br = new BinaryReader(stream);
			_fileName = stream.Name;
			_offset = position;
			stream.Position = _offset + TypeOffset;
			_type = ParseResourceType(br.ReadInt32());
			stream.Position = _offset + NameOffset;
			_name = new string(br.ReadChars(8)).Trim('\0');
			int length = br.ReadInt32();
			DecodeResource(br.ReadBytes(length), false);
		}

		/// <summary>Saves raw data and populates header if necessary</summary>
		/// <param name="raw">Raw byte data of the resource</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Resource header information</param>
		/// <remarks>To be called from individual <see cref="DecodeResource"/> functions</remarks>
		protected void _decodeResource(byte[] raw, bool containsHeader)
		{
			//System.Diagnostics.Debug.WriteLine("_decode");
			if (containsHeader)
			{
				try { _type = ParseResourceType(BitConverter.ToInt32(raw, TypeOffset)); }
				catch { _type = ResourceType.Undefined; }
				_name = Common.ArrayFunctions.ReadStringFromArray(raw, NameOffset, 8);
				int length = BitConverter.ToInt32(raw, LengthOffset);	// should <= raw.Length-HeaderLength
				_rawData = new byte[length];
				Common.ArrayFunctions.TrimArray(raw, HeaderLength, _rawData);
			}
			else _rawData = raw;
		}
		#endregion protected methods
	}
}
