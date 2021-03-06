
/*
FarNet.Tools for Far Manager
Copyright (c) 2006-2016 Roman Kuzmin
*/

using System;

namespace FarNet.Tools
{
	/// <summary>
	/// TODO
	/// </summary>
	public class HistoryMenu
	{
		readonly HistoryLog _history;

		/// <summary>
		/// TODO
		/// </summary>
		public IListMenu Menu { get; private set; }

		/// <summary>
		/// TODO
		/// </summary>
		public HistoryMenu(HistoryLog history)
		{
			_history = history;

			Menu = Far.Api.CreateListMenu();

			Menu.IncrementalOptions = PatternOptions.Substring;
			Menu.ScreenMargin = 2;
			Menu.SelectLast = true;
			Menu.Title = "History";
			Menu.UsualMargins = true;

			Menu.AddKey(KeyCode.R, ControlKeyStates.LeftCtrlPressed, OnDelete);
			Menu.AddKey(KeyCode.Delete, ControlKeyStates.None, OnDelete);
		}
		/// <summary>
		/// TODO
		/// </summary>
		public string Show()
		{
			// fill
			ResetItems(_history.ReadLines());

			// show
			if (!Menu.Show())
				return null;

			// selected
			return Menu.Items[Menu.Selected].Text;
		}
		void ResetItems(string[] lines)
		{
			Menu.Items.Clear();
			foreach (string s in lines)
			{
				if (string.IsNullOrEmpty(Menu.Incremental) || s.StartsWith(Menu.Incremental, StringComparison.OrdinalIgnoreCase))
					Menu.Add(s);
			}
		}
		void OnDelete(object sender, MenuEventArgs e)
		{
			var lines = _history.Update(null);
			if (lines.Length == Menu.Items.Count)
			{
				e.Ignore = true;
			}
			else
			{
				e.Restart = true;
				Menu.Selected = -1;

				ResetItems(lines);
			}
		}
	}
}
