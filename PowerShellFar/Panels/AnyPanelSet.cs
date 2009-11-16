/*
PowerShellFar plugin for Far Manager
Copyright (C) 2006-2009 Roman Kuzmin
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.RegularExpressions;
using FarNet;

namespace PowerShellFar
{
	/// <include file='doc.xml' path='docs/pp[@name="AnyPanel"]/*'/>
	public abstract partial class AnyPanel
	{
		/// <summary>
		/// Invokes script handlers.
		/// </summary>
		internal void InvokeThisScript(ScriptBlock script, EventArgs e)
		{
			PSVariableIntrinsics psvi = A.Psf.Engine.SessionState.PSVariable;
			psvi.Set("this", this);
			psvi.Set("_", e);
			script.InvokeReturnAsIs();
		}

		#region Open
		ScriptBlock _Open;

		/// <summary>
		/// Handler to open a file (e.g. on [Enter]). $_ = <see cref="FileEventArgs"/>.
		/// </summary>
		public void SetOpen(ScriptBlock handler)
		{
			_Open = handler;
		}

		/// <summary>
		/// Opens a file (lookup, handler, virtual method).
		/// </summary>
		internal void UIOpenFile(FarFile file)
		{
			if (file == null)
				return;

			// lookup closer?
			if (UserWants == UserAction.Enter && _LookupCloser != null)
			{
				_LookupCloser(this, new FileEventArgs(file));
				UIEscape(false);
				return;
			}

			// default or external action
			if (_Open == null)
				OpenFile(file);
			else
				InvokeThisScript(_Open, new FileEventArgs(file));
		}

		/// <summary>
		/// Opens a file.
		/// </summary>
		public virtual void OpenFile(FarFile file)
		{
			if (file.Data == null)
				return;

			//! use try, e.g. Invoke-Item throws exception with any error action (PS bug?)
			try
			{
				// case: file system
				FileSystemInfo fi = Convert<FileSystemInfo>.From(file.Data);
				if (fi != null)
				{
					A.Psf.InvokeCode("Invoke-Item -LiteralPath $args[0] -ErrorAction Stop", fi.FullName);
					return;
				}
			}
			catch (RuntimeException ex)
			{
				A.Msg(ex);
			}
		}

		#endregion

		#region Edit
		ScriptBlock _Edit;

		/// <summary>
		/// Handler to edit a file (e.g. on [F4]). $_ = <see cref="FileEventArgs"/>.
		/// </summary>
		public void SetEdit(ScriptBlock value)
		{
			_Edit = value;
		}

		/// <summary>
		/// Opens a file (handler, virtual method).
		/// </summary>
		void UIEditFile(FarFile file, bool alternative)
		{
			if (file == null)
				return;
			
			if (_Edit == null)
				EditFile(file, alternative);
			else
				InvokeThisScript(_Edit, new FileEventArgs(file, alternative));
		}

		/// <summary>
		/// Edits a file. <c>Data</c> must not be null.
		/// Mind that <c>Data</c> can be wrapped by <c>PSObject</c>.
		/// </summary>
		/// <remarks>
		/// This class edits FS files only.
		/// </remarks>
		internal virtual void EditFile(FarFile file, bool alternative)
		{
			// no data, no job
			if (file.Data == null)
				return;

			// get file and open in internal\external editor
			FileInfo fi = Convert<FileInfo>.From(file.Data);
			if (fi != null)
			{
				if (alternative)
					Process.Start("Notepad", fi.FullName);
				else
					A.CreateEditor(fi.FullName).Open(OpenMode.None);
				return;
			}

			// source info
			PSObject data = PSObject.AsPSObject(file.Data);
			if (data.BaseObject.GetType().Name == "MatchInfo")
			{
				string path;
				if (!A.TryGetPropertyValue(data, "Path", out path))
					return;
				int lineNumber;
				if (!A.TryGetPropertyValue(data, "LineNumber", out lineNumber))
					return;
				Match[] matches;
				if (!A.TryGetPropertyValue(data, "Matches", out matches) || matches.Length == 0)
					return;

				IEditor editor = A.Far.CreateEditor();
				editor.DisableHistory = true;
				editor.FileName = path;
				TextFrame frame = new TextFrame();
				frame.Line = lineNumber - 1;
				editor.Frame = frame;
				editor.Open();
				frame.TopLine = frame.Line - Console.WindowHeight / 3;
				editor.Frame = frame;
				ILine line = editor.CurrentLine; // can be null if a file is already opened
				if (line != null)
				{
					int end = matches[0].Index + matches[0].Length;
					line.Pos = end;
					line.Select(matches[0].Index, end);
					editor.Redraw();
				}
			}
		}

		#endregion

		#region View
		ScriptBlock _View;

		/// <summary>
		/// Handler to view a file (e.g. on [F3]). $_ = <see cref="FileEventArgs"/>.
		/// </summary>
		public void SetView(ScriptBlock value)
		{
			_View = value;
		}

		/// <summary>
		/// View action (view all, handler, virtual method).
		/// </summary>
		internal void UIView()
		{
			FarFile file = _Panel.CurrentFile;
			if (file == null)
			{
				UIViewAll();
				return;
			}

			if (file.Data == null) //???
				return;

			if (_View == null)
				ViewFile(file);
			else
				InvokeThisScript(_View, new FileEventArgs(file));
		}

		/// <summary>
		/// Viewer of a file.
		/// </summary>
		void ViewFile(FarFile file)
		{
			// get file and open in internal viewer
			FileInfo fi = Convert<FileInfo>.From(file.Data);
			if (fi != null)
			{
				IViewer view = A.CreateViewer(fi.FullName);
				view.Open(OpenMode.None);
				return;
			}

			//! use `try` to delete a tmp file, error can be at root of cert (PS bug?)
			string tmp = A.Far.TempName();
			try
			{
				WriteFile(file, tmp);

				IViewer v = A.CreateViewer(tmp);
				v.DisableHistory = true;
				v.Title = file.Name;
				v.Open(OpenMode.None);
			}
			finally
			{
				File.Delete(tmp);
			}
		}

		#endregion

		#region ViewAll
		ScriptBlock _ViewAll;

		/// <summary>
		/// Handler to view all files (e.g. [F3] on "..").
		/// </summary>
		public void SetViewAll(ScriptBlock handler)
		{
			_ViewAll = handler;
		}

		/// <summary>
		/// View all files.
		/// </summary>
		void UIViewAll()
		{
			if (_ViewAll != null)
			{
				InvokeThisScript(_ViewAll, null);
				return;
			}

			string tmp = A.Far.TempName();
			try
			{
				A.Psf.InvokeCode("$args[0] | Format-Table -AutoSize -ea 0 | Out-File -FilePath $args[1]", ShownItems, tmp);

				IViewer v = A.CreateViewer(tmp);
				v.DisableHistory = true;
				v.Title = _Panel.Path;
				v.Open(OpenMode.None);
			}
			finally
			{
				File.Delete(tmp);
			}
		}

		#endregion
	}
}
