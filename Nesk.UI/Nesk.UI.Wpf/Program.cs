﻿using Eto.Forms;
using System;

namespace Nesk.UI.Wpf
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
