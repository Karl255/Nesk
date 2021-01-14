using Nesk.Mappers;

namespace Nesk
{
	public static class Extensions
	{
		public static Cartridge ParseCartridge(this byte[] @this) => new Cartridge(@this);

		public static Nesk CreateConsole(this Cartridge @this) => new Nesk(@this);
	}
}
