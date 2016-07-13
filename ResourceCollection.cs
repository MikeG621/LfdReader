/*
 * Idmr.LfdReader.dll, Library file to read and write LFD resource files
 * Copyright (C) 2009-2014 Michael Gaisser (mjgaisser@gmail.com)
 * Licensed under the MPL v2.0 or later
 * 
 * Full notice in help/Idmr.LfdReader.chm
 * Version: 1.1
 */

/* CHANGE LOG
 * v1.1, 141215
 * [UPD] changed license to MPL
 * v1.0
 */

using System;
using System.Collections.Generic;

namespace Idmr.LfdReader
{
	/// <summary>Collection object for the resources in an <see cref="LfdFile"/></summary>
	public class ResourceCollection : IEnumerable<Resource>
	{
		List<Resource> _items = null;
		string _lockedMessage = "Collection structure is currently locked (";

		#region constructors
		/// <summary>Creates a new empty Collection</summary>
		/// <remarks>Structure is unlocked, <see cref="Add"/>, <see cref="Insert"/> and <see cref="RemoveAt"/> can be used</remarks>
		public ResourceCollection()
		{
			CanEditStructure = true;
		}
		
		/// <summary>Creates a new Collection with multiple initial Resource placeholders</summary>
		/// <param name="quantity">Number of Resources to start with</param>
		/// <remarks>Structure is locked</remarks>
		/// <exception cref="ArgumentOutOfRangeException"><i>quantity</i> is not positive</exception>
		public ResourceCollection(int quantity)
		{
			if (quantity > 0) _items = new List<Resource>(quantity);
			else throw new ArgumentOutOfRangeException("quantity", "quantity must be positive");
			for (int i = 0; i < quantity; i++) _items.Add(new Resource());
		}
		
		/// <summary>Creates a new Collection with the specified structure</summary>
		/// <param name="category">Structure of file to initialize</param>
		/// <remarks>Using <see cref="LfdCategory.Normal"/> is the same as using the blank constructor, otherwise structure is locked.<br/>
		/// Battle Delt palette is defined by EMPIRE.PLTTstandard and TOURDESK.PLTTtoddesk.</remarks>
		public ResourceCollection(LfdFile.LfdCategory category)
		{
			if (category == LfdFile.LfdCategory.Battle)
			{
				_items = new List<Resource>(2);
				_items.Add(new Text());
				_items[0].Name = "battle#";
				_items.Add(new Delt());
				_items[1].Name = "b#gal";
			}
			else if (category == LfdFile.LfdCategory.Cockpit)
			{
				_items = new List<Resource>(3);
				_items.Add(new Panl(true));
				_items.Add(new Mask());
				_items.Add(new Pltt());
			}
			else CanEditStructure = true;
		}
		#endregion constructors
		
		#region public methods
		/// <summary>Finds the resource with the specified <see cref="Resource.Tag"/> value</summary>
		/// <param name="tag">User-defined data</param>
		/// <returns>The first matching Resource, otherwise <b>null</b></returns>
		public Resource GetResourceByTag(object tag)
		{
			for (int i = 0; i < Count; i++)
				if (_items[i].Tag != null && _items[i].Tag.ToString() == tag.ToString()) return _items[i];
			return null;
		}

		/// <summary>Finds the index of the resource with the specified <see cref="Resource.Tag"/> value</summary>
		/// <param name="tag">User-defined data</param>
		/// <returns>The index of the first matching Resource, otherwise <b>-1</b></returns>
		public int GetIndexByTag(object tag)
		{
			for (int i = 0; i < Count; i++)
				if (_items[i].Tag != null && _items[i].Tag.ToString() == tag.ToString()) return i;
			return -1;
		}

		/// <summary>Finds the resource with the specified <see cref="Resource.Tag"/> value and replaces it with <i>resource</i></summary>
		/// <param name="tag">The value to search for</param>
		/// <param name="resource">The new Resource</param>
		/// <remarks>If <i>tag</i> is not found, no action is taken.</remarks>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked and <see cref="Resource.Type"/> or <see cref="Resource.Name"/> have been changed</exception>
		public void SetByTag(object tag, Resource resource)
		{
			for (int i = 0; i < Count; i++)
				if (_items[i].Tag.ToString() == tag.ToString())
				{
					if (!CanEditStructure && (_items[i].Name != resource.Name || _items[i].Type != resource.Type))
						throw new InvalidOperationException(_lockedMessage + "SetByTag)");
					_items[i] = resource;
					_items[i].Tag = tag;
					break;
				}
		}

		/// <summary>Empties the Collection of entries</summary>
		/// <remarks>All existing resources are lost, <see cref="Count"/> will be <b>zero</b>.</remarks>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked</exception>
		public void Clear()
		{
			if (!CanEditStructure) throw new InvalidOperationException(_lockedMessage + "Clear)");
			_items.Clear();
		}
		
		/// <summary>Adds a new Resource to the end of the Collection</summary>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked</exception>
		public void Add() { _add(new Resource()); }

		/// <summary>Adds the given Resource to the end of the Collection</summary>
		/// <param name="resource">The Resource to be added</param>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked</exception>
		public void Add(Resource resource) { _add(resource); }
		
