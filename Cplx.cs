/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2021 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.2.2+
 */

/* CHANGE LOG
 * [ADD] Created
 */

using System;
using System.Collections.Generic;
using System.IO;
using Idmr.Common;

namespace Idmr.LfdReader
{
	/// <summary>Object for "CPLX" mesh resources.</summary>
	/// <remarks>This is the second iteration of the craft mesh format used in X-wing, adds the vertex normals.<br/>
	/// Resource is read-only.</remarks>
	/// <seealso cref="Crft"/>
	/// <seealso cref="Ship"/>
	/// <example>
	/// <b>*WARNING*:</b> The entire RawData is Big-endian!
	/// <h4>Raw Data definition</h4>
	/// <code>// Pseudo-code resource structure
	/// struct RawData
	/// {
	///		0x00	SHORT	Length
	///		0x02	BYTE NumComponents
	///		0x03	BYTE NumShadingSets
	///		0x04	ShadingSet[NumShadingSets]
	///				SHORT[NumComponents]    ComponentOffsets
	///				Component[NumComponents]
	/// }
	/// 
	/// struct ShadingSet (size 0x10)
	/// {
	/// 	???	// don't know what this does
	/// }
	/// 
	/// struct Component
	/// {
	/// 	0x00	LodHeader[]
	/// 			LodMesh[]
	/// }
	/// 
	/// struct LodHeader (size 0x6)
	/// {
	/// 	0x0 INT Distance
	/// 	0x4 SHORT Offset
	/// }
	/// 
	/// struct LodMesh
	/// {
	/// 	0x00	BYTE Signature
	/// 	0x01	BYTE Unknown
	/// 	0x02	BYTE NumVertices
	/// 	0x03	BYTE Unknown
	/// 	0x04	BYTE NumShapes
	/// 	0x05	BYTE[NumShapes] ColorIndices
	/// 			Vertex16 MinimumBound
	/// 			Vertex16 MaximumBound
	/// 			Vertex16[NumVertices] MeshVertices
	/// 			Vertex16[NumVertices] VertexNormals
	/// 			ShapeSettings[NumShapes]
	/// 			Shape[NumShapes]    MeshGeometry
	/// 			Unknown1[NumShapes]
	/// 			SHORT NumUnk2
	/// 			Unknown2[NumUnk2]
	/// 			Unknown3[NumUnk2]
	/// }
	/// 
	/// struct Vertex16 (size 0x6)
	/// {
	/// 	0x0 SHORT X
	/// 	0x2 SHORT Y
	/// 	0x4 SHORT Z
	/// }
	/// 
	/// struct ShapeSettings (size 0x8)
	/// {
	/// 	0x0 Vertex16 FaceNormal
	/// 	0x6 SHORT Offset
	/// }
	/// 
	/// struct Shape
	/// {
	/// 	0x0	BYTE Type
	/// 	0x1	BYTE[] Data
	/// }
	/// 
	/// struct Unknown1 (size 0x3)
	/// {
	/// 	0x0 BYTE
	/// 	0x1 SHORT
	/// }
	/// 
	/// struct Unknown2 (size 0x3)
	/// {
	/// 	0x0 BYTE
	/// 	0x1 SHORT Offset
	/// }
	/// 
	/// struct Unknown3
	/// {
	/// 	0x00	BYTE Type?
	/// 	#if Type==1
	/// 		0x01	BYTE
	/// 		0x02	BYTE ArraySize
	/// 		0x03	Triplet[ArraySize]
	/// 	#elseif Type==2
	/// 		0x01	BYTE[16]
	/// 	// Don't know if length is fixed
	///		#endif
	/// }
	/// 
	/// struct Triplet
	/// {
	/// 	// Thanks to LE/BE CRFT/CPLX comparisons, these are definitely BYTE,
	/// 	// and not a BYTE/SHORT combo
	/// 	0x0	BYTE
	/// 	0x1	BYTE
	/// 	0x2	BYTE
	/// }
	/// </code>
	/// <para>The first Length value is the remaining data after that SHORT, so it will always be (Resource.Length-2).</para>
	/// <para>The ComponentsOffsets values are jump offsets from the beginning of their respective value, such that if an offset value is at location p, the component will be at(p+offset).</para>
	/// <para>The LodHeader.Offset is a jump offset from the beginning of the LodHeader object, such that if LodHeader[i] is at location p, the Mesh will start at (p+offset).
	/// This also means that LodHeader[0].Offset will be the entire length of the array.
	/// There isn't a defined length for the LOD arrays, rather the last LOD will have a Distance of 0x7FFFFFFF.</para>
	/// <para>In LodMesh.MeshVertices, if the top byte of a value is 0x7F then it's repeating a previous vertex's value.
	/// The repeat will match the sub-type (X-X, Y-Y, Z-Z) and the appropriate index is calculated with the bottom byte, right-shifted once, and subtracted from the current index.
	/// E.g. if the current index is 5 and the Y value is 0x7F02, then it will be using MeshVertices[5 - (2 >> 1)].Y, or [4].Y.</para>
	/// <para>ShapeSettings.Offset is a jump offset from the beginning of the ShapeSettings object, similar to LodHeader.Offset.</para>
	/// <para>Shape.Type uses the bottom nibble for the number of vertices, top nibble for type. Data is length(3 + (numVertices* 2)).
	/// If the number of vertices is 2, then Data has a pair of vertex indexes for a line defined in Data[2] and Data[3].
	/// Otherwise, for each vertex there is a line, with the vertex indexes defined in Data[v * 2] and Data[(v + 1) * 2].</para>
	/// <para>After that there's some Unknown data which is currently not read into the class.
	/// The Offset within Unknown2 points to the Unknown3 struct, measured from Unknown2 start position.</para></example>
	public partial class Cplx : Resource
	{

