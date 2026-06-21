/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2026 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 2.1+
 */

/* CHANGE LOG
 * [UPD] Indexers replaced with ReadOnlyCollection
 * v2.1, 221030
 * [UPD] Lods now has internal set
 * v2.0, 210309
 * [NEW] Created
 */

using System;
using System.Collections.ObjectModel;

namespace Idmr.LfdReader
{
	public partial class Cplx : Resource
    {
		/// <summary>Represents a complete mesh object.</summary>
		public class Component
		{
			/// <summary>Initialize the mesh with the specified number of Lods (levels of detail).</summary>
			/// <param name="lodCount">The count to create.</param>
			public Component(int lodCount)
			{
				Lod[] lods = new Lod[lodCount];
				bool[] readOnly = new bool[lodCount];
				for (int i = 0; i < lodCount; i++)
				{
					lods[i] = new Lod();
					readOnly[i] = true;
				}

				Lods = Array.AsReadOnly(lods);
			}

			/// <summary>Gets the Lods.</summary>
			public ReadOnlyCollection<Lod> Lods { get; internal set; }
		}

		/// <summary>Represents a single Level of Detail (LOD) mesh.</summary>
		public class Lod : Crft.Lod
		{
			/// <summary>Gets the normal vectors for each vertex.</summary>
			public ReadOnlyCollection<Vector16> VertexNormals { get; internal set; }
		}
	}
}
