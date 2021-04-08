using System;
using Nesk.Mappers;

namespace Nesk
{
	public static class Extensions
	{
		public static Cartridge ParseCartridge(this byte[] @this) => new(@this);

		public static Nesk CreateConsole(this Cartridge @this, Func<uint> readInputCallback) => new(@this, readInputCallback);
	}
}
