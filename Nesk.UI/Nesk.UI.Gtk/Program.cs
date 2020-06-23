using System;
using Eto.Forms;

namespace Nesk.UI.Gtk
{
	class MainClass
	{
		[STAThread]
		public static void Main(string[] args)
		{
			new Application(Eto.Platforms.Wpf).Run(new NeskWindow());
		}
	}
}
