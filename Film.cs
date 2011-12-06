/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2011 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in Resource.cs
 * Version: 0.9
 */
 
/* CHANGELOG
 * 110922 - housekeeping, added LoadFileException throw
 * 110926 - implemented DecodeResource()
 * 111108 - added ArrayFunctions calls
 */

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <remarks>Reads LFD files and interprets the FILM resource type</remarks>
	public class Film : Resource
	{
		short _numberOfFrames;
		Block[] _blocks;

		/// <param name="stream">The FileStream of the opened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Film(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <param name="path">The full path of the unopened LFD file</param>
		/// <param name="filePosition">The File.Position of the beginning of the resource</param>
		public Film(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			_read(fsLFD, filePosition);
			fsLFD.Close();
		}

		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		//===================
		/// <summary>Processes raw data to create Film information</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Determines if <i>raw</i> contains the Header</param>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			//System.Diagnostics.Debug.WriteLine("Decode FILM");
			_decodeResource(raw, containsHeader);
			_numberOfFrames = BitConverter.ToInt16(_rawData, 2);
			_blocks = new Block[BitConverter.ToInt16(_rawData, 4) + 1];
			int offset = 6;
			for (int i = 0; i < _blocks.Length; i++)
			{
				//System.Diagnostics.Debug.WriteLine("Block " + (i + 1) + " of " + _blocks.Length);
				Block.BlockType type = (Block.BlockType)BitConverter.ToInt32(_rawData, offset + TypeOffset);
				string name = ArrayFunctions.ReadStringFromArray(_rawData, offset + NameOffset, 8);
				int len = BitConverter.ToInt32(_rawData, offset + LengthOffset);
				//System.Diagnostics.Debug.WriteLine(type + name + ", " + len);
				short[] block = new short[(len - 0x12) / 2];
				//System.Diagnostics.Debug.WriteLine("block len: " + block.Length);
				//System.Diagnostics.Debug.WriteLine("copying raw...");
				ArrayFunctions.TrimArray(_rawData, offset + HeaderLength + 2, block);
				//for (int i = 0; i < block.Length; i++) block[i] = BitConverter.ToInt16(_rawData, offset + HeaderLength + 2 + i * 2);
				//System.Diagnostics.Debug.WriteLine("copied, creating block...");
				_blocks[i] = new Block(type, name, len, block);
				offset += len;
			}
		}
		/// <summary>Prepare Film information for writing</summary>
		/// <returns>Raw data ready to write to file</returns>
		public override void EncodeResource()
		{
			throw new NotImplementedException();
		}

		/// <value>Gets number of frames required for FILM animation</value>
		public short NumberOfFrames { get { return _numberOfFrames; } }
		/// <value>Gets the raw Block data from the FILM</value>
		public Block[] Blocks { get { return _blocks; } }
		/// <value>Gets the number of Blocks contained in the FILM</value>
		public short NumberOfBlocks { get { return (short)_blocks.Length; } }

		/// <summary>Represents the primary mechanism that controls FILM behaviour</summary>
		public struct Block
		{
			BlockType _type;
			string _name;
			int _length;
			Chunk[] _chunks;

			/// <summary>Initialize a new Block</summary>
			/// <param name="type">Resource type Block pertains to</param>
			/// <param name="name">Identifying name of Block</param>
			/// <param name="length">Size of Block data</param>
			/// <param name="rawData">Raw Block data</param>
			public Block(BlockType type, string name, int length, short[] rawData)
			{
				_type = type;
				_name = name;
				_length = length;
				if (rawData.Length == 0)
				{
					_chunks = null;
					return;
				}
				_chunks = new Chunk[rawData[0]];
				for (int i = 0, j = 2; i < rawData[0]; i++)
				{
					short t = rawData[j + 1];	// op code
					if (t == 2 || t == 0x11) _chunks[i].Code = (Chunk.OpCode)t;	// takes no args
					else
					{
						short[] vars = new short[(rawData[j] - 4) >> 1];
						//for(int k=0;k<vars.Length;k++) vars[k] = rawData[j+2];	// Buffer.BlockCopy?
						ArrayFunctions.TrimArray(rawData, (j + 2) << 1, vars);
						//^Buffer.BlockCopy(rawData, (j + 2) << 1, vars, 0, vars.Length << 1);
						_chunks[i].Code = (Chunk.OpCode)t;
						_chunks[i].Vars = vars;
					}
					j += rawData[j] >> 1;
				}
			}

			/// <summary>Gets the Block length</summary>
			public int Length
			{
				get { return _length; }
				internal set { _length = value; }
			}
			/// <summary>Gets or sets the Block type</summary>
			public BlockType Type
			{ 
				get { return _type; }
				set { _type = value; }
			}
			/// <summary>Gets the numerical category for the Block's Type</summary>
			public short TypeNum
			{
				get
				{
					if (_type == BlockType.End) return 1;
					else if (_type == BlockType.View) return 2;
					else if (_type == BlockType.Anim || _type == BlockType.Delt || _type == BlockType.Cust) return 3;
					else if (_type == BlockType.Pltt) return 4;
					else if (_type == BlockType.Voic) return 5;
					else return -1;
				}
			}
			/// <summary>Gets or sets the 8 char Block name</summary>
			public string Name
			{
				get { return _name; }
				set
				{
					if (value.Length > 8) _name = value.Substring(0, 8);
					else _name = value;
				}
			}
			/// <summary>Gets Chunk data of the Block</summary>
			public Chunk[] Chunks { get { return _chunks; } }
			/// <summary>Gets the number of Chunks contained in the Block</summary>
			public short NumberOfChunks { get { return (short)_chunks.Length; } }
			
			/// <summary>Preset Block types</summary>
			public enum BlockType : int { Undefined, Anim = 0x4D494E41, Cust = 0x54535543, Delt = 0x544C4544, End = 0x00444E45,
				Pltt = 0x54544C50, View = 0x57454956, Voic = 0x43494F56 }
		}
		/// <summary>Represents individual commands contained within Blocks</summary>
		public struct Chunk
		{
			public enum OpCode : short { Unknown0, Unknown1, End, Time, Move, Speed, Layer, Frame, Animation, Event, Region, Window,
				UnknownC, Display, Orientation, Use, Unknown10, Unknown11, Transition, Unknown13, Loop, Unknown15, Unknown16, Unknown17,
				Preload, Sound, Unknown1A, Unknown1B, Stereo }

			/// <summary>Gets or sets the command instruction for the Chunk</summary>
			public OpCode Code { get; set; }
			/// <summary>Gets or sets the arguments for the chunk (when applicable)</summary>
			public short[] Vars { get; set; }
		}
	}
}
