﻿using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.Shell;
using Pendletron.Vsix.Core.Wrappers;

namespace Pendletron.Vsix.LocateInTFS
{
	/// <summary>
	/// This is the class that implements the package exposed by this assembly.
	///
	/// The minimum requirement for a class to be considered a valid package for Visual Studio
	/// is to implement the IVsPackage interface and register itself with the shell.
	/// This package uses the helper classes defined inside the Managed Package Framework (MPF)
	/// to do it: it derives from the Package class that provides the implementation of the 
	/// IVsPackage interface and uses the registration attributes defined in the framework to 
	/// register itself and its components with the shell.
	/// </summary>
	// This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
	// a package.
	[PackageRegistration(UseManagedResourcesOnly = true)]
	// This attribute is used to register the informations needed to show the this package
	// in the Help/About dialog of Visual Studio.
	[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
	// This attribute is needed to let the shell know that this package exposes some menus.
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[Guid(GuidList.guidVisualStudio_LocateInTFS_VSIPPkgString)]
    [ProvideAutoLoad("{f1536ef8-92ec-443c-9ed7-fdadf150da82}")]
	public sealed class VisualStudio_LocateInTFS_VSIPPackage : Package
	{
		/// <summary>
		/// Default constructor of the package.
		/// Inside this method you can place any initialization code that does not require 
		/// any Visual Studio service because at this point the package object is created but 
		/// not sited yet inside Visual Studio environment. The place to do all the other 
		/// initialization is the Initialize method.
		/// </summary>
		public VisualStudio_LocateInTFS_VSIPPackage()
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
		}

		/////////////////////////////////////////////////////////////////////////////
		// Overriden Package Implementation

		/// <summary>
		/// Initialization of the package; this method is called right after the package is sited, so this is the place
		/// where you can put all the initilaization code that rely on services provided by VisualStudio.
		/// </summary>
		protected override void Initialize()
		{
			Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
			base.Initialize();
			/* */
			// Add our command handlers for menu (commands must exist in the .vsct file)
			OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
			if (null != mcs)
			{
				// Create the command for the menu item.
				CommandID menuCommandID = new CommandID(GuidList.guidVisualStudio_LocateInTFS_VSIPCmdSet, (int)PkgCmdIDList.cmdidLocateInTFS);
				
				OleMenuCommand menuItem = new OleMenuCommand(MenuItemCallback, menuCommandID);
				
				menuItem.BeforeQueryStatus += new EventHandler(queryStatusMenuCommand_BeforeQueryStatus);
				mcs.AddCommand(menuItem);
				

			}
		}

		private DTE2 _dteInstance = null;
		public DTE2 DTEInstance
		{
			get {
				if (_dteInstance == null)
				{
					_dteInstance = GetDTEService();
				}
				return _dteInstance;
			}
			set { _dteInstance = value; }
		}

		private DTE2 GetDTEService()
		{
			return (DTE2)this.GetService(typeof(DTE));
		}

        private void queryStatusMenuCommand_BeforeQueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand menuCommand = sender as OleMenuCommand;
			
            if (menuCommand != null)
            {
                string selectedPath = GetSelectedPathFromSolutionExplorer();
                bool isVersionControlled = false;
                try{
                    var ws = GetWorkspaceForPath(selectedPath);
                    if (ws != null){
                        isVersionControlled = true;
                    }
                }
                catch (Exception){
                    isVersionControlled = false;
                }

                menuCommand.Visible = isVersionControlled;
            }
        }

		private UIHierarchyItem GetSelectedUIHierarchy(UIHierarchy solutionExplorer)
		{
			object[] objArray = solutionExplorer.SelectedItems as object[];
			if (objArray != null && objArray.Length == 1)
				return objArray[0] as UIHierarchyItem;
			else
				return (UIHierarchyItem)null;
		}

		public T GetService<T>() where T : class
		{
			return GetService(typeof(T)) as T;
		}

		public string GetLocalPath(SelectedItem item)
		{
			string result = "";

			if (item.ProjectItem == null)
			{
				if (item.Project == null)
				{
					// If there's no ProjectItem and no Project then it's (probably?) the solution
					result = DTEInstance.Solution.FullName;
				}
				else
				{
					// If there's no ProjectItem but there is a Project then the Project node is selected
					result = item.Project.FullName;
				}
			}
			else
			{
				//Just selected a file
				result = item.ProjectItem.get_FileNames(0);
			}
			return result;
		}

		public string GetSelectedPathFromSolutionExplorer()
		{
			string localPath = "";
			if (DTEInstance.SelectedItems != null && DTEInstance.SelectedItems.Count > 0)
			{
				foreach (SelectedItem item in DTEInstance.SelectedItems)
				{
					localPath = GetLocalPath(item);
					if (!String.IsNullOrWhiteSpace(localPath))
					{
						break;
					}
				}
			}
			return localPath;
		}

        protected Workspace GetWorkspaceForPath(string localFilePath){
			HatPackage hat = new HatPackage();
			VersionControlServer vcServer = hat.GetVersionControlServer();
			Workspace workspace = vcServer.GetWorkspace(localFilePath);
            return workspace;
        }

		public void Locate(string localPath)
		{
			// Get the first selected item? _dte.
			if (String.IsNullOrEmpty(localPath)) return; // Throw an exception, log to output?

			HatPackage hat = new HatPackage();
			
			string localFilePath = localPath;
			string serverItem = "";
			try
			{
                var workspace = GetWorkspaceForPath(localFilePath);
				serverItem = workspace.TryGetServerItemForLocalItem(localFilePath);
			}
			catch (Exception)
			{

			}
			if (!String.IsNullOrEmpty(serverItem))
			{
				Assembly tfsVC = Assembly.Load("Microsoft.VisualStudio.TeamFoundation.VersionControl");
				//Type t = tfsVC.GetType("Microsoft.VisualStudio.TeamFoundation.VersionControl.HatPackage");
				// if the tool window hasn't been opened yet "explorer" will be null, so we make sure it has opened at least once via ExecuteCommand
				DTEInstance.ExecuteCommand("View.TfsSourceControlExplorer");
				Type explorer = tfsVC.GetType("Microsoft.VisualStudio.TeamFoundation.VersionControl.ToolWindowSccExplorer");

				var prop = explorer.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Static);
				object toolWindowSccExplorerInstance = prop.GetValue(null, null);
				if (toolWindowSccExplorerInstance != null)
				{
					var navMethod = toolWindowSccExplorerInstance.GetType().GetMethod("Navigate", BindingFlags.Default | BindingFlags.Instance | BindingFlags.NonPublic);
					if (navMethod != null)
					{
						navMethod.Invoke(toolWindowSccExplorerInstance, new object[] { serverItem });
					}
				}
			}
		}

		/// <summary>
		/// This function is the callback used to execute a command when the a menu item is clicked.
		/// See the Initialize method to see how the menu item is associated to this function using
		/// the OleMenuCommandService service and the MenuCommand class.
		/// </summary>
		private void MenuItemCallback(object sender, EventArgs e)
		{
			string selected = GetSelectedPathFromSolutionExplorer();
			Locate(selected);
		}
	}
}