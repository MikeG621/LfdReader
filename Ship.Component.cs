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

using Idmr.Common;

namespace Idmr.LfdReader
{
	public partial class Ship : Resource
	{
		/// <summary>Represents a complete mesh object.</summary>
		public class Component
		{
			/// <summary>Initialize the mesh with the specified number of Lods (levels of detail).</summary>
			/// <param name="lodCount">The count to create.</param>
			/// <remarks><see cref="Lods"/> is created with read only flags set.</remarks>
			public Component(int lodCount)
			{
				Cplx.Lod[] lods = new Cplx.Lod[lodCount];
				bool[] readOnly = new bool[lodCount];
				for (int i = 0; i < lodCount; i++)
				{
					lods[i] = new Cplx.Lod();
					readOnly[i] = true;
				}

				Lods = new Indexer<Cplx.Lod>(lods, readOnly);
			}

			/// <summary>Gets the Lods.</summary>
			/// <remarks>Each Lod is read-only.</remarks>
			public Indexer<Cplx.Lod> Lods { get; }

			/// <summary>Gets the assignment of the Component.</summary>
			public MeshType MeshType { get; internal set; }
		}
	}
}
