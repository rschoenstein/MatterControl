﻿/*
Copyright (c) 2015, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public class LibraryProviderSelector : LibraryProvider
	{
		private List<LibraryProvider> libraryProviders = new List<LibraryProvider>();
		private int selectedLibraryProvider = -1;

		public LibraryProviderSelector(string parentProviderKey)
			: base(parentProviderKey)
		{
			// put in the sqlite provider
			libraryProviders.Add(LibraryProviderSQLite.Instance);

			// and any directory providers (sd card provider, etc...)
			libraryProviders.Add(new LibraryProviderFileSystem(Path.Combine("C:\\", "Users", "LarsBrubaker", "Downloads"), "Downloads", this.ProviderKey));
			//#if __ANDROID__
			//libraryProviders.Add(new LibraryProviderFileSystem(ApplicationDataStorage.Instance.PublicDataStoragePath, "Downloads", this.ProviderKey));

			PrintItemCollection libraryCollection = new PrintItemCollection("Library Folder1", Path.Combine("C:\\", "Users", "LarsBrubaker", "AppData", "Local", "MatterControl", "Library"));
			//libraryProviders.Add(new LibraryProviderFileSystem(libraryCollection, "Library Folder2", this.ProviderKey));

			// Check for LibraryProvider factories and put them in the list too.
			PluginFinder<LibraryProviderPlugin> libraryProviderPlugins = new PluginFinder<LibraryProviderPlugin>();
			foreach (LibraryProviderPlugin libraryProviderPlugin in libraryProviderPlugins.Plugins)
			{
				libraryProviders.Add(libraryProviderPlugin.CreateLibraryProvider(this.ProviderKey));
			}

			providerLocationStack.Add(new PrintItemCollection("..", ProviderKey));
		}

		#region Overriden Abstract Methods

		private List<PrintItemCollection> providerLocationStack = new List<PrintItemCollection>();

		public override int CollectionCount
		{
			get
			{
				if (selectedLibraryProvider == -1)
				{
					return libraryProviders.Count;
				}
				else
				{
					return libraryProviders[selectedLibraryProvider].CollectionCount;
				}
			}
		}

		public override int ItemCount
		{
			get
			{
				if (selectedLibraryProvider == -1)
				{
					return 0;
				}
				else
				{
					return libraryProviders[selectedLibraryProvider].ItemCount;
				}
			}
		}

		public override string KeywordFilter
		{
			get
			{
				if (selectedLibraryProvider == -1)
				{
					return "";
				}
				else
				{
					return libraryProviders[selectedLibraryProvider].KeywordFilter;
				}
			}

			set
			{
				if (selectedLibraryProvider == -1)
				{
				}
				else
				{
					libraryProviders[selectedLibraryProvider].KeywordFilter = value;
				}
			}
		}

		public override string Name
		{
			get
			{
				return "Never visible";
			}
		}

		public override string ProviderKey
		{
			get
			{
				return LibraryProviderSelectorKey;
			}
		}

		public static string LibraryProviderSelectorKey
		{
			get
			{
				return "ProviderSelectorKey";
			}
		}

		public override void AddCollectionToLibrary(string collectionName)
		{
			if (selectedLibraryProvider == -1)
			{
				throw new NotImplementedException();
			}
			else
			{
				libraryProviders[selectedLibraryProvider].AddCollectionToLibrary(collectionName);
			}
		}

		public override void AddFilesToLibrary(IList<string> files, List<ProviderLocatorNode> providerSavePath, ReportProgressRatio reportProgress = null, RunWorkerCompletedEventHandler callback = null)
		{
			List<ProviderLocatorNode> subProviderSavePath;
			int libraryProviderToUseIndex = GetProviderIndex(providerSavePath, out subProviderSavePath);

			libraryProviders[libraryProviderToUseIndex].AddFilesToLibrary(files, subProviderSavePath, reportProgress, callback);
		}

		public override void AddItem(PrintItemWrapper itemToAdd)
		{
			throw new NotImplementedException();
		}

		public override PrintItemCollection GetCollectionItem(int collectionIndex)
		{
			if (selectedLibraryProvider == -1)
			{
				LibraryProvider provider = libraryProviders[collectionIndex];
				return new PrintItemCollection(provider.Name, provider.ProviderKey);
			}
			else
			{
				return libraryProviders[selectedLibraryProvider].GetCollectionItem(collectionIndex);
			}
		}

		public override PrintItemCollection GetParentCollectionItem()
		{
			if (selectedLibraryProvider == -1)
			{
				return null;
			}
			else
			{
				return libraryProviders[selectedLibraryProvider].GetParentCollectionItem();
			}
		}

		public override PrintItemWrapper GetPrintItemWrapper(int itemIndex)
		{
			if (selectedLibraryProvider == -1)
			{
				if (libraryProviders[0].ProviderKey != LibraryProviderSQLite.StaticProviderKey)
				{
					throw new Exception("It is expected these are the same.");
				}
				return libraryProviders[0].GetPrintItemWrapper(itemIndex);
			}
			else
			{
				return libraryProviders[selectedLibraryProvider].GetPrintItemWrapper(itemIndex);
			}
		}

		// A key,value list that threads into the current collection loos like "key0,displayName0|key1,displayName1|key2,displayName2|...|keyN,displayNameN".
		public override List<ProviderLocatorNode> GetProviderLocator()
		{
			if (selectedLibraryProvider == -1)
			{
				return new List<ProviderLocatorNode>();
			}
			else
			{
				List<ProviderLocatorNode> providerPathNodes = new List<ProviderLocatorNode>();
				bool first = true;

				for (int i = 0; i < providerLocationStack.Count; i++)
				{
					PrintItemCollection collection = providerLocationStack[i];
					if (first)
					{
						providerPathNodes.Add(new ProviderLocatorNode(collection.Key, collection.Name));
						first = false;
					}
					else
					{
						providerPathNodes.Add(new ProviderLocatorNode(collection.Key, collection.Name));
					}
				}

				return providerPathNodes;
			}
		}

		public override void RemoveCollection(string collectionName)
		{
			if (selectedLibraryProvider == -1)
			{
				throw new NotImplementedException();
			}
			else
			{
				libraryProviders[selectedLibraryProvider].RemoveCollection(collectionName);
			}
		}

		public override void RemoveItem(PrintItemWrapper printItemWrapper)
		{
			List<ProviderLocatorNode> subProviderSavePath;
			int libraryProviderToUseIndex = GetProviderIndex(printItemWrapper, out subProviderSavePath);

			libraryProviders[libraryProviderToUseIndex].RemoveItem(printItemWrapper);
		}

		public override void SaveToLibrary(PrintItemWrapper printItemWrapper, List<MeshGroup> meshGroupsToSave, List<ProviderLocatorNode> providerSavePath = null)
		{
			if (selectedLibraryProvider == -1)
			{
				throw new NotImplementedException();
			}
			else
			{
				List<ProviderLocatorNode> subProviderSavePath;
				int libraryProviderToUseIndex = GetProviderIndex(printItemWrapper, out subProviderSavePath);

				libraryProviders[libraryProviderToUseIndex].SaveToLibrary(printItemWrapper, meshGroupsToSave, subProviderSavePath);
			}
		}

		public override void SetCollectionBase(PrintItemCollection collectionBase)
		{
			// This logic may need to be move legitamately into the virtual functions of the providers rather than all
			// gathered up here. If you find that this is not working the way you want ask me. LBB
			if ((providerLocationStack.Count > 2
				&& collectionBase.Key == providerLocationStack[providerLocationStack.Count - 2].Key)
				|| (providerLocationStack.Count > 1
				&& selectedLibraryProvider != -1
				&& collectionBase.Key == libraryProviders[selectedLibraryProvider].GetParentCollectionItem().Key)
				)
			{
				providerLocationStack.RemoveAt(providerLocationStack.Count - 1);
			}
			else
			{
				providerLocationStack.Add(collectionBase);
			}

			if (collectionBase.Key == this.ProviderKey)
			{
				selectedLibraryProvider = -1;
			}
			else
			{
				bool wasSet = false;
				for (int i = 0; i < libraryProviders.Count; i++)
				{
					if (libraryProviders[i].ProviderKey == collectionBase.Key)
					{
						selectedLibraryProvider = i;
						wasSet = true;
						break;
					}
				}

				if (!wasSet)
				{
					libraryProviders[selectedLibraryProvider].SetCollectionBase(collectionBase);
				}
			}

			CollectionChanged.CallEvents(this, null);
		}

		private int GetProviderIndex(PrintItemWrapper printItemWrapper, out List<ProviderLocatorNode> subProviderSavePath)
		{
			List<ProviderLocatorNode> providerPath = printItemWrapper.PrintItem.GetLibraryProviderLocator();

			return GetProviderIndex(providerPath, out subProviderSavePath);
		}

		private int GetProviderIndex(List<ProviderLocatorNode> providerSavePath, out List<ProviderLocatorNode> subProviderSavePath)
		{
			subProviderSavePath = null;

			if (providerSavePath != null
				&& providerSavePath.Count > 1) // key 0 is this provider so we want to look at the next provider
			{
				for (int i = 0; i < libraryProviders.Count; i++)
				{
					if (libraryProviders[i].ProviderKey == providerSavePath[1].Key)
					{
						subProviderSavePath = new List<ProviderLocatorNode>(providerSavePath);
						subProviderSavePath.RemoveAt(0);
						return i;
					}
				}
			}

			return 0;
		}

		#endregion Overriden Abstract Methods
	}
}