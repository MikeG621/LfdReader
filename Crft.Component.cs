/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2026 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 3.1
 */

/* CHANGE LOG
 * v3.1, 261107
 * [NEW] Added Vertex16.ToString and Vector16.ToStrings overrides
 * [NEW] Redid format, with Lines and Marking types
 * v3.0, 260821
 * [UPD] Indexers replaced with ReadOnlyCollection
 * v2.2, 230401
 * [NEW] Shape.IsTwoSided and Shape.IsGouraudShaded properties
 * v2.1, 221030
 * [NEW] Lod.Line class, and Shape.Lines property
 * v2.0, 210309
 * [NEW] Created
 */

using System;
using System.Collections.ObjectModel;

namespace Idmr.LfdReader
{
	public partial class Crft : Resource
	{
		/// <summary>Represents a complete mesh object.</summary>
		public class Component
		{
			/// <summary>Initialize the mesh with the specified number of Lods (levels of detail).</summary>
			/// <param name="lodCount">The count to create.</param>
			public Component(int lodCount)
			{
				Lod[] lods = new Lod[lodCount];
				for (int i = 0; i < lodCount; i++) lods[i] = new Lod();
				Lods = Array.AsReadOnly(lods);
			}

			/// <summary>Gets the Lods.</summary>
			public ReadOnlyCollection<Lod> Lods { get; internal set; }
		}

		/// <summary>Represents a single Level of Detail (LOD) mesh.</summary>
		public class Lod
		{
			/// <summary>Gets the distance at which the LOD becomes active.</summary>
			public int Distance { get; internal set; }

			/// <summary>Gets the Lod signature.</summary>
			/// <remarks>May also be a Type value, currently only known to affect if <see cref="Shape.Unknown1"/> and <see cref="Shape.Unknown2"/> are present.</remarks>
			public byte Signature { get; internal set; }
			/// <summary>At 0x1, after the signature.</summary>
			public byte Unknown1 { get; internal set; }
			/// <summary>At 0x3.</summary>
			public byte Unknown2 { get; internal set; }
			/// <summary>Gets the color indices.</summary>
			//public ReadOnlyCollection<byte> ColorIndices { get; internal set; }
			/// <summary>Gets the minimum value for the bounding box.</summary>
			public Vertex16 MinimumBound { get; internal set; }
			/// <summary>Gets the maximum value for the bounding box.</summary>
			public Vertex16 MaximumBound { get; internal set; }
			/// <summary>Gets the vertices for the Lod.</summary>
			public ReadOnlyCollection<Vertex16> MeshVertices { get; internal set; }
			/// <summary>Gets the Shapes for the Lod.</summary>
			public ReadOnlyCollection<Shape> Shapes { get; internal set; }
			/// <summary>Gets the marking data for the Lod</summary>
			/// <remarks>Each entry is read-only</remarks>
			public ReadOnlyCollection<MarkingShape> MarkShapes { get; internal set; } = null;

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

				/// <summary>Returns a string that represents the current object.</summary>
				/// <returns>A string formatted as "({X} {Y} {Z})".</returns>
				public override string ToString() => $"({X} {Y} {Z})";
			}

			/// <summary>Represents a direction in 3D space.</summary>
			/// <remarks>This is a derived class, only adds the <see cref="Magnitude"/> calculation to the inherited <see cref="Vertex16"/>.</remarks>
			public class Vector16 : Vertex16
			{
				internal Vector16(byte[] raw, ref int offset) : base(raw, ref offset) { }

				/// <summary>Gets the RSS length of the vector.</summary>
				public short Magnitude => (short)Math.Sqrt(X * X + Y * Y + Z * Z);

				/// <summary>Returns a string that represents the current object.</summary>
				/// <returns>A string formatted as "({X} {Y} {Z}) Mag: {Magnitude}".</returns>
				public override string ToString() => base.ToString() + " Mag: " + Magnitude;
			}

			/// <summary>Represents a single line or face within the mesh.</summary>
			public class Shape
			{
				/// <summary>Gets the color index of the shape.</summary>
				public byte Color { get; internal set; }

				/// <summary>Gets the normal vector of the shape.</summary>
				public Vector16 FaceNormal { get; internal set; }
				/// <summary>Gets the Shape's Type.</summary>
				public byte Type { get; internal set; }
                /// <summary>Gets the data array.</summary>
                /// <remarks>Length is (Type &amp; 0x0F)*2 + 3. The array of pairs are Vertex indices, the remaining 3 are unknown.</remarks>
				public ReadOnlyCollection<byte> Data { get; internal set; }
				/// <summary>Gets if the Shape is double-sided.</summary>
				/// <remarks>Top bit of <see cref="Type"/>, if <see langword="false"/> then the shape is single-sided.</remarks>
				public bool IsTwoSided => (Type & 0x80) == 0x80;
				/// <summary>Gets if the Shape uses Gouraud (interpolated) shading.</summary>
				/// <remarks>Second bit of <see cref="Type"/>, if <see langword="false"/> then the shape is flat shaded.</remarks>
				public bool IsGouraudShaded => (Type & 0x40) == 0x40;
				/// <summary>From the separate array following the Shape collection.</summary>
				/// <remarks>Looks unique among the other Shapes and can be 0 to ShapeCount, likely an ID value.</remarks>
				public byte Unknown1 { get; internal set; }
				/// <summary>From the separate array following the Shape collection.</summary>
				/// <remarks>Immediately follows Unknown1.</remarks>
				public short Unknown2 { get; internal set; }
				/// <summary>Gets the lines made up of vertex pairs from <see cref="Data"/>.</summary>
				public ReadOnlyCollection<Line> Lines { get; internal set; }
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

			/// <summary>Represents the data to produce FlightGroup markings on a mesh.</summary>
			public class MarkingShape
			{
				/// <summary>From the preceding jump array</summary>
				/// <remarks>Probably an ID value, looks unique and can be 0 to ShapeCount, could be the Index of the Shape being marked.</remarks>
				public byte ID { get; internal set; }

				/// <summary>Gets the number of markings for the shape.</summary>
				public byte MarkCount { get; internal set; }

				/// <summary>Gets the Color indexes used by the marks.</summary>
				public ReadOnlyCollection<byte> Colors { get; internal set; }

				/// <summary>Gets the info for the individual marks.</summary>
				public ReadOnlyCollection<MarkInfo> Marks { get; internal set; }
			}

			/// <summary>Represents the data used to define individual marks.</summary>
			public class MarkInfo
			{
				/// <summary>Gets the format for <see cref="Data"/>.</summary>
				public byte Type { get; internal set; }

				/// <summary>If <see cref="Type"/> is <b>0xFF</b>, returns the distance beyond which Marks that follow will not be drawn.<br/>
				/// Otherwise, <b>-1</b>.</summary>
				public int ClippingDistance { get; internal set; } = -1;

				/// <summary>Gets the vertices that define where the marks are drawn.</summary>
				public ReadOnlyCollection<VertexInfo> Vertices { get; internal set; } = null;

				/// <summary>Represents a single vertex reference within a mark.</summary>
				public struct VertexInfo
				{
					/// <summary>The ID of the vertex used, multiplied by 2.</summary>
					public byte VertexID;
					/// <summary>A multiplier in the position calc based on the difference to the previous vertex.</summary>
					public byte LeftFactor;
					/// <summary>A multiplier in the position calc based on the difference to the next vertex.</summary>
					public byte RightFactor;
				}
			}
		}
	}
}
