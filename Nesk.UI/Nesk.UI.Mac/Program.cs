﻿using System;
using Eto.Forms;

namespace Nesk.UI.Mac
{
	class MainClass
	{
		[STAThread]
		public static void Main()
		{
			new Application(Eto.Platforms.Mac64).Run(new NeskWindow());
		}
	}
}
