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
	/// <summary>Object for "SHIP" mesh resources.</summary>
	/// <remarks>This is the third iteration of craft mesh data, used in TIE. This format adds designations to mesh data such as MainHull or Engine, and is the precursor to the OPT format.<br/>
	/// Resource is read-only.</remarks>
	/// <seealso cref="Cplx"/>
	/// <seealso cref="Crft"/>
	/// <example>
	/// <h4>Raw Data definition</h4>
	/// <code>
	/// RawData
	/// {
	///   /* 0x00 */ short Length
	///   /* 0x02 */ byte[30] Unknown
	///   /* 0x20 */ byte NumComponents
	///   /* 0x21 */ byte NumShadingSets
	///   /* 0x22 */ short Unknown
	///   /* 0x24 */ ShadingSet[NumShadingSets]
	/// 			 MeshSettings[NumComponents]
	/// 			 Component[NumComponents]
	/// }
	/// 
	/// ShadingSet (size 0x6)
	/// {
	///   ???	// don't know what this does
	/// }
	/// 
	/// MeshSettings (size 0x40)
	/// {
	///   /* 0x00 */ short Type (enum)
	///   /* 0x2C */ short ComponentOffset
	///	}
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
	/// 			 Vertex16[NumVertices] VertexNormals
	/// 			 ShapeSettings[NumShapes]
	/// 			 Shape[NumShapes]    MeshGeometry
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
	///   #else
	///     /* 0x01 */ byte[]
	///     // I've seen Types 2-5 and 7, but don't know how the data is sized
	///   #endif
	/// }
	/// 
	/// Triplet
	/// {
	///   /* 0x00 */ byte
	///   /* 0x01 */ byte
	///   /* 0x02 */ byte
	/// }
	/// </code>
	/// <para>The first <i>Length</i> value is the remaining data after it, so it will always equal (<see cref="Resource.Length"/>-2).</para>
	/// <para>The <i>ComponentsOffsets</i> values are jump offsets from the beginning of their respective MeshSettings, such that if an offset value is at location (p+0x2C), the component will be at (p+offset).</para>
	/// <para>The <i>LodHeader.Offset</i> is a jump offset from the beginning of the LodHeader object, such that if LodHeader[i] is at location p, the Mesh will start at (p+offset).
	/// This also means that LodHeader[0].Offset will be the entire length of the array.
	/// There isn't a defined length for the LOD arrays, rather the last LOD will have a <see cref="Crft.Lod.Distance">Distance</see> of <b>0x7FFFFFFF</b>.</para>
	/// <para>In <i>LodMesh.MeshVertices</i>, if the top byte of a value is <b>0x7F</b> then it's repeating a previous vertex's value.
	/// The repeat will match the sub-type (X-X, Y-Y, Z-Z) and the appropriate index is calculated with the bottom byte, right-shifted once, and subtracted from the current index.
	/// E.g. if the current index is 5 and the Y value is 0x7F02, then it will be using MeshVertices[5 - (2 >> 1)].Y, or [4].Y.</para>
	/// <para><i>ShapeSettings.Offset</i> is a jump offset from the beginning of the ShapeSettings object, similar to LodHeader.Offset.</para>
	/// <para><see cref="Crft.Lod.Shape.Type">Shape.Type</see> uses the bottom nibble for the number of vertices, top nibble for type. <see cref="Crft.Lod.Shape.Data">Data</see> is length(3 + (numVertices* 2)).
	/// If the number of vertices is 2, then Data has a pair of vertex indexes for a line defined in Data[2] and Data[3].
	/// Otherwise, for each vertex there is a line, with the vertex indexes defined in Data[v * 2] and Data[(v + 1) * 2].</para>
	/// <para>After that there's some Unknown data which is currently not read into the class.
	/// The Offset within Unknown2 points to the Unknown3 struct, measured from Unknown2's start position.<br/>
	/// Some meshes are missing the <i>Unknown1</i> array. I've noticed this in SPECIES.LFD:SHIPCONTAIN, on a <i>Signature</i>=81 LOD.</para></example>
	public partial class Ship : Resource
	{
		public enum MeshType : short
		{
			Default,
			MainHull,
			Wing,
			Fuselage,
			GunTurret,
			SmallGun,
			Engine,
			Bridge,
			ShieldGen,
			EnergyGen,
			Launcher,
			CommSys,
			BeamSys,
			CommandVBeam,
			DockingPlat,
			LandingPlat,
			Hangar,
			CargoPod,
			MiscHull,
			Antenna,
			RotWing,
			RotGunTurret,
			RotLauncher,
			RotCommSys,
			RotBeamSys,
			RotCommandBeam,
			Custom1,
			Custom2,
			Custom3,
			Custom4,
			Custom5,
			Custom6
		}

		#region constructors
		/// <summary>Blank constructor.</summary>
		public Ship()
		{
			_type = ResourceType.Ship;
		}
		/// <summary>Creates a new instance from an existing opened file.</summary>
		/// <param name="stream">The opened LFD file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Ship(FileStream stream, long filePosition)
		{
			read(stream, filePosition);
		}
		/// <summary>Creates a new instance from an existing file.</summary>
		/// <param name="path">The full path to the unopened LFD file.</param>
		/// <param name="filePosition">The offset of the beginning of the resource.</param>
		/// <exception cref="LoadFileException">Typically due to file corruption.</exception>
		public Ship(string path, long filePosition)
		{
			FileStream stream = File.OpenRead(path);
			read(stream, filePosition);
			stream.Close();
		}
		#endregion

		void read(FileStream stream, long filePosition)
		{
			try
			{
				_process(stream, filePosition);
			}
			catch (Exception x) { throw new LoadFileException(x); }
		}

		/// <summary>Processes raw data to populate the resource.</summary>
		/// <param name="raw">Raw byte data.</param>
		/// <param name="containsHeader">Whether or not <paramref name="raw"/> contains the resource Header information.</param>
		/// <exception cref="ArgumentException">Header-defined <see cref="Type"/> from the LFD is not <see cref="Resource.ResourceType.Ship"/>.</exception>
		public override void DecodeResource(byte[] raw, bool containsHeader)
		{
			_decodeResource(raw, containsHeader);
			if (_type != ResourceType.Ship) throw new ArgumentException("Raw header is not for a Ship resource");

			int pos = 2;
			byte[] unks = new byte[30];
			bool[] readOnly;
			ArrayFunctions.TrimArray(_rawData, pos, unks);
			pos += unks.Length;
			readOnly = new bool[unks.Length];
			for (int i = 0; i < readOnly.Length; i++) readOnly[i] = true;
			Unknowns = new Indexer<byte>(unks, readOnly);
			byte componentCount = _rawData[pos++];
			Component[] components = new Component[componentCount];
			byte shadingCount = _rawData[pos++];
			Unknown = BitConverter.ToInt16(_rawData, pos);
			pos += 2;
			if (shadingCount != 0)
			{
				Indexer<byte>[] sets = new Indexer<byte>[shadingCount];
				int setLength = 6;
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
			short[] meshTypes = new short[componentCount];
			short[] componentJumps = new short[componentCount];
			for (int i = 0; i < componentCount; i++)
			{
				meshTypes[i] = BitConverter.ToInt16(_rawData, pos);
				componentJumps[i] = BitConverter.ToInt16(_rawData, pos + 0x2C);
				pos += 0x40;
			}
			for (int c = 0; c < componentCount; c++)
			{
				pos = componentJumpStart + c * 0x40 + componentJumps[c];
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

				components[c] = new Component(lodCount) { MeshType = (MeshType)meshTypes[c] };
				for (int l = 0; l < lodCount; l++)
				{
					Cplx.Lod lod = components[c].Lods[l];
					lod.Distance = lodDistances[l];
					pos = componentJumpStart + c * 0x40 + componentJumps[c] + l * 6 + lodJumps[l];

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
					lod.MaximumBound = new Crft.Lod.Vertex16(_rawData, ref pos);

					Crft.Lod.Vertex16[] vertices = new Crft.Lod.Vertex16[vertexCount];
					readOnly = new bool[vertexCount];
					for (int v = 0; v < vertexCount; v++)
					{
						vertices[v] = new Crft.Lod.Vertex16(_rawData, ref pos);
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
					}
					lod.VertexNormals = new Indexer<Crft.Lod.Vector16>(normals, readOnly);

					normals = new Crft.Lod.Vector16[shapeCount];
					short[] shapeJumps = new short[shapeCount];
					int shapeJumpStart = pos;
					for (int s = 0; s < shapeCount; s++)
					{
						normals[s] = new Crft.Lod.Vector16(_rawData, ref pos);
						shapeJumps[s] = BitConverter.ToInt16(_rawData, pos);
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
					//short unkCount;
					for (int i = 0; i < shapeCount; i++) readOnly[i] = true;
					try
					{
						for (int s = 0; s < shapeCount; s++)
						{
							shapes[s].Unknown1 = _rawData[pos++];
							shapes[s].Unknown2 = BitConverter.ToInt16(_rawData, pos);   // this throws in SHIPCONTAIN, effectively these values are missing
							pos += 2;
						}
						//unkCount = BitConverter.ToInt16(_rawData, pos);
					}
					catch
					{
						//unkCount = 0;
					}	// if it threw, assume it doesn't exist and skip the Unks since now we can't trust it.
					lod.Shapes = new Indexer<Crft.Lod.Shape>(shapes, readOnly);

					/*pos += 2;
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
						Crft.Lod.UnknownData[] unks2 = new Crft.Lod.UnknownData[unkCount];
						for (int u = 0; u < unkCount; u++)
						{
							pos = unkJumpStart + u * 3 + unkJumps[u];
							unks2[u] = new Crft.Lod.UnknownData
							{
								Type = _rawData[pos],
								Unknown = unkID[u]
							};
							byte[] data;
							if (unks2[u].Type == 1)
							{
								data = new byte[_rawData[pos + 2] * 3 + 2];
							}
							else
							{
								data = new byte[1];	// don't keep it, haven't figured out how the sizing is done
							}
							ArrayFunctions.TrimArray(_rawData, pos + 1, data);
							readOnly = new bool[data.Length];
							for (int i = 0; i < data.Length; i++) readOnly[i] = true;
							unks2[u].Data = new Indexer<byte>(data, readOnly);
						}
						readOnly = new bool[unkCount];
						for (int i = 0; i < unkCount; i++) readOnly[i] = true;
						lod.UnkData = new Indexer<Crft.Lod.UnknownData>(unks2, readOnly);
					}
					else { lod.UnkData = null; }*/
				}
			}
			readOnly = new bool[componentCount];
			for (int i = 0; i < componentCount; i++) readOnly[i] = true;
			Components = new Indexer<Component>(components, readOnly);
		}

		/// <summary>Gets the components of the model.</summary>
		/// <remarks>Each component is read-only.</remarks>
		public Indexer<Component> Components { get; private set; }
		/// <summary>Gets the shading data.</summary>
		/// <remarks>Each set is read-only.</remarks>
		public Indexer<Indexer<byte>> ShadingSets { get; private set; }

		/// <summary>Unknown values</summary>
		/// <remarks>Bytes 0x02 through 0x1F</remarks>
		public Indexer<byte> Unknowns { get; private set; }

		/// <summary>Unknown value</summary>
		/// <remarks>Byte 0x22</remarks>
		public short Unknown { get; private set; }
	}
}