		/// <summary>Inserts a new Resource at the specified index</summary>
		/// <param name="index">Location of the Resource within the Collection</param>
		/// <exception cref="ArgumentOutOfRangeException">Invalid <i>index</i> value</exception>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked</exception>
		public void Insert(int index) { _insert(index, new Resource()); }

		/// <summary>Inserts the given Resource at the specified index</summary>
		/// <param name="index">Location of the Resource</param>
		/// <param name="resource">The Resource to be added</param>
		/// <exception cref="ArgumentOutOfRangeException">Invalid <i>index</i> value</exception>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked</exception>
		public void Insert(int index, Resource resource) { _insert(index, resource); }
		
		/// <summary>Deletes the Resource at the specified index</summary>
		/// <param name="index">The index of the Resource to be deleted</param>
		/// <exception cref="ArgumentOutOfRangeException">Invalid <i>index</i> value</exception>
		/// <exception cref="InvalidOperationException">Attempted to call when structure is locked<br/><b>-or-</b><br/>Collection is empty</exception>
		public void RemoveAt(int index) { _removeAt(index); }
		#endregion public methods
		
		#region public properties
		/// <summary>A single Resource within the collection</summary>
		/// <param name="index">The location within the collection</param>
		/// <exception cref="IndexOutOfRangeException">Invalid <i>index</i> value</exception>
		/// <exception cref="InvalidOperationException">Attempted to set when structure is locked and <see cref="Resource.Type"/> or <see cref="Resource.Name"/> have been changed</exception>
		public Resource this[int index]
		{
			get { return _items[index]; }
			set
			{
				if (!CanEditStructure && _items[index].Type != Resource.ResourceType.Undefined && (_items[index].Name != value.Name || _items[index].Type != value.Type))
					throw new InvalidOperationException(_lockedMessage + "Item)");
				_items[index] = value;
			}
		}
		
		/// <summary>A single Resource within the collection</summary>
		/// <param name="label">The identifying string of the Resource in the form of "<see cref="Type">TYPE</see><see cref="Name"/>"</param>
		/// <exception cref="ArgumentException">Resource not found</exception>
		/// <exception cref="InvalidOperationException">Attempted to set when structure is locked and <see cref="Resource.Type"/> or <see cref="Resource.Name"/> have been changed</exception>
		/// <returns>The Resource matching <i>label</i>, otherwise <b>null</b></returns>
		/// <remarks><i>label</i> is the same format as <see cref="Resource.ToString()"/></remarks>
		public Resource this[string label]
		{
			get
			{
				for (int i = 0; i < Count; i++)
					if (_items[i].ToString() == label) return _items[i];
				return null;
			}
			set
			{
				int index = -1;
				for (int i = 0; i < Count; i++)
					if (_items[i].ToString() == label) { index = i; break; }
				if (index == -1) throw new ArgumentException("label not found");
				if (!CanEditStructure && _items[index].Type != Resource.ResourceType.Undefined && (_items[index].Name != value.Name || _items[index].Type != value.Type))
					throw new InvalidOperationException(_lockedMessage + "Item)");
				_items[index] = value;
			}
		}
		
		/// <summary>Gets the number of objects in the Collection</summary>
		/// <remarks>If internal List is <b>null</b>, returns <b>-1</b></remarks>
		public int Count { get { return (_items == null ? -1 : _items.Count); } }
		
		/// <summary>Gets or sets whether or not the structure is unlocked</summary>
		/// <remarks>Default is <b>false</b>, set to <b>true</b> when initialized with <see cref="ResourceCollection()"/> or <see cref="LfdCategory.Normal"/>.<br/>
		///	When locked, <see cref="Resource.Type"/> and <see cref="Resource.Name"/> are read-only, Collection cannot be resized</remarks>
		public bool CanEditStructure { get; set; }
		#endregion public properties
		
		void _add(Resource item)
		{
			if (!CanEditStructure) throw new InvalidOperationException(_lockedMessage + "add)");
			if (_items == null) _items = new List<Resource>(1);
			_items.Add(item);
		}
		void _insert(int index, Resource item)
		{
			if (!CanEditStructure) throw new InvalidOperationException(_lockedMessage + "insert)");
			if (_items == null) _items = new List<Resource>(1);
			if (index >= 0 && index <= Count) _items.Insert(index, item);
			else throw new ArgumentOutOfRangeException("index", "Invalid index value, (0-" + Count + ")");
		}
		void _removeAt(int index)
		{
			if (!CanEditStructure) throw new InvalidOperationException(_lockedMessage + "remove)");
			if (Count == 0) throw new InvalidOperationException("Collection is already empty");
			if (index >= 0 && index < Count) _items.RemoveAt(index);
			else throw new ArgumentOutOfRangeException("index", "Invalid index value, (0-" + (Count - 1) + ")");
		}
		
		#region IEnumerable<Resource> Members
		/// <summary>Returns an enumerator that iterations through the collection</summary>
		/// <returns>The enumerator</returns>
		public IEnumerator<Resource> GetEnumerator()
		{
			return _items.GetEnumerator();
		}
		#endregion

		#region IEnumerable Members
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return _items.GetEnumerator();
		}
		#endregion
	}
}