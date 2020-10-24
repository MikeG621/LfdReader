﻿/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2020 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.2.2+
 */

/* CHANGE LOG
 * [ADD] Created
 */

using System;
using Idmr.Common;

namespace Idmr.LfdReader
{
	public partial class Crft : Resource
	{
		/// <summary>Represents a complete mesh object</summary>
		public class Component
		{
			/// <summary>Initialize the mesh with the specified number of Lods (levels of detail)</summary>
			/// <param name="lodCount">The count to create</param>
			/// <remarks><see cref="Lods"/> is created with read only flags set.</remarks>
			public Component(int lodCount)
			{
				Lod[] lods = new Lod[lodCount];
				bool[] readOnly = new bool[lodCount];
				for (int i = 0; i < lodCount; i++) readOnly[i] = true;

				Lods = new Indexer<Lod>(lods, readOnly);
			}

			/// <summary>Gets the Lods</summary>
			/// <remarks>Each Lod is read-only</remarks>
			public Indexer<Lod> Lods { get; }
		}

		/// <summary>Represents a single Level of Detail (LOD) mesh</summary>
		public class Lod
		{
			public int Distance { get; internal set; }

			/// <summary>At 0x1, after the 0x83 signature</summary>
			public byte Unknown1 { get; internal set; }
			/// <summary>At 0x3</summary>
			public byte Unknown2 { get; internal set; }
			/// <summary>Gets the color indices</summary>
			/// <remarks>Each value is read-only</remarks>
			public Indexer<byte> ColorIndices { get; internal set; }
			public Vertex16 MinimumBound { get; internal set; }
			public Vertex16 MaximumBound { get; internal set; }
			/// <summary>Gets the vertices for the Lod</summary>
			/// <remarks>Each vertex is read-only</remarks>
			public Indexer<Vertex16> MeshVertices { get; internal set; }
			/// <summary>Gets the Shapes for the Lod</summary>
			/// <remarks>Each shape is read-only</remarks>
			public Indexer<Shape> Shapes { get; internal set; }
			/// <summary>Gets the unknown data at the end of the Lod</summary>
			/// <remarks>Might be texture related? Each entry is read-only</remarks>
			public Indexer<UnknownData> UnkData { get; internal set; }

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

				public short X { get; internal set; }
				public short Y { get; internal set; }
				public short Z { get; internal set; }

				/// <summary>Provides quick access to the values</summary>
				/// <param name="index">0-2 for {X, Y, Z}</param>
				/// <returns>The appropriate value, otherwise silently returns <b>0</b></returns>
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

			public class Vector16 : Vertex16
			{
				internal Vector16(byte[] raw, ref int offset) : base(raw, ref offset) { }

				public short Magnitude => (short)Math.Sqrt(X * X + Y * Y + Z * Z);
			}

			public class Shape
			{
				public Vector16 FaceNormal { get; internal set; }
				public byte Type { get; internal set; }
				/// <summary>Gets the data array</summary>
				/// <remarks>Length is (Type &amp; 0x0F)*2 + 3. The array of pairs are Vertex indices, the remaining 3 are unknown.<br/>
				/// Each value is read-only</remarks>
				public Indexer<byte> Data { get; internal set; }
				/// <summary>From the separate array following the Shape collection</summary>
				/// <remarks>Looks unique among the other Shapes and can be 0 to ShapeCount, likely an ID value</remarks>
				public byte Unknown1 { get; internal set; }
				/// <summary>From the separate array following the Shape collection</summary>
				/// <remarks>Immediately follows Unknown1</remarks>
				public short Unknown2 { get; internal set; }
			}

			public class UnknownData
			{
				/// <summary>From the preceding jump array</summary>
				/// <remarks>Probably an ID value, looks unique and can be 0 to ShapeCount</remarks>
				public byte Unknown { get; internal set; }

				public byte Type { get; internal set; }
				/// <summary>Gets the data array</summary>
				/// <remarks>If Type==1, data is { Unk, ArrayCount } followed by 3*ArrayCount bytes<br/>
				/// If Type==2, data is 16 bytes.<br/>
				/// Each value is read-only</remarks>
				public Indexer<byte> Data { get; internal set; }
			}
		}
	}
}