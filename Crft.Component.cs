﻿/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2023 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 2.2
 */

/* CHANGE LOG
 * [NEW] Shape.IsTwoSided and Shape.IsGouraudShaded properties
 * v2.1, 221030
 * [NEW] Lod.Line class, and Shape.Lines property
 * v2.0, 210309
 * [NEW] Created
 */

using System;
using Idmr.Common;

namespace Idmr.LfdReader
{
	public partial class Crft : Resource
	{
		/// <summary>Represents a complete mesh object.</summary>
		public class Component
		{
			/// <summary>Initialize the mesh with the specified number of Lods (levels of detail).</summary>
			/// <param name="lodCount">The count to create.</param>
			/// <remarks><see cref="Lods"/> is created with read only flags set.</remarks>
			public Component(int lodCount)
			{
				Lod[] lods = new Lod[lodCount];
				bool[] readOnly = new bool[lodCount];
				for (int i = 0; i < lodCount; i++)
				{
					lods[i] = new Lod();
					readOnly[i] = true;
				}

				Lods = new Indexer<Lod>(lods, readOnly);
			}

			/// <summary>Gets the Lods.</summary>
			/// <remarks>Each Lod is read-only.</remarks>
			public Indexer<Lod> Lods { get; }
		}

		/// <summary>Represents a single Level of Detail (LOD) mesh.</summary>
		public class Lod
		{
			/// <summary>Gets the distance at which the LOD becomes active.</summary>
			public int Distance { get; internal set; }

			/// <summary>At 0x1, after the signature.</summary>
			public byte Unknown1 { get; internal set; }
			/// <summary>At 0x3.</summary>
			public byte Unknown2 { get; internal set; }
			/// <summary>Gets the color indices.</summary>
			/// <remarks>Each value is read-only.</remarks>
			public Indexer<byte> ColorIndices { get; internal set; }
			/// <summary>Gets the minimum value for the bounding box.</summary>
			public Vertex16 MinimumBound { get; internal set; }
			/// <summary>Gets the maximum value for the bounding box.</summary>
			public Vertex16 MaximumBound { get; internal set; }
			/// <summary>Gets the vertices for the Lod.</summary>
			/// <remarks>Each vertex is read-only.</remarks>
			public Indexer<Vertex16> MeshVertices { get; internal set; }
			/// <summary>Gets the Shapes for the Lod.</summary>
			/// <remarks>Each shape is read-only.</remarks>
			public Indexer<Shape> Shapes { get; internal set; }
			// <summary>Gets the unknown data at the end of the Lod</summary>
			// <remarks>Might be texture related? Each entry is read-only</remarks>
			//public Indexer<UnknownData> UnkData { get; internal set; }

			/// <summary>Represents a single point in 3D space.</summary>
			public class Vertex16
			{
				internal Vertex16(byte[] raw, ref int offset)
				{
					X = BitConverter.ToInt16(raw, offset);
					offset += 2;
					Y = BitConverter.ToInt16(raw, offset);
					offset += 2;
					Z = BitConverter.ToInt16(raw, offset);
					offset += 2;
				}

				/// <summary>Gets the X value.</summary>
				public short X { get; internal set; }
				/// <summary>Gets the Y value.</summary>
				public short Y { get; internal set; }
				/// <summary>Gets the Z value.</summary>
				public short Z { get; internal set; }

				/// <summary>Provides quick access to the values.</summary>
				/// <param name="index">0-2 for {X, Y, Z}.</param>
				/// <returns>The appropriate value, otherwise silently returns <b>0</b>.</returns>
				public short this[int index]
				{
					get
					{
						if (index == 0) return X;
						else if (index == 1) return Y;
						else if (index == 2) return Z;
						else return 0;
					}
					internal set
					{
						if (index == 0) X = value;
						else if (index == 1) Y = value;
						else if (index == 2) Z = value;
					}
				}
			}

