/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2020 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.1+
 */

/* CHANGE LOG
 * [ADD} ToString for Block and Chunk
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using System;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "FILM" layout resources</summary>
	/// <remarks>The Film resource controls everything you see outside of the flight engine. This is where the images are controlled regarding the colors used, when the image is shown, draw order, animation controls, etc. Many of the mouse-click regions are defined here as well, which then activate various animations (doors, etc) or sound effects.<hr/>
	/// <h4>Raw Data definition</h4>
	/// <code>// Pseudo-code resource structure
	/// struct RawData
	/// {
	///   /* 0x00 */ short Reserved = 4;
	///   /* 0x02 */ short NumberOfFrames;
	///   /* 0x04 */ short NumberOfBlocks; // zero-indexed
	///   /* 0x06 */ Block[NumberOfBlocks + 1] Blocks;
	/// }
	///
	/// struct Block
	/// {
	///   /* 0x00 */ char[4] Type;
	///   /* 0x04 */ char[8] Name;
	///   /* 0x0C */ int Length;
	///   /* 0x10 */ short TypeIndex;
	///   /* 0x12 */ short NumberOfChunks;
	///   /* 0x14 */ short ChunkDataLength;
	///   /* 0x16 */ Chunk[NumberOfChunks] Chunks;
	/// }
	///
	/// struct Chunk
	/// {
	///   /* 0x00 short Length;
	///   /* 0x02 OpCode[] Codes;
	/// }</code>
	/// order of Blocks is usually VIEW, VOIC, PLTT, ANIM/DELT/CUST
	/// stuff<br/><br/>
	/// -- Section --<br/><br/>
	/// stuff</remarks>
	public class Film : Resource
	{
		short _numberOfFrames;
		Block[] _blocks;

		#region constructors
		// No default constructor for FILM, must read from file
		
		/// <summary>Create a new instance from an existing opened file</summary>
		/// <param name="stream">The opened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		/// <exception cref="Idmr.Common.LoadFileException">Typically due to file corruption</exception>
		public Film(FileStream stream, long filePosition)
		{
			_read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an exsiting file</summary>
		/// <param name="path">The full path of the unopened LFD file</param>
		/// <param name="filePosition">The offset of the beginning of the resource</param>
		public Film(string path, long filePosition)
		{
			FileStream fsLFD = File.OpenRead(path);
			_read(fsLFD, filePosition);
			fsLFD.Close();
		}
		#endregion constructors

		void _read(FileStream stream, long filePosition)
		{
			try { _process(stream, filePosition); }
			catch (Exception x) { throw new LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to populate the resource</summary>
		/// <param name="raw">Raw byte data</param>
		/// <param name="containsHeader">Whether or not <i>raw</i> contains the resource Header information</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="Resource.ResourceType.Film"/></exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Film) throw new ArgumentException("Raw header is not for a Film resource");
			_numberOfFrames = BitConverter.ToInt16(_rawData, 2);
			_blocks = new Block[BitConverter.ToInt16(_rawData, 4) + 1];
			int offset = 6;
			for (int i = 0; i < _blocks.Length; i++)
			{
				Block.BlockType type = (Block.BlockType)BitConverter.ToInt32(_rawData, offset + TypeOffset);
				string name = ArrayFunctions.ReadStringFromArray(_rawData, offset + NameOffset, 8);
				int len = BitConverter.ToInt32(_rawData, offset + LengthOffset);
				short[] block = new short[(len - 0x12) / 2];
				ArrayFunctions.TrimArray(_rawData, offset + 0x12, block);
				_blocks[i] = new Block(type, name, block);
				offset += len;
			}
		}
		/// <summary>Prepares the resource for writing and updates <see cref="Resource.RawData"/></summary>
		public override void EncodeResource()
		{
			int length = 4;
			for (int b = 0; b < NumberOfBlocks; b++) length += _blocks[b].Length;
			byte[] raw = new byte[length];
			int position = 0;
			ArrayFunctions.WriteToArray(NumberOfFrames, raw, ref position);
			ArrayFunctions.WriteToArray(NumberOfBlocks, raw, ref position);
			foreach(Block b in Blocks)
			{
				ArrayFunctions.WriteToArray((int)b.Type, raw, ref position);
				ArrayFunctions.WriteToArray(b.Name, raw, position);
				position += 8;
				ArrayFunctions.WriteToArray(b.Length, raw, ref position);
				ArrayFunctions.WriteToArray(b.TypeNum, raw, ref position);
				ArrayFunctions.WriteToArray(b.NumberOfChunks, raw, ref position);
				ArrayFunctions.WriteToArray((short)(b.Length - 22), raw, ref position);
				foreach(Chunk c in b.Chunks)
				{
					ArrayFunctions.WriteToArray(c.Length, raw, ref position);
					ArrayFunctions.WriteToArray((short)c.Code, raw, ref position);
					if (c.Vars != null) ArrayFunctions.WriteToArray(c.Vars, raw, ref position);
				}
			}
			_rawData = raw;
		}
		#endregion public methods

		#region public properties
		/// <summary>Gets the number of animation frames</summary>
		public short NumberOfFrames { get { return _numberOfFrames; } }
		/// <summary>Gets the raw Block data</summary>
		public Block[] Blocks { get { return _blocks; } }
		/// <summary>Gets the number of <see cref="Block">Blocks</see> contained in the resource</summary>
		public short NumberOfBlocks { get { return (short)_blocks.Length; } }
		#endregion public properties

		/// <summary>Container for the primary mechanism that controls FILM behaviour</summary>
		public struct Block
		{
			BlockType _type;
			string _name;
			Chunk[] _chunks;
			
			/// <summary>Preset Block types</summary>
			public enum BlockType : int {
				/// <summary>Default uninitialized value</summary>
				Undefined,
				/// <summary>Commands for an <see cref="Anim"/> Resource</summary>
				Anim = 0x4D494E41,
				/// <summary>Commands for a Cust Resource</summary>
				Cust = 0x54535543,
				/// <summary>Commands for a <see cref="Delt"/> Resource</summary>
				Delt = 0x544C4544,
				/// <summary>Marks the end of the Film</summary>
				End = 0x00444E45,
				/// <summary>Commands for a <see cref="Pltt"/> Resource</summary>
				Pltt = 0x54544C50,
				/// <summary>Commands for the layout of the Film</summary>
				View = 0x57454956,
				/// <summary>Commands for <see cref="Blas"/> and <see cref="Blas">Voic</see> Resources</summary>
				Voic = 0x43494F56 }

			/// <summary>Initializes a new Block</summary>
			/// <param name="type">Resource type</param>
			/// <param name="name">Identifying name</param>
			/// <param name="rawData">Raw data</param>
			public Block(BlockType type, string name, short[] rawData)
			{
				_type = type;
				_name = name;
				if (rawData.Length == 0)
				{
					_chunks = null;
					return;
				}
				_chunks = new Chunk[rawData[0]];
				for (int c = 0, j = 2; c < rawData[0]; c++)
				{
					short code = rawData[j + 1];	// op code
					_chunks[c].Code = (Chunk.OpCode)code;
					if (code != 2 && code != 0x11)
					{
						short[] vars = new short[(rawData[j] - 4) >> 1];
						ArrayFunctions.TrimArray(rawData, (j + 2) << 1, vars);
						_chunks[c].Vars = vars;
					}
					j += rawData[j] >> 1;
				}
			}

			/// <summary>Gets the total byte length of the Block</summary>
			public int Length
			{
				get
				{
					short length = 0x16;	// HeaderLength + TypeNum + NumberOfChunks + ??
					for (int c = 0; c < NumberOfChunks; c++) length += _chunks[c].Length;
					return length;
				}
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
			/// <summary>Gets or sets the Block name</summary>
			/// <remarks>Restricted to 8 characters</remarks>
			public string Name
			{
				get { return _name; }
				set { _name = StringFunctions.GetTrimmed(value, 8); }
			}
			/// <summary>Gets Chunk data of the Block</summary>
			public Chunk[] Chunks { get { return _chunks; } }
			/// <summary>Gets the number of Chunks contained in the Block</summary>
			public short NumberOfChunks { get { return (short)(_chunks != null ? _chunks.Length : 0); } }
			/// <summary>Gets a representative string of the Block</summary>
			/// <returns>Block in the format <see cref="Type">TYPE</see> <see cref="Name"/></returns>
			public override string ToString()
			{
				return Enum.GetName(typeof(BlockType), _type).ToUpper() + _name;
			}
		}
		/// <summary>Container for individual commands contained within <see cref="Block">Blocks</see></summary>
		public struct Chunk
		{
			/// <summary>Individual commands</summary>
			public enum OpCode : short {
				/// <summary>Marker for the end of a <see cref="Block"/></summary>
				End = 2,
				/// <summary>Sets the time to execute the following commands</summary>
				Time,
				/// <summary>(<see cref="Block.BlockType.Anim"/>, <see cref="Block.BlockType.Delt"/>) Move the resource to a new location for animation purposes</summary>
				Move,
				/// <summary>(<see cref="Block.BlockType.Anim"/>, <see cref="Block.BlockType.Delt"/>) The speed at which the <see cref="OpCode.Move"/> command is executed</summary>
				Speed,
				/// <summary>(<see cref="Block.BlockType.Anim"/>, <see cref="Block.BlockType.Delt"/>) Establishes the drawing order for the resource</summary>
				Layer,
				/// <summary>(<see cref="Block.BlockType.Anim"/>, <see cref="Block.BlockType.Delt"/>) Display the resource beginning from a specific frame</summary>
				Frame,
				/// <summary>(<see cref="Block.BlockType.Anim"/>) The direction and rate of display for an animation resource</summary>
				Animation,
				/// <summary>(<see cref="Block.BlockType.Anim"/>, <see cref="Block.BlockType.Delt"/>, <see cref="Block.BlockType.Cust"/>) Executable command</summary>
				Event,
				/// <summary>(<see cref="Block.BlockType.Anim"/>) Executable-defined screen location for Click events</summary>
				Region,
				/// <summary>(<see cref="Block.BlockType.Anim"/>, <see cref="Block.BlockType.Delt"/>) Displays a portion of the resource</summary>
				Window,
				/// <summary>(<see cref="Block.BlockType.Delt"/>) Offset the image location</summary>
				Shift,
				/// <summary>(<see cref="Block.BlockType.Anim"/>, <see cref="Block.BlockType.Delt"/>, <see cref="Block.BlockType.Cust"/>) Show or hide the resource</summary>
				Display,
				/// <summary>(<see cref="Block.BlockType.Anim"/>, <see cref="Block.BlockType.Delt"/>) Flip the X/Y display of the resource</summary>
				Orientation,
				/// <summary>(<see cref="Block.BlockType.Pltt"/>) Marker to activate the palette</summary>
				Use,
				/// <summary>(<see cref="Block.BlockType.View"/>) Unknown</summary>
				Unknown11 = 0x11,
				/// <summary>(<see cref="Block.BlockType.View"/>) Method of clearing the screen and loading the new View</summary>
				Transition,
				/// <summary>(<see cref="Block.BlockType.Voic"/>) Unknown</summary>
				Unknown13,
				/// <summary>(<see cref="Block.BlockType.Voic"/>) Appears to stop sounds interally set to repeat</summary>
				Loop,
				/// <summary>(<see cref="Block.BlockType.Voic"/>) Unknown</summary>
				Unknown17 = 0x17,
				/// <summary>(<see cref="Block.BlockType.Voic"/>) Possibly defines file the sound is located in</summary>
				Preload,
				/// <summary>(<see cref="Block.BlockType.Voic"/>) Determines playback controls</summary>
				Sound,
				/// <summary>(<see cref="Block.BlockType.Voic"/>) Determines stereo playback controls</summary>
				Stereo = 0x1C }

			/// <summary>Gets or sets the command instruction for the Chunk</summary>
			public OpCode Code { get; set; }
			/// <summary>Gets or sets the arguments for the chunk (when applicable)</summary>
			public short[] Vars { get; set; }
			/// <summary>Gets the total byte length of the Chunk</summary>
			public short Length { get { return (short)(4 + (Vars != null ? Vars.Length << 1 : 0)); } }

			/// <summary>Gets a representation of the command</summary>
			/// <returns>Command in the form of <see cref="Code"/>: <see cref="Vars"/>[0], Vars[1]...</returns>
			public override string ToString()
			{
				string str = Enum.GetName(typeof(OpCode), Code);
				if (Vars != null)
				{
					str += ":";
					for (int i = 0; i < Vars.Length; i++) str += " " + Vars[i];
				}
				return str;
			}
		}
	}
}
