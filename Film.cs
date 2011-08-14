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
	/// <remarks>Reads LFD files and interprets the FILM resource type</remarks>
	public class Film : Resource
	{
		private short _numberOfFrames;
		private Block[] _blocks;

		/// <param name="stream">The FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Film(FileStream stream, long filePosition)
		{
			Read(stream, filePosition);
		}
		/// <param name="path">The full path of the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Film(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			Read(fsLFD, filePosition);
			fsLFD.Close();
		}

		private void Read(FileStream stream, long filePosition)
		{
			BinaryReader br = new BinaryReader(stream);
			_fileName = stream.Name;	// Resource.filename
			_offset = filePosition;	// Resource.offset
			stream.Position = _offset + NameOffset;
			_name = new string(br.ReadChars(8)).Trim('\0');	// Resource.name
			_length = br.ReadInt32();	// Resource.length
			stream.Position += 2;
			_numberOfFrames = br.ReadInt16();
			short numblocks = br.ReadInt16();
			_blocks = new Block[numblocks];
			for (int i=0;i<numblocks;i++)
			{
				string t = new string(br.ReadChars(4)).Trim('\0');	// need the Trim for END blocks
				string n = new string(br.ReadChars(8)).Trim('\0');
				int l = br.ReadInt32();
				stream.Position += 2;
				short[] raw = new short[(l-0x12)/2];
				for (int j=0;j<raw.Length;j++) raw[j] = br.ReadInt16();	// see if Buffer.BlockCopy cna be used here
				_blocks[i] = new Block(t, n, l, raw);
			}
		}


		/// <value>Gets number of frames required for FILM animation</value>
		public short NumberOfFrames { get { return _numberOfFrames; } }
		/// <value>Gets the raw Block data from the FILM</value>
		public Block[] Blocks { get { return _blocks; } }
		/// <value>Gets the number of Blocks contained in the FILM</value>
		public short NumberOfBlocks { get { return (short)_blocks.Length; } }

		/// <remarks>Represents the primary mechanism that controls FILM behaviour</remarks>
		public struct Block
		{
			private string _type;
			private short _typeNum;
			private string _name;
			public int Length;
			private Chunk[] _chunks;

			/// <param name="strType">Resource type Block pertains to</param>
			/// <param name="strName">Identifying name of Block</param>
			/// <param name="intLength">Size of Block data</param>
			/// <param name="Data">Raw Block data</param>
			public Block(string type, string name, int length, short[] rawData)
			{
				/*_type = type;
				_typeNum = 0;
				if (_type == "END") _typeNum = 1;
				else if (_type == "VIEW") _typeNum = 2;
				else if (_type == "ANIM" || _type == "DELT" || _type == "CUST") _typeNum = 3;
				else if (_type == "PLTT") _typeNum = 4;
				else if (_type == "VOIC") _typeNum = 5;*/
				Type = type;
				_name = name;
				Length = length;
				_chunks = new Chunk[rawData[0]];
				for(int i=0, j=2;i<rawData[0];i++)
				{
					short t = rawData[j+1];	// op code
					if (t == 2 || t == 0x11) _chunks[i].Code = t;	// takes no args
					else
					{
						short[] vars = new short[rawData[j]/2-2];
						for(int k=0;k<vars.Length;k++) vars[k] = rawData[j+2];	// Buffer.BlockCopy?
						_chunks[i].Code = t;
						_chunks[i].Vars = vars;
					}
					j += rawData[j]/2;
				}
			}


			/// <value>4 char block type</value>
			public string Type
			{ 
				get { return _type; }
				set
				{
					if (value.Length > 4) _type = value.Substring(0, 4);
					else _type = value;
					if (_type == "END") _typeNum = 1;
					else if (_type == "VIEW") _typeNum = 2;
					else if (_type == "ANIM" || _type == "DELT" || _type == "CUST") _typeNum = 3;
					else if (_type == "PLTT") _typeNum = 4;
					else if (_type == "VOIC") _typeNum = 5;
					else _typeNum = 0;
				}
			}
			/// <value>Gets the numerical code for the Block's Type</value>
			public short TypeNum { get { return _typeNum; } }
			/// <value>8 char block name</value>
			public string Name
			{
				get { return _name; }
				set
				{
					if (value.Length > 8) _name = value.Substring(0, 8);
					else _name = value;
				}
			}
			/// <value>Gets Chunk data of the Block</value>
			public Chunk[] Chunks { get { return _chunks; } }
			/// <value>Gets the number of Chunks contained in the Block</value>
			public short NumberOfChunks { get { return (short)_chunks.Length; } }
		}
		/// <remarks>Represents individual commands contained within Blocks</remarks>
		public struct Chunk
		{
			/// <value>The command instruction for the Chunk</value>
			public short Code;
			/// <value>The arguments for the chunk (when applicable)</value>
			public short[] Vars;
		}
	}
}