			/// <summary>Represents a direction in 3D space.</summary>
			/// <remarks>This is a derived class, only adds the <see cref="Magnitude"/> calculation to the inherited <see cref="Vertex16"/>.</remarks>
			public class Vector16 : Vertex16
			{
				internal Vector16(byte[] raw, ref int offset) : base(raw, ref offset) { }

				/// <summary>Gets the RSS length of the vector.</summary>
				public short Magnitude => (short)Math.Sqrt(X * X + Y * Y + Z * Z);
			}

			/// <summary>Represents a single line or face within the mesh.</summary>
			public class Shape
			{
				/// <summary>Gets the normal vector of the shape.</summary>
				public Vector16 FaceNormal { get; internal set; }
				/// <summary>Gets the Shape's Type.</summary>
				public byte Type { get; internal set; }
                /// <summary>Gets the data array.</summary>
                /// <remarks>Length is (Type &amp; 0x0F)*2 + 3. The array of pairs are Vertex indices, the remaining 3 are unknown.<br/>
                /// Each value is read-only.</remarks>
				public Indexer<byte> Data { get; internal set; }
				/// <summary>Gets if the Shape is double-sided.</summary>
				/// <remarks>Top bit of <see cref="Type"/>, if <b>false</b> then the shape is single-sided.</remarks>
                public bool IsTwoSided { get { return (Type & 0x80) == 0x80; } }
				/// <summary>Gets if the Shape uses Gouraud (interpolated) shading.</summary>
				/// <remarks>Second bit of <see cref="Type"/>, if <b>false</b> then the shape is flat shaded.</remarks>
				public bool IsGouraudShaded { get { return (Type & 0x40) == 0x40; } }
                /// <summary>From the separate array following the Shape collection.</summary>
                /// <remarks>Looks unique among the other Shapes and can be 0 to ShapeCount, likely an ID value.</remarks>
                public byte Unknown1 { get; internal set; }
				/// <summary>From the separate array following the Shape collection.</summary>
				/// <remarks>Immediately follows Unknown1.</remarks>
				public short Unknown2 { get; internal set; }
				/// <summary>Gets the lines made up of vertex pairs from <see cref="Data"/>.</summary>
				/// <remarks>Indices are read-only.</remarks>
				public Indexer<Line> Lines { get; internal set; }
			}

			/// <summary>Represents a single line within a mesh</summary>
			/// <remarks>The indices points to a <see cref="Vertex16"/> within <see cref="MeshVertices"/>.</remarks>
			public class Line
			{
				/// <summary>Initialize with the indicated vertices.</summary>
				/// <param name="vertex1">The index of the start vertex.</param>
				/// <param name="vertex2">The index of the end vertex</param>
				public Line (int vertex1, int vertex2)
				{
					Vertex1 = vertex1;
					Vertex2 = vertex2;
				}

				/// <summary>Gets the index of the starting vertex.</summary>
				public int Vertex1 { get; internal set; }
				/// <summary>Gets the index of the end vertex</summary>
				public int Vertex2 { get; internal set; }
			}

			// <summary>Represents an unknown data set at the end of the Mesh data</summary>
			/*public class UnknownData
			{
				/// <summary>From the preceding jump array</summary>
				/// <remarks>Probably an ID value, looks unique and can be 0 to ShapeCount</remarks>
				public byte Unknown { get; internal set; }

				/// <summary>Gets the type of the <see cref="Data"/> structure</summary>
				/// <remarks>Looks like this can either equal <b>1</b> or <b>2</b>.</remarks>
				public byte Type { get; internal set; }

				/// <summary>Gets the data array</summary>
				/// <remarks>If Type==1, data is { Unk, ArrayCount } followed by 3*ArrayCount bytes<br/>
				/// If Type==2, data is 16 bytes.<br/>
				/// Each value is read-only</remarks>
				public Indexer<byte> Data { get; internal set; }
			}*/
		}
	}
}