		#region constructors
		/// <summary>Blank constructor.</summary>
		public Cplx()
		{
			_type = ResourceType.Crft;
		}
		/// <summary>Creates a new instance from an existing opened file.</summary>
		/// <param name="stream">The opened LFD file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Cplx(FileStream stream, long filePosition)
		{
			read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an existing file.</summary>
		/// <param name="path">The full path to the unopened LFD file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Cplx(string path, long filePosition)
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
				_process(stream, filePosition);
			}
			catch (Exception x) { throw new LoadFileException(x); }
		}

		/// <summary>Swaps the endianess of the given value.</summary>
		/// <param name="original">The original value.</param>
		/// <returns>The opposite endianess, can go either way.</returns>
		short convertInt16(short original)
		{
			short result = (short)((original >> 8) | ((original & 0xFF) << 8));
			return result;
		}
		/// <summary>Swaps the endianess of the given value.</summary>
		/// <param name="buffer">Raw bytes.</param>
		/// <param name="offset">Starting index.</param>
		/// <returns>The opposite endianess, can go either way.</returns>
		short convertInt16(byte[] buffer, int offset)
		{
			short result = (short)(buffer[offset++] << 8 | buffer[offset]);
			return result;
		}
		/// <summary>Swaps the endianess of the given value.</summary>
		/// <param name="buffer">Raw bytes.</param>
		/// <param name="offset">Starting index.</param>
		/// <returns>The opposite endianess, can go either way.</returns>
		int convertInt32(byte[] buffer, int offset)
		{
			int result = buffer[offset++] << 24 | buffer[offset++] << 16 | buffer[offset++] << 8 | buffer[offset];
			return result;
		}

		#region public methods
		/// <summary>Processes raw data to populate the resource.</summary>
		/// <param name="raw">Raw byte data.</param>
		/// <param name="containsHeader">Whether or not <paramref name="raw"/> contains the resource Header information.</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> from the LFD is not <see cref="Resource.ResourceType.Cplx"/>.</exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Cplx) throw new ArgumentException("Raw header is not for a Cplx resource");

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

