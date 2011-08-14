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
	/// Reads LFD files and interprets BLAS and VOIC audio resources
	/// This is under the assumption that BLAS/VOIC data is uncompressed 8-bit PCM
	/// </remarks>
	public class Blas : Resource
	{
		private string _type;
		private byte[] _data;
		private int _frequency;
		private const int vocHeaderLength = 0x1A;

		/// <param name="stream">This is the FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Blas(FileStream stream, long filePosition)
		{
			Read(stream, filePosition);
		}
		/// <param name="path">The full path to the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Blas(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			Read(fsLFD, filePosition);
			fsLFD.Close();
		}

		private void Read(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			_fileName = stream.Name;	// Resource.name
			_offset = filePosition;	// Resource.offset
			stream.Position = _offset;
			_type = new string(br.ReadChars(4));	// BLAS or VOIC
			_name = new string(br.ReadChars(8)).Trim('\0');	// Resource.name
			_length = br.ReadInt32();	// Resource.length
			stream.Position = _offset + HeaderLength + vocHeaderLength;	// skipping over VOC_HEADER
			byte block = br.ReadByte();
			if (block == 6)	// REPEAT
			{
				stream.Position += 5;
				block = br.ReadByte();
			}
			if (block == 1)	// SOUND_DATA
			{
				int blockLen = br.ReadUInt16() + br.ReadByte()*256*256;	// stupid 3-byte length value
				_frequency = 1000000 / (256 - br.ReadByte());		// FREQUENCY_DIVSOR
				stream.Position++;	// CODEC == 0
				_data = br.ReadBytes(blockLen-2);
			}
			else _data = new byte[1];
		}

		/// <returns>True if successfull, False if failed</returns>
		public bool Write()
		{
			try
			{
				FileStream fs = File.Open(_fileName, FileMode.Open, FileAccess.ReadWrite);
				BinaryWriter bw = new BinaryWriter(fs);
				BinaryReader br = new BinaryReader(fs);
				bool repeat = false;
				fs.Position = _offset + HeaderLength + vocHeaderLength;	// skip over VOC_HEADER
				int len = _data.Length + 0x21;	// VOC_HEADER + SOUND_BLOCK_HEADER + SOUND_BLOCK + EOF_BLOCK_HEADER
				if (fs.ReadByte() == 6)
				{
					// contains repeat block
					repeat = true;
					len += 0xA;	// + REPEAT_BLOCK(6) + ENDREPEAT_BLOCK(4)
				}
				fs.Position = _offset + LengthOffset;
				bw.Write(len);
				fs.Position = _offset + HeaderLength + _length;
				byte[] big = new byte[fs.Length - fs.Position];
				big = br.ReadBytes(big.Length);	// save rest of file to be shifted after write
				fs.Position = _offset + HeaderLength + 0x1B;	// go to first datablock length
				if (repeat)	fs.Position += 6;	// skip over
				bw.Write((int)(_data.Length + 2));
				fs.Position--;	// because of the stupid 3-byte value, values are never near using that 4th byte
				byte div = (byte)Math.Round((decimal)(256 - (1000000/_frequency)));
				bw.Write(div);
				fs.Position++;	// CODEC is still zero
				bw.Write(_data);
				if (repeat) bw.Write((int)0x00000007);	// 7 type, + 3-byte length(0)
				fs.WriteByte(0);	// EOF block
				bw.Write(big);
				fs.SetLength(fs.Position);
				_length = len;
				UpdateRmap(fs, _type, _name, _length);
				fs.Close();
				return true;
			}
			catch { return false; }
		}

		/// <value>Raw source data</value>
		public byte[] Data { get { return _data; } set { _data = value; } }
		/// <value>Frequency in Hertz of audio data.</value>
		/// <remarks>Will only update if value is 10-12 kHz</remarks>
		public int Frequency
		{
			get { return _frequency; }
			set { if (value >= 10000 && value <= 12000) _frequency = value; }
		}
		/// <value>Gets resource type</value>
		/// <remarks>Part of every resource type, but typically unique. Will return either "BLAS" or "VOIC"</remarks>
		public string Type { get { return _type; } }
	}
}
