/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2023 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 2.1
 */

/* CHANGE LOG
 * v2.1, 221030
 * [UPD] Now inherits
 * v2.0, 210309
 * [NEW] Created
 */

namespace Idmr.LfdReader
{
	public partial class Ship : Resource
	{
		/// <summary>Represents a complete mesh object.</summary>
		public class Component : Cplx.Component
		{
            /// <summary>Initialize the mesh with the specified number of Lods (levels of detail).</summary>
            /// <param name="lodCount">The count to create.</param>
            /// <remarks><see cref="Cplx.Component.Lods"/> is created with read only flags set.</remarks>
            public Component(int lodCount) : base(lodCount) { }

			/// <summary>Gets the assignment of the Component.</summary>
			public MeshType MeshType { get; internal set; }
		}
	}
}