			int componentJumpStart = pos;
			short[] componentJumps = new short[componentCount];
			for (int i = 0; i < componentCount; i++)
			{
				componentJumps[i] = convertInt16(_rawData, pos);
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
					lodDistances.Add(convertInt32(_rawData, pos + lodCount * 6));
					lodJumps.Add(convertInt16(_rawData, pos + 4 + lodCount * 6));
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

					lod.MinimumBound = new Crft.Lod.Vertex16(_rawData, ref pos);
					lod.MinimumBound.X = convertInt16(lod.MinimumBound.X);
					lod.MinimumBound.Y = convertInt16(lod.MinimumBound.Y);
					lod.MinimumBound.Z = convertInt16(lod.MinimumBound.Z);
					lod.MaximumBound = new Crft.Lod.Vertex16(_rawData, ref pos);
					lod.MaximumBound.X = convertInt16(lod.MaximumBound.X);
					lod.MaximumBound.Y = convertInt16(lod.MaximumBound.Y);
					lod.MaximumBound.Z = convertInt16(lod.MaximumBound.Z);

					Crft.Lod.Vertex16[] vertices = new Crft.Lod.Vertex16[vertexCount];
					readOnly = new bool[vertexCount];
					for (int v = 0; v < vertexCount; v++)
					{
						vertices[v] = new Crft.Lod.Vertex16(_rawData, ref pos);
						vertices[v].X = convertInt16(vertices[v].X);
						vertices[v].Y = convertInt16(vertices[v].Y);
						vertices[v].Z = convertInt16(vertices[v].Z);
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
					lod.MeshVertices = new Indexer<Crft.Lod.Vertex16>(vertices, readOnly);
					Crft.Lod.Vector16[] normals = new Crft.Lod.Vector16[vertexCount];
					for (int v = 0; v < vertexCount; v++)
					{
						normals[v] = new Crft.Lod.Vector16(_rawData, ref pos);
						normals[v].X = convertInt16(normals[v].X);
						normals[v].Y = convertInt16(normals[v].Y);
						normals[v].Z = convertInt16(normals[v].Z);
					}
					lod.VertexNormals = new Indexer<Crft.Lod.Vector16>(normals, readOnly);

					normals = new Crft.Lod.Vector16[shapeCount];
					short[] shapeJumps = new short[shapeCount];
					int shapeJumpStart = pos;
					for (int s = 0; s < shapeCount; s++)
					{
						normals[s] = new Crft.Lod.Vector16(_rawData, ref pos);
						normals[s].X = convertInt16(normals[s].X);
						normals[s].Y = convertInt16(normals[s].Y);
						normals[s].Z = convertInt16(normals[s].Z);
						shapeJumps[s] = convertInt16(_rawData, pos);
						pos += 2;
					}
					Crft.Lod.Shape[] shapes = new Crft.Lod.Shape[shapeCount];
					for (int s = 0; s < shapeCount; s++)
					{
						shapes[s] = new Crft.Lod.Shape();
						pos = shapeJumpStart + s * 8 + shapeJumps[s];
						shapes[s].FaceNormal = normals[s];
						shapes[s].Type = _rawData[pos++];
						int len = (shapes[s].Type & 0x0F) * 2 + 3;
						byte[] data = new byte[len];
						readOnly = new bool[len];
						for (int i = 0; i < len; i++) readOnly[i] = true;
						ArrayFunctions.TrimArray(_rawData, pos, data);
						pos += len;
						shapes[s].Data = new Indexer<byte>(data, readOnly);
					}
					readOnly = new bool[shapeCount];
					for (int i = 0; i < shapeCount; i++) readOnly[i] = true;
					try
					{
						for (int s = 0; s < shapeCount; s++)
						{
							shapes[s].Unknown1 = _rawData[pos++];
							shapes[s].Unknown2 = convertInt16(_rawData, pos);
							pos += 2;
						}
					}
					catch { /* do nothing */ }
					lod.Shapes = new Indexer<Crft.Lod.Shape>(shapes, readOnly);

					/*short unkCount = convertInt16(_rawData, pos);
					pos += 2;
					if (unkCount != 0)
					{
						byte[] unkID = new byte[unkCount];
						short[] unkJumps = new short[unkCount];
						int unkJumpStart = pos;
						for (int u = 0; u < unkCount; u++)
						{
							unkID[u] = _rawData[pos++];
							unkJumps[u] = convertInt16(_rawData, pos);
							pos += 2;
						}
						Crft.Lod.UnknownData[] unks = new Crft.Lod.UnknownData[unkCount];
						for (int u = 0; u < unkCount; u++)
						{
							pos = unkJumpStart + u * 3 + unkJumps[u];
							unks[u] = new Crft.Lod.UnknownData
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
						lod.UnkData = new Indexer<Crft.Lod.UnknownData>(unks, readOnly);
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
	}
}
