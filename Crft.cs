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
 * [UPD] Redid types and now reads all data, fully defined
 * v3.0, 260821
 * [UPD] Indexers replaced with ReadOnlyCollection
 * v2.1, 221030
 * [NEW] operator to SHIP
 * [NEW] Creation of Lines during Decode
 * v2.0, 210309
 * [NEW] Created
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
	///   /* 0x01 */ byte Unknown1
	///   /* 0x02 */ byte NumVertices
	///   /* 0x03 */ byte Unknown2
	///   /* 0x04 */ byte NumShapes
	///   /* 0x05 */ byte[NumShapes] ColorIndices
	/// 			 Vertex16 MinimumBound
	/// 			 Vertex16 MaximumBound
	/// 			 Vertex16[NumVertices] MeshVertices
	/// 			 ShapeHeader[NumShapes]
	/// 			 Shape[NumShapes] MeshGeometry
	/// 			 UnkShapeData[NumShapes] // but only if Unknown0x01 is not 0x81?
	/// 			 short NumMarkingShapes
	/// 			 MarkHeader[NumMarkingShapes]
	/// 			 MarkShape[NumMarkingShapes]
	/// }
	/// 
	/// Vertex16 (size 0x6)
	/// {
	///   /* 0x00 */ short X
	///   /* 0x02 */ short Y
	///   /* 0x04 */ short Z
	/// }
	/// 
	/// ShapeHeader (size 0x8)
	/// {
	///   /* 0x00 */ Vertex16 FaceNormal
	///   /* 0x06 */ short Offset
	/// }
	/// 
	/// Shape
	/// {
	///   /* 0x00 */ byte Type
	///   #if (Type & 0xF == 2)
	///		/* 0x01 */ byte LineRadius
	///		/* 0x02 */ byte Unknown
	///		/* 0x03 */ Line Edge
	///	  #else
	///	    /* 0x01 */ Line[Type & 0xF] Edges
	///	  #endif
	///				 byte[3] Unknown
	/// }
	/// 
	/// Line (size 0x02)
	/// {
	///		/* 0x00 */ byte Vertex1ID
	///		/* 0x01 */ byte Vertex2ID
	/// }
	/// 
	/// UnkShapeData (size 0x3)
	/// {
	///   /* 0x00 */ byte // might be ShapeIndex
	///   /* 0x01 */ short
	/// }
	/// 
	/// MarkHeader (size 0x3)
	/// {
	///   /* 0x00 */ byte ID // might be ShapeIndex being modified
	///   /* 0x01 */ short Offset
	/// }
	/// 
	/// MarkShape
	/// {
	///   /* 0x00 */ byte MarkCount
	///   /* 0x01 */ byte[MarkCount] Colors
	///              MarkInfo[MarkCount]
	/// }
	/// 
	/// MarkInfo
	/// {
	///   /* 0x00 */ byte Type
	///   #if (Type == 1)
	///     /* 0x01 */ byte VertexCount
	///     /* 0x02 */ VertexInfo[VertexCount]
	///   #elseif (Type == 2 || (Type >= 0x11 && Type <= 0xFE)) // Line type of increasing thickness
	///     /* 0x01 */ VertexInfo[2]
	///   #elseif (Type == 0xFF)
	///     /* 0x01 */ int ClippingDistance
	///     /* 0x05 */ MarkInfo TheActualMarkInfo
	///   #else // Poly
	///     /* 0x01 */ VertexInfo[Type]
	///   #endif
	///	}
	/// 
	/// VertexInfo
	/// {
	///   /* 0x00 */ byte VertexID
	///   /* 0x01 */ byte LeftFactor
	///   /* 0x02 */ byte RightFactor
	/// }
	/// </code>
	/// <para>The first <i>Length</i> value is the remaining data after that SHORT, so it will always be (<see cref="Resource.Length"/>-2). It's in effect "JumpToNextResource".
	/// A couple basic values, the ShadingSets</br>
	/// The contents of the ShadingSets data isn't current known, but it's always a fixed size.
	/// That said, original CRFT resources always have a <i>NumShadingSets</i> value of zero, this is primarily known from reverse-engineering efforts by others.</para>
	/// 
	/// <para>The <i>ComponentsOffsets</i> values are jump offsets from the beginning of their respective value, such that if an offset value is at address p, the component will be at (p+offset).</para>
	/// 
	/// <para>The <i>LodHeader.Offset</i> is a jump offset from the beginning of the LodHeader object (i.e., address of LodHeader.Distance).
	/// Technically there isn't a defined length for the LOD arrays (due to the use of jumps), rather the last LOD will have a <see cref="Lod.Distance"/> of <b>0x7FFFFFFF</b>.<br/>
	/// However, for original LucasArts CRFT resources, LodHeader[0].Offset will always equal the length of the entire array.
	/// This means by reading the first record, the number of Lods will be (LodHeader[0].Offset / 6). Not guaranteed to work with custom craft, but I verified the originals.</para>
	/// 
	/// <para>In <i>LodMesh.MeshVertices</i>, if the top byte of a value is <b>0x7F</b> then it's repeating a previous vertex's value.
	/// The repeat will match the sub-type (X-X, Y-Y, Z-Z) and the appropriate index is calculated with the bottom byte, right-shifted once, and subtracted from the current index.
	/// E.g. if the current index is 5 and the Y value is 0x7F02, then it will be using MeshVertices[5 - (2 >> 1)].Y, or [4].Y.</para>
	/// 
	/// <para><i>ShapeHeader.Offset</i> is a jump offset from the beginning of the ShapeHeader object, similar to LodHeader.Offset.</para>
	/// 
	/// <para><i>Shape.Type</i> uses the bottom nibble for the number of lines, top nibble as a bitfield. The first bit is <see cref="Lod.Shape.IsTwoSided"/>, the second bit is <see cref="Lod.Shape.IsGouraudShaded"/>.
	/// The contents of the Shape aside from the Type can be considered as byte[3 + (numLines* 2)] (referred to here as "Data").
	/// If the number of vertices is 2, then Data is structured as the thickness of the Line, an unk value, and then the line itself in Data[2] and Data[3].
	/// Otherwise, it's simply an array of Lines, with the vertex indexes defined in Data[i * 2] and Data[i * 2 + 1].  Regardless of which Data format is used, it's followed by three unknown bytes.</para>
	/// 
	/// <para>After that there's some Unknown data which appears to be a byte and short and is read in as such, but only if <i>LodMesh.Signature</i> is not <b>0x81</b>.
	/// The first byte *appears* to be a ShapeIndex, though it's not always in order. That said, since its current purpose isn't known, they're read in order instead of being read into the Shape they *might* be referring to.
	/// E.g., If we assume these are ShapeIDs, in CONTAIN the order is 0, 5, 1, 4, 2, 3. The values are read into sequential Shapes (0-5) instead of hopping around.
	/// There's no special defaults to denote if the values were read or not, they'll simply be <b>0</b>; you must verify Signature to know if those are real.</para>
	/// 
	/// <para>Marking shapes can be details or FlightGroup colors. The <i>ID</i> value *might* be a ShapeIndex that it's modifying. Not always in order, not always the same quantity as normal Shapes.
	/// The Offset within MarkHeader points to the MarkShape struct, measured from the MarkHeader start position.</para>
	/// 
	/// <para>The shape itself is made of several individual Marks. Each Mark has a Color index.
	/// The <i>MarkInfo</i> array is another one that while above I've defined as a fixed size, the way the code handles is by not counting the Clipping variant (Type 0xFF) towards the total, so the array is really a dynamic length.
	/// That's why the Clipping variant has another MarkInfo nested in it above.<br/>
	/// The VertexID values point to the MeshVertices, and the Factors are used against the values to the left/right of the indicated vertex.<br/>
	/// C = V + ((U-V)L + (W-V)R)) / 16, where C is the Corner, V is the vertex, U is the vertex to the left (clockwise), W is the vertex to the right (counterclockwise), L and R are the factors.<br/>
	/// The Type 2 line is 6.25% thickness, though I don't know what that's a percentage of. Type 0x11 starts at thickness of 1, then it gets thicker from there, though again no details as to by how much.<br/>
	/// The Clipping Type 0xFF sets the distance beyond which subsequent markings are no longer visible, similar to a LOD.</para>
	/// 
	/// <para>While I did a lot of the reverse-engineering myself, some of the finer details, especially with marking, came from <a href="https://github.com/rob-pilkington/XWLoader/wiki/Craft-Model-Format">Ron Pilkington</a>.</para>
	/// </example>
	public partial class Crft : Resource
	{
		bool _isCft => _fileName.EndsWith(".CFT", StringComparison.OrdinalIgnoreCase);

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

		/// <summary>Clean up any resources being used.</summary>
		/// <param name="disposing"><see langword="true"/> if managed resources should be disposed; otherwise, <see langword="false"/>.</param>
		protected override void Dispose(bool disposing)
		{
			if (_disposed) return;

			if (disposing) { /* do nothing */ }
			foreach (var c in Components)
			{
				foreach (var lod in c.Lods)
				{
					lod.MeshVertices = null;
					foreach (var s in lod.Shapes)
					{
						s.Data = null;
						s.Lines = null;
					}
					lod.Shapes = null;
				}
				c.Lods = null;
			}
			Components = null;
			ShadingSets = null;
			base.Dispose(disposing);
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
			if (shadingCount != 0)
			{
				ReadOnlyCollection<byte>[] sets = new ReadOnlyCollection<byte>[shadingCount];
				int setLength = 16;
				for (int i = 0; i < shadingCount; i++)
				{
					byte[] shadingSet = new byte[setLength];
					ArrayFunctions.TrimArray(_rawData, pos, shadingSet);
					pos += setLength;
					sets[i] = Array.AsReadOnly(shadingSet);
				}
				ShadingSets = Array.AsReadOnly(sets);
			}
			else ShadingSets = null;

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
					lod.Signature = _rawData[pos++]; // need Signature to determine if unks are present
					lod.Unknown1 = _rawData[pos++];
					byte vertexCount = _rawData[pos++];
					lod.Unknown2 = _rawData[pos++];
					byte shapeCount = _rawData[pos++];
					byte[] colors = new byte[shapeCount];
					ArrayFunctions.TrimArray(_rawData, pos, colors);
					pos += shapeCount;

					lod.MinimumBound = new Lod.Vertex16(_rawData, ref pos);
					lod.MaximumBound = new Lod.Vertex16(_rawData, ref pos);

					Lod.Vertex16[] vertices = new Lod.Vertex16[vertexCount];
					for (int v = 0; v < vertexCount; v++)
					{
						vertices[v] = new Lod.Vertex16(_rawData, ref pos);

						for (int i = 0; i < 3; i++)
						{
							if ((vertices[v][i] & 0xFF00) == 0x7F00)
							{
								int delta = (vertices[v][i] & 0x00FF) >> 1;
								vertices[v][i] = vertices[v - delta][i];
							}
						}
					}
					lod.MeshVertices = Array.AsReadOnly(vertices);

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
						pos = shapeJumpStart + s * 8 + shapeJumps[s];
						shapes[s].Color = colors[s];
						shapes[s].FaceNormal = normals[s];
						shapes[s].Type = _rawData[pos++];
						int len = (shapes[s].Type & 0x0F) * 2 + 3;
						byte[] data = new byte[len];
						ArrayFunctions.TrimArray(_rawData, pos, data);
						pos += len;
						shapes[s].Data = Array.AsReadOnly(data);

                        byte shapeVertexCount = (byte)(shapes[s].Type & 0xF);
                        Lod.Line[] lines = new Lod.Line[shapeVertexCount / 2];	// TODO: possibly bug, need to verify this
						if (shapeVertexCount == 2)
							lines[0] = new Lod.Line(shapes[s].Data[2], shapes[s].Data[3]);
						else
							for (int ln = 0; ln < lines.Length; ln++)
								lines[ln] = new Lod.Line(shapes[s].Data[ln * 2], shapes[s].Data[(ln + 1) * 2]);	// TODO: probably bug, need to verify
						shapes[s].Lines = Array.AsReadOnly(lines);
                    }
					if (lod.Signature != 0x81)
						for (int s = 0; s < shapeCount; s++)
						{
							shapes[s].Unknown1 = _rawData[pos++];
							shapes[s].Unknown2 = BitConverter.ToInt16(_rawData, pos);
							pos += 2;
						}
					lod.Shapes = Array.AsReadOnly(shapes);

					short numMarkShapes = BitConverter.ToInt16(_rawData, pos);
					pos += 2;
					if (numMarkShapes != 0)
					{
						byte[] markIDs = new byte[numMarkShapes];
						short[] markJumps = new short[numMarkShapes];
						int markJumpStart = pos;
						for (int u = 0; u < numMarkShapes; u++)
						{
							markIDs[u] = _rawData[pos++];
							markJumps[u] = BitConverter.ToInt16(_rawData, pos);
							pos += 2;
						}
						Lod.MarkingShape[] markShapes = new Lod.MarkingShape[numMarkShapes];
						for (int u = 0; u < numMarkShapes; u++)
						{
							pos = markJumpStart + u * 3 + markJumps[u];
							markShapes[u] = new Lod.MarkingShape()
							{
								MarkCount = _rawData[pos++],
								ID = markIDs[u]
							};
							colors = new byte[markShapes[u].MarkCount];
							ArrayFunctions.TrimArray(_rawData, pos, colors);
							pos += colors.Length;
							markShapes[u].Colors = Array.AsReadOnly(colors);
							List<Lod.MarkInfo> infoList = new List<Lod.MarkInfo>();
							int markCount = 0;
							while (markCount < markShapes[u].MarkCount)
							{
								byte mvCount = 0;
								var info = new Lod.MarkInfo { Type = _rawData[pos++] };
								if (info.Type == 1) mvCount = _rawData[pos++];
								else if (info.Type == 0xFF)
								{
									info.ClippingDistance = BitConverter.ToInt32(_rawData, pos);
									pos += 4;
								}
								else if (info.Type == 2 || info.Type >= 0x11) mvCount = 2;
								else mvCount = info.Type;
								if (mvCount > 0)
								{
									Lod.MarkInfo.VertexInfo[] mvis = new Lod.MarkInfo.VertexInfo[mvCount];
									for (int i = 0; i < mvCount; i++)
										mvis[i] = new Lod.MarkInfo.VertexInfo
										{
											VertexID = _rawData[pos++],
											LeftFactor = _rawData[pos++],
											RightFactor = _rawData[pos++]
										};
									info.Vertices = Array.AsReadOnly(mvis);
								}
								infoList.Add(info);
								if (info.Type != 0xFF) markCount++;
							}
							markShapes[u].Marks = new ReadOnlyCollection<Lod.MarkInfo>(infoList);
						}
						lod.MarkShapes = new ReadOnlyCollection<Lod.MarkingShape>(markShapes);
					}
				}
			}
			Components = Array.AsReadOnly(components);
		}
		#endregion public methods

		/// <summary>Gets the components of the model.</summary>
		public ReadOnlyCollection<Component> Components { get; private set; }
		/// <summary>Gets the shading data.</summary>
		public ReadOnlyCollection<ReadOnlyCollection<byte>> ShadingSets { get; private set; }

        /// <summary>Transfers the wireframe data into a SHIP object.</summary>
        /// <param name="craft">The CRFT wireframe data</param>
        /// <returns>A SHIP with <u>only</u> the wireframe data from <see cref="Components"/>. <see cref="Ship.Unknown"/> is set to <b>0</b>,
        /// <see cref="Ship.Unknowns"/> and <see cref="Ship.ShadingSets"/> are both set to <see langword="null"/>.
		/// Each <see cref="Cplx.Lod.VertexNormals"/> will be also be set to <see langword="null"/>.</returns>
        public static implicit operator Ship(Crft craft)
        {
			var ship = new Ship
			{
				Name = craft.Name,
				Unknowns = null,
				Unknown = 0,
				ShadingSets = null
			};
			byte componentCount = (byte)craft.Components.Count;
            var components = new Ship.Component[componentCount];
            for (int c = 0; c < componentCount; c++)
            {
                byte lodCount = (byte)craft.Components[c].Lods.Count;
                components[c] = new Ship.Component(lodCount)
                {
                    MeshType = Ship.MeshType.Default,
                };

                var lods = new Cplx.Lod[lodCount];
                for (int i = 0; i < lodCount; i++)
                {
					lods[i] = new Cplx.Lod
					{
						Distance = craft.Components[c].Lods[i].Distance,
						MinimumBound = craft.Components[c].Lods[i].MinimumBound,
						MaximumBound = craft.Components[c].Lods[i].MaximumBound,
						MeshVertices = craft.Components[c].Lods[i].MeshVertices,
						Shapes = craft.Components[c].Lods[i].Shapes,
						VertexNormals = null
					};
                }
                components[c].Lods = Array.AsReadOnly(lods);
            }
            ship.Components = Array.AsReadOnly(components);

            return ship;
        }
    }
}
