﻿/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2023 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 2.1
 */

/* CHANGE LOG
 * v2.1, 221030
 * [NEW] operator to SHIP
 * [NEW] Creation of Lines during Decode
 * v2.0, 210309
 * [NEW] Created
 */

using System;
using System.Collections.Generic;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "CRFT" mesh resources.</summary>
	/// <remarks>This is the original format used in X-wing.<br/>
	/// Resource is read-only.</remarks>
	/// <seealso cref="Cplx"/>
	/// <seealso cref="Ship"/>
	/// <example>
	/// <h4>Raw Data definition</h4>
	/// <code>
	/// RawData
	/// {
	///   /* 0x00 */ short Length
	///   /* 0x02 */ byte NumComponents
	///   /* 0x03 */ byte NumShadingSets
	///   /* 0x04 */ ShadingSet[NumShadingSets]
	///				 short[NumComponents] ComponentOffsets
	///				 Component[NumComponents]
	/// }
	/// 
	/// ShadingSet (size 0x10)
	/// {
	///   ??? // don't know what this does
	/// }
	/// 
	/// Component
	/// {
	///   /* 0x00 */ LodHeader[]
	/// 			 LodMesh[]
	/// }
	/// 
	/// LodHeader (size 0x6)
	/// {
	///   /* 0x00 */ int Distance
	///   /* 0x04 */ short Offset
	/// }
	/// 
	/// LodMesh
	/// {
	///   /* 0x00 */ byte Signature
	///   /* 0x01 */ byte Unknown
	///   /* 0x02 */ byte NumVertices
	///   /* 0x03 */ byte Unknown
	///   /* 0x04 */ byte NumShapes
	///   /* 0x05 */ byte[NumShapes] ColorIndices
	/// 			 Vertex16 MinimumBound
	/// 			 Vertex16 MaximumBound
	/// 			 Vertex16[NumVertices] MeshVertices
	/// 			 ShapeSettings[NumShapes]
	/// 			 Shape[NumShapes] MeshGeometry
	/// 			 Unknown1[NumShapes]
	/// 			 short NumUnk2
	/// 			 Unknown2[NumUnk2]
	/// 			 Unknown3[NumUnk2]
	/// }
	/// 
	/// Vertex16 (size 0x6)
	/// {
	///   /* 0x00 */ short X
	///   /* 0x02 */ short Y
	///   /* 0x04 */ short Z
	/// }
	/// 
	/// ShapeSettings (size 0x8)
	/// {
	///   /* 0x00 */ Vertex16 FaceNormal
	///   /* 0x06 */ short Offset
	/// }
	/// 
	/// Shape
	/// {
	///   /* 0x00 */ byte Type
	///   /* 0x01 */ byte[] Data
	/// }
	/// 
	/// Unknown1 (size 0x3)
	/// {
	///   /* 0x00 */ byte
	///   /* 0x01 */ short
	/// }
	/// 
	/// Unknown2 (size 0x3)
	/// {
	///   /* 0x00 */ byte
	///   /* 0x01 */ short Offset
	/// }
	/// 
	/// Unknown3
	/// {
	///   /* 0x00 */ byte Type?
	///   #if (Type == 1)
	///     /* 0x01 */ byte
	///     /* 0x02 */ byte ArraySize
	///     /* 0x03 */ Triplet[ArraySize]
	///   #elseif (Type == 2)
	///     /* 0x01 */ byte[16]
	///     // Don't know if length is fixed
	///   #endif
	/// }
	/// 
	/// Triplet
	/// {
	///   /* 0x00 */ byte
	///   /* 0x01 */ byte
	///   /* 0x02 */ byte
	/// }</code>
	/// <para>The first <i>Length</i> value is the remaining data after that SHORT, so it will always be (<see cref="Resource.Length"/>-2).</para>
	/// <para>The <i>ComponentsOffsets</i> values are jump offsets from the beginning of their respective value, such that if an offset value is at location p, the component will be at (p+offset).</para>
	/// <para>The <i>LodHeader.Offset</i> is a jump offset from the beginning of the LodHeader object, such that if LodHeader[i] is at location p, the Mesh will start at (p+offset).
	/// This also means that LodHeader[0].Offset will be the entire length of the array.
	/// There isn't a defined length for the LOD arrays, rather the last LOD will have a <see cref="Lod.Distance"/> of <b>0x7FFFFFFF</b>.</para>
	/// <para>In <i>LodMesh.MeshVertices</i>, if the top byte of a value is <b>0x7F</b> then it's repeating a previous vertex's value.
	/// The repeat will match the sub-type (X-X, Y-Y, Z-Z) and the appropriate index is calculated with the bottom byte, right-shifted once, and subtracted from the current index.
	/// E.g. if the current index is 5 and the Y value is 0x7F02, then it will be using MeshVertices[5 - (2 >> 1)].Y, or [4].Y.</para>
	/// <para><i>ShapeSettings.Offset</i> is a jump offset from the beginning of the ShapeSettings object, similar to LodHeader.Offset.</para>
	/// <para><see cref="Lod.Shape.Type"/> uses the bottom nibble for the number of vertices, top nibble for type as a bitfield. The first bit is IsTwoSided, the second bit is IsGouraudShaded.
	/// <see cref="Lod.Shape.Data"/> is length(3 + (numVertices* 2)).
	/// If the number of vertices is 2, then Data has a pair of vertex indexes for a line defined in Data[2] and Data[3].
	/// Otherwise, for each vertex there is a line, with the vertex indexes defined in Data[v * 2] and Data[(v + 1) * 2].</para>
	/// <para>After that there's some Unknown data which is currently not read into the class.
	/// The Offset within Unknown2 points to the Unknown3 struct, measured from Unknown2 start position.</para></example>
	public partial class Crft : Resource
	{
		bool _isCft { get { return _fileName.ToUpper().EndsWith(".CFT"); } }

		#region constructors
		/// <summary>Blank constructor.</summary>
		public Crft()
		{
			_type = ResourceType.Crft;
		}
		/// <summary>Creates a new instance from an existing opened file.</summary>
		/// <param name="stream">The opened LFD or CFT file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Crft(FileStream stream, long filePosition)
		{
			read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an existing file.</summary>
		/// <param name="path">The full path to the unopened LFD or CFT file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Crft(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			read(stream, filePosition);
			stream.Close();
		}
		#endregion constructors

		void read(FileStream stream, long filePosition)
		{
			try
			{
				_fileName = stream.Name;    // Resource._fileName: even though _process gets it, _isCft needs it first
				if (!_isCft) _process(stream, filePosition);
				else
				{
					BinaryReader br = new BinaryReader(stream);
					_offset = filePosition; // Resource._offset
					_type = ResourceType.Crft;
					// *.CFT files do not contain headers, just the raw data
					_name = StringFunctions.GetFileName(_fileName);
					DecodeResource(br.ReadBytes((int)stream.Length), false);
				}
			}
			catch (Exception x) { throw new LoadFileException(x); }
		}

		#region public methods
		/// <summary>Processes raw data to populate the resource.</summary>
		/// <param name="raw">Raw byte data.</param>
		/// <param name="containsHeader">Whether or not <paramref name="raw"/> contains the resource Header information.</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> from the LFD is not <see cref="Resource.ResourceType.Crft"/>.</exception>
		/// <remarks>If resource was created from a *.CFT file, <paramref name="containsHeader"/> is ignored.</remarks>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			if (!_isCft)
			{
				_decodeResource(raw, containsHeader);
				if (_type != ResourceType.Crft) throw new ArgumentException("Raw header is not for a Crft resource");
			}
			else
			{
				_rawData = raw;
			}

			int pos = 2;
			byte componentCount = _rawData[pos++];
			Component[] components = new Component[componentCount];
			byte shadingCount = _rawData[pos++];
			bool[] readOnly;
			if (shadingCount != 0)
			{
				Indexer<byte>[] sets = new Indexer<byte>[shadingCount];
				int setLength = 16;
				readOnly = new bool[setLength];
				for (int i = 0; i < setLength; i++) readOnly[i] = true;
				for (int i = 0; i < shadingCount; i++)
				{
					byte[] shadingSet = new byte[setLength];
					ArrayFunctions.TrimArray(_rawData, pos, shadingSet);
					pos += setLength;
					sets[i] = new Indexer<byte>(shadingSet, readOnly);
				}
				readOnly = new bool[shadingCount];
				for (int i = 0; i < shadingCount; i++) readOnly[i] = true;
				ShadingSets = new Indexer<Indexer<byte>>(sets, readOnly);
			}
			else ShadingSets = null;

			// Since we're reading the whole thing probably don't need to do this with jumps, but going to anyway
			int componentJumpStart = pos;
			short[] componentJumps = new short[componentCount];
			for (int i = 0; i < componentCount; i++)
			{
				componentJumps[i] = BitConverter.ToInt16(_rawData, pos);
				pos += 2;
			}

			for (int c = 0; c < componentCount; c++)
			{
				pos = componentJumpStart + c * 2 + componentJumps[c];
				List<int> lodDistances = new List<int>();
				List<short> lodJumps = new List<short>();
				int lodCount = 0;
				do
				{
					lodDistances.Add(BitConverter.ToInt32(_rawData, pos + lodCount * 6));
					lodJumps.Add(BitConverter.ToInt16(_rawData, pos + 4 + lodCount * 6));
					lodCount++;
				}
				while (lodDistances[lodDistances.Count - 1] != int.MaxValue);

				components[c] = new Component(lodCount);
				for (int l = 0; l < lodCount; l++)
				{
					Lod lod = components[c].Lods[l];
					lod.Distance = lodDistances[l];
					pos = componentJumpStart + c * 2 + componentJumps[c] + l * 6 + lodJumps[l];
					pos++; //skip Signature
					lod.Unknown1 = _rawData[pos++];
					byte vertexCount = _rawData[pos++];
					lod.Unknown2 = _rawData[pos++];
					byte shapeCount = _rawData[pos++];
					byte[] colors = new byte[shapeCount];
					ArrayFunctions.TrimArray(_rawData, pos, colors);
					pos += shapeCount;
					readOnly = new bool[shapeCount];
					for (int i = 0; i < shapeCount; i++) readOnly[i] = true;
					lod.ColorIndices = new Indexer<byte>(colors, readOnly);

					lod.MinimumBound = new Lod.Vertex16(_rawData, ref pos);
					lod.MaximumBound = new Lod.Vertex16(_rawData, ref pos);

					Lod.Vertex16[] vertices = new Lod.Vertex16[vertexCount];
					readOnly = new bool[vertexCount];
					for (int v = 0; v < vertexCount; v++)
					{
						vertices[v] = new Lod.Vertex16(_rawData, ref pos);
						readOnly[v] = true;

						for (int i = 0; i < 3; i++)
						{
							if ((vertices[v][i] & 0xFF00) == 0x7F00)
							{
								int delta = (vertices[v][i] & 0x00FF) >> 1;
								vertices[v][i] = vertices[v - delta][i];
							}
						}
					}
					lod.MeshVertices = new Indexer<Lod.Vertex16>(vertices, readOnly);

					Lod.Vector16[] normals = new Lod.Vector16[shapeCount];
					short[] shapeJumps = new short[shapeCount];
					int shapeJumpStart = pos;
					for (int s = 0; s < shapeCount; s++)
					{
						normals[s] = new Lod.Vector16(_rawData, ref pos);
						shapeJumps[s] = BitConverter.ToInt16(_rawData, pos);
						pos += 2;
					}
					Lod.Shape[] shapes = new Lod.Shape[shapeCount];	
					for (int s = 0; s < shapeCount; s++)
					{
						shapes[s] = new Lod.Shape();
						pos = shapeJumpStart + s * 8 + shapeJumps[s];	// this really shouldn't do anything
						shapes[s].FaceNormal = normals[s];
						shapes[s].Type = _rawData[pos++];
						int len = (shapes[s].Type & 0x0F) * 2 + 3;
						byte[] data = new byte[len];
						readOnly = new bool[len];
						for (int i = 0; i < len; i++) readOnly[i] = true;
						ArrayFunctions.TrimArray(_rawData, pos, data);
						pos += len;
						shapes[s].Data = new Indexer<byte>(data, readOnly);

                        byte shapeVertexCount = (byte)(shapes[s].Type & 0xF);
                        Lod.Line[] lines = new Lod.Line[shapeVertexCount / 2];
						readOnly = new bool[lines.Length];
						for (int i = 0; i < readOnly.Length; i++) readOnly[i] = true;
						if (shapeVertexCount == 2)
							lines[0] = new Lod.Line(shapes[s].Data[2], shapes[s].Data[3]);
						else
							for (int ln = 0; ln < lines.Length; ln++)
								lines[ln] = new Lod.Line(shapes[s].Data[ln * 2], shapes[s].Data[(ln + 1) * 2]);
						shapes[s].Lines = new Indexer<Lod.Line>(lines, readOnly);
                    }
					readOnly = new bool[shapeCount];
					for (int i = 0; i < shapeCount; i++) readOnly[i] = true;
					try
					{
						for (int s = 0; s < shapeCount; s++)
						{
							shapes[s].Unknown1 = _rawData[pos++];
							shapes[s].Unknown2 = BitConverter.ToInt16(_rawData, pos);
							pos += 2;
						}
					}
					catch { /* do nothing */ }
					lod.Shapes = new Indexer<Lod.Shape>(shapes, readOnly);

					/*short unkCount = BitConverter.ToInt16(_rawData, pos);
					pos += 2;
					if (unkCount != 0)
					{
						byte[] unkID = new byte[unkCount];
						short[] unkJumps = new short[unkCount];
						int unkJumpStart = pos;
						for (int u = 0; u < unkCount; u++)
						{
							unkID[u] = _rawData[pos++];
							unkJumps[u] = BitConverter.ToInt16(_rawData, pos);
							pos += 2;
						}
						Lod.UnknownData[] unks = new Lod.UnknownData[unkCount];
						for (int u = 0; u < unkCount; u++)
						{
							pos = unkJumpStart + u * 3 + unkJumps[u];
							unks[u] = new Lod.UnknownData
							{
								Type = _rawData[pos],
								Unknown = unkID[u]
							};
							byte[] data = null;
							if (unks[u].Type == 1)
							{
								data = new byte[_rawData[pos + 2] * 3 + 2];
							}
							else if (unks[u].Type == 2)
							{
								data = new byte[16];
							}
							ArrayFunctions.TrimArray(_rawData, pos + 1, data);
							readOnly = new bool[data.Length];
							for (int i = 0; i < data.Length; i++) readOnly[i] = true;
							unks[u].Data = new Indexer<byte>(data, readOnly);
						}
						readOnly = new bool[unkCount];
						for (int i = 0; i < unkCount; i++) readOnly[i] = true;
						lod.UnkData = new Indexer<Lod.UnknownData>(unks, readOnly);
					}
					else { lod.UnkData = null; }*/
				}
			}
			readOnly = new bool[componentCount];
			for (int i = 0; i < componentCount; i++) readOnly[i] = true;
			Components = new Indexer<Component>(components, readOnly);
		}
		#endregion public methods

		/// <summary>Gets the components of the model.</summary>
		/// <remarks>Each component is read-only.</remarks>
		public Indexer<Component> Components { get; private set; }
		/// <summary>Gets the shading data.</summary>
		/// <remarks>Each set is read-only.</remarks>
		public Indexer<Indexer<byte>> ShadingSets { get; private set; }

        /// <summary>Transfers the wireframe data into a SHIP object.</summary>
        /// <param name="craft">The CRFT wireframe data</param>
        /// <returns>A SHIP with <u>only</u> the wireframe data from <see cref="Components"/>. <see cref="Ship.Unknown"/> is set to <b>0</b>,
        /// <see cref="Ship.Unknowns"/> and <see cref="Ship.ShadingSets"/> are both set to <b>null</b>.
		/// Each <see cref="Cplx.Lod.VertexNormals"/> will be also be set to <b>null</b>.</returns>
        public static implicit operator Ship(Crft craft)
        {
			var ship = new Ship
			{
				Name = craft.Name,
				Unknowns = null,
				Unknown = 0,
				ShadingSets = null
			};
			byte componentCount = (byte)craft.Components.Length;
            var components = new Ship.Component[componentCount];
			bool[] readOnly;
            for (int c = 0; c < componentCount; c++)
            {
                byte lodCount = (byte)craft.Components[c].Lods.Length;
                components[c] = new Ship.Component(lodCount)
                {
                    MeshType = Ship.MeshType.Default,
                };

                var lods = new Cplx.Lod[lodCount];
                readOnly = new bool[lodCount];
                for (int i = 0; i < lodCount; i++)
                {
					lods[i] = new Cplx.Lod
					{
						Distance = craft.Components[c].Lods[i].Distance,
						ColorIndices = craft.Components[c].Lods[i].ColorIndices,
						MinimumBound = craft.Components[c].Lods[i].MinimumBound,
						MaximumBound = craft.Components[c].Lods[i].MaximumBound,
						MeshVertices = craft.Components[c].Lods[i].MeshVertices,
						Shapes = craft.Components[c].Lods[i].Shapes,
						VertexNormals = null
					};
					readOnly[i] = true;
                }
                components[c].Lods = new Indexer<Cplx.Lod>(lods, readOnly);
            }
            readOnly = new bool[componentCount];
            for (int i = 0; i < componentCount; i++) readOnly[i] = true;
            ship.Components = new Indexer<Ship.Component>(components, readOnly);

            return ship;
        }
    }
}
