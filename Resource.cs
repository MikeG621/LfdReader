/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2025 Michael Gaisser (mjgaisser@gmail.com)
 * 
 * This library is free software; you can redistribute it and/or modify it
 * under the terms of the Mozilla Public License; either version 2.0 of the
 * License, or (at your option) any later version.
 *
 * This library is "as is" without warranty of any kind; including that the
 * library is free of defects, merchantable, fit for a particular purpose or
 * non-infringing. See the full license text for more details.
 *
 * If a copy of the MPL (MPL.txt) was not distributed with this file,
 * you can obtain one at http://mozilla.org/MPL/2.0/.
 *
 * Version: 2.0+
 */

/* CHANGE LOG
 * [ADD] Dirty()
 * v2.0, 210309
 * [ADD] Adlb, Btmp, Crft, Cplx, Rlnd, and Ship to ResourceType
 * [UPD] cleanup
 * v1.2, 160712
 * [ADD] _isModifed
 * [UPD] various minor tweaks
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Base class for LFD resources, also provides ability to retrieve raw data from unknown resources</summary>
	/// <remarks>All LFD resources are derived from this class. Resource types without a defined class are left as a generic Resource and read-only.</remarks>
	/// <example><h4>Format Definition</h4>
	/// <code>
	/// Resource
	/// {
	///   /* 0x00 */ char[4] Type;
	///   /* 0x04 */ char[8] Name;
	///   /* 0x0C */ int Length;
	///   /* 0x10 */ byte[Length] RawData;
	/// }</code>
	/// <para>Every Resource contains a Header of <b>0x10</b> bytes, which is comprised of the identifying <see cref="Type"/>, <see cref="Name"/> and resource <see cref="Length"/>.
	/// This Header is what makes up the SubHeader for <see cref="Rmap"/> resources.
	/// The <see cref="RawData"/> array is the complete contents of the resource, with its size equal to the Length value.</para></example>
	public class Resource
	{
		/// <summary>Full path to the lfd file.</summary>
		protected string _fileName = "untitled.lfd";
		/// <summary>File.Position of the beginning of the Resource.</summary>
		protected long _offset = 0;
		/// <summary>Resource name.</summary>
		protected string _name = "resource";
		/// <summary>Resource type.</summary>
		protected ResourceType _type = ResourceType.Undefined;
		/// <summary>Resource raw byte data.</summary>
		protected byte[] _rawData = null;
        /// <summary>Flag to denote if resource must be encoded before writing.</summary>
        internal bool _isModifed = false;

		/// <summary>Enumeration of all known LFD Resource Types.</summary>
		/// <remarks>Values are simply taken from the raw data (<b>int</b> or 4-byte ASCII <b>string</b>).</remarks>
		public enum ResourceType : int {
			/// <summary>Unknown or uninitialized.</summary>
			Undefined,
			/// <summary>Adlib-optimized MIDI sound.</summary>
			Adlb = 0x424C4441,
			/// <summary>Animation.</summary>
			Anim = 0x4D494E41,
			/// <summary>Sound effect.</summary>
			Blas = 0x53414C42,
			/// <summary>Image format.</summary>
			Bmap = 0x50414D42,
			/// <summary>Image format.</summary>
			Btmp = 0x504D5442,
			/// <summary>Legacy mesh data (Mac port).</summary>
			Cplx = 0x584c5043,
			/// <summary>Legacy mesh data (XW).</summary>
			Crft = 0x54465243,
			/// <summary>Image format, similar to Delt?</summary>
			Cust = 0x54535543,
			/// <summary>Static image format.</summary>
			Delt = 0x544c4544,
			/// <summary>Layout.</summary>
			Film = 0x4D4C4946,
			/// <summary>Font.</summary>
			Font = 0x544E4F46,
			/// <summary>General MIDI sound.</summary>
			Gmid = 0x44494D47,
			/// <summary>Cockpit transparency.</summary>
			Mask = 0x4B53414D,
			/// <summary>Replay data to display OPTs when not in-flight.</summary>
			Mtrx = 0x5852544D,
			/// <summary>Cockpit component images.</summary>
			Panl = 0x4C4E4150,
			/// <summary>Color palette.</summary>
			Pltt = 0x54544C50,
			/// <summary>Roland-optimzed MIDI sound.</summary>
			Rlnd = 0x444E4C52,
			/// <summary>File structure map.</summary>
			Rmap = 0x50414D52,
			// TABL?
			/// <summary>Mesh data.</summary>
			Ship = 0x50494853,
			/// <summary>Strings.</summary>
			Text = 0x54584554,
			/// <summary>Sound effect, typically voice data.</summary>
			Voic = 0x43494F56,
			/// <summary>***DEPRECATED***, ACT image form.</summary>
			Xact = 0x54434158 }

		#region constructors
		/// <summary>Blank constructor.</summary>
		public Resource()
		{
			// blank constructor for derived classes
		}
		/// <summary>Creates a new generic resource from an existing opened file.</summary>
		/// <param name="stream">The opened LFD file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Resource(FileStream stream, long filePosition)
		{
			read(stream, filePosition);
		}
		/// <summary>Creates a new generic resource from an existing file.</summary>
		/// <param name="filePath">The full path to the unopened LFD file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Resource(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			read(stream, filePosition);
			stream.Close();
		}
		#endregion constructors

		#region public methods
		/// <summary>Processes raw data to populate the resource.</summary>
		/// <param name="raw">Raw byte data.</param>
		/// <param name="containsHeader">Determines if <paramref name="raw"/> contains the Header.</param>
		/// <remarks>For generic Resources, populates <see cref="RawData"/> and header information if necessary.</remarks>
		public virtual void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
		}

		/// <summary>Marks the resource as modified.</summary>
		/// <remarks>To be used when making modifications that otherwise aren't detected, such as values of a simple array.</remarks>
		public void Dirty() => _isModifed = true;

		/// <summary>Prepares the resource for writing and updates <see cref="RawData"/>.</summary>
		/// <remarks>For generic Resources, does nothing as <see cref="RawData"/> is the only property.</remarks>
		public virtual void EncodeResource()
		{
		}

		/// <summary>Gets the Type of the Resource.</summary>
		/// <param name="stream">Open FileStream to LFD file.</param>
		/// <param name="filePosition">Beginning of Resource.</param>
		/// <returns>Known resource type, otherwise <see cref="ResourceType.Undefined"/>.</returns>
		public static ResourceType GetType(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + TypeOffset;
			return ParseResourceType(br.ReadInt32());
		}
		/// <summary>Gets the Type of the Resource.</summary>
		/// <param name="filePath">Full path to LFD file.</param>
		/// <param name="filePosition">Beginning of Resource.</param>
		/// <returns>Known resource type, otherwise <see cref="ResourceType.Undefined"/>.</returns>
		public static ResourceType GetType(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			ResourceType type = GetType(stream, filePosition);
			stream.Close();
			return type;
		}

		/// <summary>Gets the name of the Resource.</summary>
		/// <param name="stream">Open FileStream to LFD file.</param>
		/// <param name="filePosition">Beginning of Resource.</param>
		/// <returns>Trimmed name of the Resource, 8 char max.</returns>
		public static string GetName(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + NameOffset;
			return new string(br.ReadChars(8)).Trim('\0');
		}
		/// <summary>Gets the name of the Resource.</summary>
		/// <param name="filePath">Full path to LFD file.</param>
		/// <param name="filePosition">Beginning of Resource.</param>
		/// <returns>Trimmed name of the Resource, 8 char max.</returns>
		public static string GetName(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			string str = GetName(stream, filePosition);
			stream.Close();
			return str;
		}

		/// <summary>Gets the length of the Resource.</summary>
		/// <param name="stream">Open FileStream to LFD file.</param>
		/// <param name="filePosition">Beginning of Resource.</param>
		/// <returns>Byte length of the resource, not including the header.</returns>
		public static int GetLength(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + LengthOffset;
			return br.ReadInt32();
		}
		/// <summary>Gets the length of the Resource.</summary>
		/// <param name="filePath">Full path to LFD file.</param>
		/// <param name="filePosition">Beginning of Resource.</param>
		/// <returns>Byte length of the resource, not including the header.</returns>
		public static int GetLength(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			int len = GetLength(stream, filePosition);
			stream.Close();
			return len;
		}

		/// <summary>Gets the raw byte data of the Resource.</summary>
		/// <param name="stream">Open FileStream to LFD file.</param>
		/// <param name="filePosition">Beginning of Resource.</param>
		/// <returns>Full byte array of all information after the header.</returns>
		public static byte[] GetRawData(FileStream stream, long filePosition)
		{
			int len = GetLength(stream, filePosition);
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + HeaderLength;
			return br.ReadBytes(len);
		}
		/// <summary>Gets the raw byte data of the Resource.</summary>
		/// <param name="filePath">Full path to LFD file.</param>
		/// <param name="filePosition">Beginning of Resource.</param>
		/// <returns>Full byte array of all information after the header.</returns>
		public static byte[] GetRawData(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			byte[] data = GetRawData(stream, filePosition);
			stream.Close();
			return data;
		}
		
		/// <summary>Gets the ResourceType specified by <paramref name="type"/>.</summary>
		/// <param name="type">32-bit definition of the Type.</param>
		/// <returns>Known resource type, otherwise <see cref="ResourceType.Undefined"/>.</returns>
		public static ResourceType ParseResourceType(string type)
		{
			try { return (ResourceType)Enum.Parse(typeof(ResourceType), type, true); }
			catch { System.Diagnostics.Debug.WriteLine("ResourceType Parse failure: " + type); return ResourceType.Undefined; }
		}
		/// <summary>Gets the ResourceType specified by <paramref name="type"/>.</summary>
		/// <param name="type">32-bit definition of the Type.</param>
		/// <returns>Known resource type, otherwise <see cref="ResourceType.Undefined"/>.</returns>
		public static ResourceType ParseResourceType(int type)
		{
			try { return (ResourceType)type; }
			catch
			{
				string typeText = ArrayFunctions.ReadStringFromArray(BitConverter.GetBytes(type), 0, 4);
				System.Diagnostics.Debug.WriteLine($"ResourceType Parse failure: {typeText}(0x{type:X})");
				return ResourceType.Undefined;
			}
		}

		/// <summary>Gets a representative string of the Resource.</summary>
		/// <returns>Resource in the format <see cref="Type">TYPE</see> <see cref="Name"/>.</returns>
		public override string ToString() => _type.ToString().ToUpper() + _name;
		#endregion

		#region public properties
		/// <summary>Gets the full path to the LFD file.</summary>
		public string FileName => _fileName;
		/// <summary>Gets the offset of the beginning of the Resource.</summary>
		public long Offset => _offset;
		/// <summary>Gets or sets the name of the Resource.</summary>
		/// <remarks>Truncated to 8 characters, null characters trimmed.</remarks>
		public string Name
		{
			get => _name;
			set
			{
				_name = StringFunctions.GetTrimmed(value, 8);
				_isModifed = true;
			}
		}
		/// <summary>Gets the length of the raw byte data, not including header.</summary>
		public int Length => (_rawData != null ? _rawData.Length : 0);
		/// <summary>Gets the Resource type.</summary>
		public ResourceType Type => _type;
		/// <summary>Gets a copy of the raw byte data.</summary>
		public byte[] RawData => (byte[])_rawData.Clone();
		/// <summary>Gets or sets the object that contains user-defined information.</summary>
		public object Tag { get; set; }
		
		/// <summary>Total length of header information.</summary>
		/// <remarks>Value is <b>16</b>.</remarks>
		public const int HeaderLength = 16;
		/// <summary>Location of <see cref="Type"/> within the header.</summary>
		/// <remarks>Value is <b>0</b>.</remarks>
		public const int TypeOffset = 0;
		/// <summary>Location of <see cref="Offset"/> within the header.</summary>
		/// <remarks>Value is <b>4</b>.</remarks>
		public const int NameOffset = 4;
		/// <summary>Location of <see cref="Length"/> within the header.</summary>
		/// <remarks>Value is <b>12</b>.</remarks>
		public const int LengthOffset = 12;
		#endregion public properties

		void read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}
		
		#region protected methods
		/// <summary>Updates the appropriate subheader.</summary>
		/// <param name="stream">FileStream to the opened Read/Write-enabled LFD.</param>
		/// <param name="type">Resource Type.</param>
		/// <param name="name">Resource Name.</param>
		/// <param name="length">Resource Length.</param>
		/// <remarks>To be used during individual Write functions. Looks for <paramref name="type"/> and <paramref name="name"/> within the Rmap if it exists, updates <paramref name="length"/>.</remarks>
		protected static void _updateRmap(FileStream stream, ResourceType type, string name, int length)
		{
			if (GetType(stream, 0) != ResourceType.Rmap) return;
			
			for (int i = 1; i <= (GetLength(stream, 0) / HeaderLength); i++)
				if ((GetType(stream, i * HeaderLength) == type) && (GetName(stream, i * HeaderLength) == name))
				{
					stream.Position = i * HeaderLength + LengthOffset;
					new BinaryWriter(stream).Write(length);
					break;
				}
		}

		/// <summary>Processes the LFD and initializes the object.</summary>
		/// <param name="stream">FileStream to the opened Read-enabled LFD.</param>
		/// <param name="position">Beginning of the Resource.</param>
		/// <remarks>To be used during individual read functions. Calls <see cref="DecodeResource"/>.</remarks>
		protected void _process(FileStream stream, long position)
		{
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

		/// <summary>Saves raw data and populates header if necessary.</summary>
		/// <param name="raw">Raw byte data of the resource.</param>
		/// <param name="containsHeader">Determines if <paramref name="raw"/> contains the Resource header information.</param>
		/// <remarks>To be called from individual <see cref="DecodeResource"/> functions.</remarks>
		protected void _decodeResource(byte[] raw, bool containsHeader)
		{
			if (containsHeader)
			{
				try { _type = ParseResourceType(BitConverter.ToInt32(raw, TypeOffset)); }
				catch { _type = ResourceType.Undefined; }
				_name = ArrayFunctions.ReadStringFromArray(raw, NameOffset, 8);
				int length = BitConverter.ToInt32(raw, LengthOffset);	// should <= raw.Length-HeaderLength
				_rawData = new byte[length];
				ArrayFunctions.TrimArray(raw, HeaderLength, _rawData);
			}
			else _rawData = raw;
		}
		#endregion protected methods
	}
}