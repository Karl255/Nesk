using System;
using Eto.Forms;

namespace Nesk.UI.Gtk
{
	class MainClass
	{
		[STAThread]
		public static void Main()
		{
			new Application(Eto.Platforms.Gtk).Run(new NeskWindow());
		}
	}
}
