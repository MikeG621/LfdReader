/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010 Michael Gaisser (mjgaisser@gmail.com)
 * 
 * This program is free software; you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the
 * Free Software Foundation; either version 2 of the License, or (at your
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
 */

using System;
using System.IO;

namespace Idmr.LfdReader
{
	/// <remarks>Base class for LFD resources, also provides ability to retrieve raw data from unknown resources</remarks>
	public abstract class Resource
	{
		protected string _fileName;
		protected long _offset;
		protected string _name;
		protected int _length;

		/// <value>Full path to the LFD file</value>
		public string FileName { get { return _fileName; } }
		/// <value>File position of the beginning of the Resource</value>
		public long Offset { get { return _offset; } }
		/// <value>8 char name of Resource</value>
		public string Name { get { return _name; } }
		/// <value>Length of Resource, not including header</value>
		public int Length { get { return _length; } }

		/// <param name="stream">Open FileStream to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <exception cref="System.ArgumentException"></exception>
		/// <exception cref="System.ArgumentOutOfRangeException"></exception>
		/// <returns>4 char indent string of Resource</returns>
		public static string GetType(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + TypeOffset;
			return new string(br.ReadChars(4)).Trim('\0');
		}
		/// <param name="filePath">Full path to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <exception cref="System.ArgumentException"></exception>
		/// <exception cref="System.ArgumentOutOfRangeException"></exception>
		/// <exception cref="System.UnauthorizedAccessException"></exception>
		/// <exception cref="System.IO.FileNotFoundException"></exception>
		/// <returns>4 char indent string of Resource</returns>
		public static string GetType(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			string str = GetType(stream, filePosition);
			stream.Close();
			return str;
		}

		/// <param name="stream">Open FileStream to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <exception cref="System.ArgumentException"></exception>
		/// <exception cref="System.ArgumentOutOfRangeException"></exception>
		/// <returns>8 char name of Resource</returns>
		public static string GetName(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + NameOffset;
			return new string(br.ReadChars(8)).Trim('\0');
		}
		/// <param name="filePath">Full path to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <exception cref="System.ArgumentException"></exception>
		/// <exception cref="System.ArgumentOutOfRangeException"></exception>
		/// <exception cref="System.UnauthorizedAccessException"></exception>
		/// <exception cref="System.IO.FileNotFoundException"></exception>
		/// <returns>8 char name of Resource</returns>
		public static string GetName(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			string str = GetName(stream, filePosition);
			stream.Close();
			return str;
		}

		/// <param name="stream">Open FileStream to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <exception cref="System.ArgumentException"></exception>
		/// <exception cref="System.IO.EndOfStreamException"></exception>
		/// <returns>4 byte length of Resource, does not include header</returns>
		public static int GetLength(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + LengthOffset;
			return br.ReadInt32();
		}
		/// <param name="filePath">Full path to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <exception cref="System.ArgumentException"></exception>
		/// <exception cref="System.UnauthorizedAccessException"></exception>
		/// <exception cref="System.IO.EndOfStreamException"></exception>
		/// <exception cref="System.IO.FileNotFoundException"></exception>
		/// <returns>4 byte length of Resource, does not include header</returns>
		public static int GetLength(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			int len = GetLength(stream, filePosition);
			stream.Close();
			return len;
		}

		/// <param name="stream">Open FileStream to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <exception cref="System.ArgumentException"></exception>
		/// <exception cref="System.ArgumentOutOfRangeException"></exception>
		/// <exception cref="System.IO.EndOfStreamException"></exception>
		/// <returns>Byte array of information beyond header</returns>
		public static byte[] GetRawData(FileStream stream, long filePosition)
		{
			int len = GetLength(stream, filePosition);
			BinaryReader br = new BinaryReader(stream);
			stream.Position = filePosition + HeaderLength;
			return br.ReadBytes(len);
		}
		/// <param name="filePath">Full path to LFD file</param>
		/// <param name="filePosition">Beginning of Resource</param>
		/// <exception cref="System.ArgumentException"></exception>
		/// <exception cref="System.ArgumentOutOfRangeException"></exception>
		/// <exception cref="System.UnauthorizedAccessException"></exception>
		/// <exception cref="System.IO.EndOfStreamException"></exception>
		/// <exception cref="System.IO.FileNotFoundException"></exception>
		/// <returns>Byte array of information beyond header</returns>
		public static byte[] GetRawData(string filePath, long filePosition)
		{
			FileStream stream = File.OpenRead(filePath);
			byte[] data = GetRawData(stream, filePosition);
			stream.Close();
			return data;
		}

		/// <remarks>Total length of header information</remarks>
		public const int HeaderLength = 16;
		/// <remarks>Location of Type within header</remarks>
		public const int TypeOffset = 0;
		/// <remarks>Location of Offset within header</remarks>
		public const int NameOffset = 4;
		/// <remarks>Location of Length within header</remarks>
		public const int LengthOffset = 12;

		/// <remarks>To be used during individual Write() functions, searches for RMAP and updates the appropriate subheader</remarks>
		/// <param name="stream">FileStream to the opened LFD</param>
		/// <param name="type">Resource Type</param>
		/// <param name="name">Resource Name</param>
		/// <param name="length">Resource Length</param>
		/// <exception cref="System.ArgumentException"></exception>
		/// <exception cref="System.ArgumentOutOfRangeException"></exception>
		/// <exception cref="System.IO.EndOfStreamException"></exception>
		protected static void UpdateRmap(FileStream stream, string type, string name, int length)
		{
			if (GetType(stream, 0) == "RMAP")
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
	}
}
