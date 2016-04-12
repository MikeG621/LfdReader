/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2016 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.2
 */

/* CHANGE LOG
 * v1.2, 
 * [UPD] Only calls _encode on children if they report being modified
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using System;
using System.IO;

namespace Idmr.LfdReader
{
	/// <summary>Object to manage LFD resource files</summary>
	public class LfdFile
	{
		Rmap _rmp = null;
		static string _cockpitRmapErrorMessage = "Cockpit LFDs do not contain RMAPs";
		ResourceCollection _resources = new ResourceCollection();
		string _filePath = "resource.lfd";
		string _tempPath { get { return _filePath + ".tmp"; } }
		LfdCategory _lfdCategory = LfdCategory.Normal;

		/// <summary>Preset Lfd structures</summary>
		public enum LfdCategory : byte {
			/// <summary>Typical LFD file, unlocked structure, no restrictions</summary>
			Normal,
			/// <summary>Cockpit view LFDs, locked structure, does not contain an RMAP</summary>
			Cockpit,
			/// <summary>Battle#.LFD files, locked structure</summary>
			Battle }
		
		#region constructors
		/// <summary>Populates with an existing *.lfd file</summary>
		/// <param name="stream">Opened *.lfd</param>
		public LfdFile(FileStream stream)
		{
			_read(stream);
		}
		/// <summary>Populates with an existing *.lfd file</summary>
		/// <param name="filePath">Full path to the unopened *.lfd</param>
		public LfdFile(string filePath)
		{
			FileStream stream = File.OpenRead(filePath);
			_read(stream);
			stream.Close();
		}
		/// <summary>Creates an empty file with the appropriate file structure</summary>
		/// <param name="category">Preset type</param>
		/// <remarks><see cref="Rmap"/> and <see cref="Resources"/> are initialized accordingly.</remarks>
		public LfdFile(LfdCategory category)
		{
			_resources = new ResourceCollection(category);
			if (category == LfdCategory.Battle) _rmp = new Rmap(category);
			_lfdCategory = category;
		}
		#endregion constructors

		#region public methods
		/// <summary>Initializes <see cref="Rmap"/> using the contents of the LfdFile. If Rmap is already defined, it is updated</summary>
		/// <remarks>Processes individual <see cref="Resource.EncodeResource"/> functions, thus updating all <see cref="Resource.RawData"/> properties</remarks>
		public void CreateRmap()
		{
			if (_lfdCategory == LfdCategory.Cockpit) throw new ArgumentException(_cockpitRmapErrorMessage);
			string name = null;
			if (_rmp != null) name = _rmp.Name;
			_encodeResources();
			if (_lfdCategory == LfdCategory.Battle) _rmp = new Rmap(_lfdCategory);
			else _rmp = new Rmap(_resources.Count);
			for (int i = 0; i < _rmp.NumberOfHeaders; i++)
			{
				_rmp.SubHeaders[i].Type = _resources[i].Type;
				_rmp.SubHeaders[i].Name = _resources[i].Name;
				_rmp.SubHeaders[i].Length = _resources[i].Length;
				_rmp.SubHeaders[i].Offset = Resource.HeaderLength +
					(i > 0 ? _rmp.SubHeaders[i - 1].Offset + _rmp.SubHeaders[i - 1].Length : 0);
			}
			if (name != null) _rmp.Name = name;
			_rmp.EncodeResource();
		}

		/// <summary>Writes the file to disk</summary>
		/// <exception cref="Idmr.Common.SaveFileException">An error occured, file remains unchanged</exception>
		public void Write()
		{
			//System.Diagnostics.Debug.WriteLine("Encoding Lfd...");
			if (_rmp != null) CreateRmap();
			else _encodeResources();
			FileStream stream = null;
			try
			{
				if (File.Exists(_filePath)) File.Copy(_filePath, _tempPath);	// create backup
				stream = File.OpenWrite(_filePath);
				BinaryWriter bw = new BinaryWriter(stream);
				//System.Diagnostics.Debug.WriteLine("Writing...");
				if (_rmp != null)
				{
					bw.Write((int)_rmp.Type);
					bw.Write(_rmp.Name.ToCharArray());
					stream.Position = Resource.LengthOffset;
					bw.Write(_rmp.Length);
					bw.Write(_rmp.RawData);
				}
				for(int i=0;i<_resources.Count;i++)
				{
					long pos = stream.Position;
					bw.Write((int)_resources[i].Type);
					bw.Write(_resources[i].Name.ToCharArray());
					stream.Position = pos + Resource.LengthOffset;
					bw.Write(_resources[i].Length);
					bw.Write(_resources[i].RawData);
				}
				stream.SetLength(stream.Position);
				stream.Close();
				File.Delete(_tempPath);	// delete backup if it exists
				//System.Diagnostics.Debug.WriteLine("Completed");
			}
			catch (Exception x)
			{
				//System.Diagnostics.Debug.WriteLine("Write failure");
				if (stream != null) stream.Close();
				if (File.Exists(_tempPath)) File.Copy(_tempPath, _filePath);	// restore backup if it exists
				File.Delete(_tempPath);	// delete backup if it exists
				throw new Common.SaveFileException(x);
			}
		}
		#endregion public methods

		#region public properties
		/// <summary>Resources contained within the file</summary>
		/// <remarks>Does <u>not</u> contain <see cref="Rmap"/> if applicable</remarks>
		public ResourceCollection Resources { get { return _resources; } set { _resources = value; } }
		
		/// <summary>Gets the full path to the file</summary>
		public string FilePath { get { return _filePath; } }
		/// <summary>Gets the file name and extension of the file</summary>
		public string FileName { get { return Common.StringFunctions.GetFileName(_filePath); } }
		/// <summary>Gets if <see cref="Rmap"/> is defined</summary>
		public bool HasRmap { get { return (_rmp != null); } }
		/// <summary>Gets or sets the Rmap resource for the file</summary>
		/// <exception cref="System.ArgumentException">Cannot set if file is defined by the <see cref="LfdCategory.Cockpit"/> preset</exception>
		public Rmap Rmap
		{
			get { return _rmp; }
			set
			{
				if (_lfdCategory == LfdCategory.Cockpit) throw new ArgumentException(_cockpitRmapErrorMessage);
				_rmp = value;
			}
		}
		#endregion public properties
		
		#region private methods
		/// <summary>Loops through Resources and calls the individual EncodeResource() functions</summary>
		void _encodeResources()
		{
			for (int i = 0; i < _resources.Count; i++) if (_resources[i]._isModifed) _resources[i].EncodeResource();
		}

		void _read(FileStream stream)
		{
			_filePath = stream.Name;
			//System.Diagnostics.Debug.WriteLine("Creating " + FileName);
			if (Resource.GetType(stream, 0) == Resource.ResourceType.Rmap)
			{
				_rmp = new Rmap(stream);
				//System.Diagnostics.Debug.WriteLine("Rmap created");
				_resources = new ResourceCollection(_rmp.NumberOfHeaders);
				for (int i = 0; i < _resources.Count; i++)
				{
					//System.Diagnostics.Debug.WriteLine("Create " + _rmp.SubHeaders[i].Type.ToString() + " " + _rmp.SubHeaders[i].Name + " (" + (i+1) + "/" + _rmp.NumberOfHeaders + ")");
					_assignResource(i, _rmp.SubHeaders[i].Type, stream, _rmp.SubHeaders[i].Offset);
				}
				if (_resources.Count == 2 && _resources[0].Name.StartsWith("battle") && _resources[1].Name.EndsWith("gal"))
					_lfdCategory = LfdCategory.Battle;
			}
			else if (Resource.GetType(stream, 0) == Resource.ResourceType.Panl)
			{
				//System.Diagnostics.Debug.WriteLine("cockpit LFD");
				_lfdCategory = LfdCategory.Cockpit;
				_resources = new ResourceCollection(3);
				_resources[0] = new Panl(stream, 0);
				_resources[1] = new Mask(stream, Resource.HeaderLength + _resources[0].Length);
				_resources[2] = new Pltt(stream, Resource.HeaderLength * 2 + _resources[0].Length + _resources[1].Length);
			}
			else
			{
				_resources = new ResourceCollection(1);
				_assignResource(0, Resource.GetType(stream, 0), stream, 0);
				//System.Diagnostics.Debug.WriteLine("Solo resource " + _resources[0].Type + " " + _resources[0].Name);
			}
		}

		void _assignResource(int index, Resource.ResourceType type, FileStream stream, long offset)
		{
			// commented out types redirect to Resource to read and capture _rawData
			if (type == Resource.ResourceType.Anim) _resources[index] = new Anim(stream, offset);
			else if (type == Resource.ResourceType.Blas || type == Resource.ResourceType.Voic) _resources[index] = new Blas(stream, offset);
			//TODO: else if (type == Resource.ResourceType.Bmap) _resources[index] = new Bmap(stream, offset);
			//TODO: else if (type == Resource.ResourceType.Cust) _resources[index] = new Cust(stream, offset);
			else if (type == Resource.ResourceType.Delt) _resources[index] = new Delt(stream, offset);
			else if (type == Resource.ResourceType.Film) _resources[index] = new Film(stream, offset);
			else if (type == Resource.ResourceType.Font) _resources[index] = new Font(stream, offset);
			//TODO: else if (type == Resource.ResourceType.Gmid) _resources[index] = new Gmid(stream, offset);
			else if (type == Resource.ResourceType.Mask) _resources[index] = new Mask(stream, offset);
			//TODO: else if (type == Resource.ResourceType.Mtrx) _resources[index] = new Mtrx(stream, offset);
			else if (type == Resource.ResourceType.Panl) _resources[index] = new Panl(stream, offset);
			else if (type == Resource.ResourceType.Pltt) _resources[index] = new Pltt(stream, offset);
			// skip Rmap
			//TODO: else if (type == Resource.ResourceType.Ship) _resources[index] = new Ship(stream, offset);
			else if (type == Resource.ResourceType.Text) _resources[index] = new Text(stream, offset);
			else if (type == Resource.ResourceType.Xact) _resources[index] = new Xact(stream, offset);
			else _resources[index] = new Resource(stream, offset);
		}
		#endregion private methods

        bool _isModified
        {
            get
            {
                for (int i = 0; i < _resources.Count; i++)
                    if (_resources[i]._isModifed) return true;

                return false;
            }
        }
	}
}