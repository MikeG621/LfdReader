/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2021 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 2.0
 */

/* CHANGE LOG
 * v2.0, 210309
 * [NEW] Created
 */

using Idmr.Common;

namespace Idmr.LfdReader
{
	public partial class Cplx : Resource
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
		public class Lod : Crft.Lod
		{
			/// <summary>Gets the normal vectors for each vertex.</summary>
			/// <remarks>Each vector is read-only.</remarks>
			public Indexer<Vector16> VertexNormals { get; internal set; }
		}
	}
}
