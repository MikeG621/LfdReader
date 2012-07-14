/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2010-2012 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the GPL v3.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.0
 */
 
/* CHANGELOG
 * 110922 - housekeeping, added LoadFileException throw
 * 110926 - implemented DecodeResource()
 * 111108 - added ArrayFunctions calls
 * 120411 - added Chunk.Length, Block.Length to dynamic, prelim EncodeResource
 * 120415 - OpCode.UnknownC to Shift
 * 120425 - ResourceType check
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
	///   /* 0x00 */ Image[] Images;
	/// }
	/// 
	/// struct Image
	/// {
	///   /* 0x00 */ Row[] Rows;
	///   /* 0x?? */ byte EndImage = 0xFF;
	/// }
	/// 
	/// struct Row
	/// {
	///   /* 0x00 */ OpCode[] Operations;
	///   /* 0x?? */ byte EndRow = 0xFE;
	/// }
	///
	/// struct OpCode
	/// {
	///   /* 0x00 */ byte Value;
	///	  #if (Value == 0xFD)	// repeat Type 1
	///	    /* 0x01 */ byte NumberOfRepeats;
	///     /* 0x02 */ byte ColorIndex;
	///	  #elseif (Value == 0xFC)	// repeat Type 2
	///     /* 0x01 */ byte ColorIndex;
	///     /* 0x02 */ byte NumberOfRepeats;
	///   #else	// repeat Type 3 (short)
	///	  #endif
	/// }</code>
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
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> is not <see cref="ResourceType.Film"/></exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			//System.Diagnostics.Debug.WriteLine("Decode FILM");
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Film) throw new ArgumentException("Raw header is not for a Film resource");
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
				ArrayFunctions.TrimArray(_rawData, offset + 0x12, block);
				//for (int i = 0; i < block.Length; i++) block[i] = BitConverter.ToInt16(_rawData, offset + HeaderLength + 2 + i * 2);
				//System.Diagnostics.Debug.WriteLine("copied, creating block...");
				_blocks[i] = new Block(type, name, block);
				offset += len;
			}
		}
		/// <summary>Prepares the resource for writing and updates <see cref="RawData"/></summary>
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
			public short NumberOfChunks { get { return (short)_chunks.Length; } }
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
				/// <summary>(<see cref="BlockType.Anim"/>, <see cref="BlockType.Delt"/>) Move the resource to a new location for animation purposes</summary>
				Move,
				/// <summary>(<see cref="BlockType.Anim"/>, <see cref="BlockType.Delt"/>) The speed at which the <see cref="OpCode.Move"/> command is executed</summary>
				Speed,
				/// <summary>(<see cref="BlockType.Anim"/>, <see cref="BlockType.Delt"/>) Establishes the drawing order for the resource</summary>
				Layer,
				/// <summary>(<see cref="BlockType.Anim"/>, <see cref="BlockType.Delt"/>) Display the resource beginning from a specific frame</summary>
				Frame,
				/// <summary>(<see cref="BlockType.Anim"/>) The direction and rate of display for an animation resource</summary>
				Animation,
				/// <summary>(<see cref="BlockType.Anim"/>, <see cref="BlockType.Delt"/>, <see cref="BlockType.Cust"/>) Executable command</summary>
				Event,
				/// <summary>(<see cref="BlockType.Anim"/>) Executable-defined screen location for Click events</summary>
				Region,
				/// <summary>(<see cref="BlockType.Anim"/>, <see cref="BlockType.Delt"/>) Displays a portion of the resource</summary>
				Window,
				/// <summary>(<see cref="BlockType.Delt"/>) Offset the image location</summary>
				Shift,
				/// <summary>(<see cref="BlockType.Anim"/>, <see cref="BlockType.Delt"/>, <see cref="BlockType.Cuat"/>) Show or hide the resource</summary>
				Display,
				/// <summary>(<see cref="BlockType.Anim"/>, <see cref="BlockType.Delt"/>) Flip the X/Y display of the resource</summary>
				Orientation,
				/// <summary>(<see cref="BlockType.Pltt"/>) Marker to activate the palette</summary>
				Use,
				/// <summary>(<see cref="BlockType.View"/>) Unknown</summary>
				Unknown11 = 0x11,
				/// <summary>(<see cref="BlockType.View"/>) Method of clearing the screen and loading the new View</summary>
				Transition,
				/// <summary>(<see cref="BlockType.Voic"/>) Unknown</summary>
				Unknown13,
				/// <summary>(<see cref="BlockType.Voic"/>) Appears to stop sounds interally set to repeat</summary>
				Loop,
				/// <summary>(<see cref="BlockType.Voic"/>) Unknown</summary>
				Unknown17 = 0x17,
				/// <summary>(<see cref="BlockType.Voic"/>) Possibly defines file the sound is located in</summary>
				Preload,
				/// <summary>(<see cref="BlockType.Voic"/>) Determines playback controls</summary>
				Sound,
				/// <summary>(<see cref="BlockType.Voic"/>) Determines stereo playback controls</summary>
				Stereo = 0x1C }

			/// <summary>Gets or sets the command instruction for the Chunk</summary>
			public OpCode Code { get; set; }
			/// <summary>Gets or sets the arguments for the chunk (when applicable)</summary>
			public short[] Vars { get; set; }
			/// <summary>Gets the total byte length of the Chunk</summary>
			public short Length { get { return (short)(4 + (Vars != null ? Vars.Length << 1 : 0)); } }
		}
	}
}
