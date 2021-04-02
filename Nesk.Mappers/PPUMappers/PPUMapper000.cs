using System;

namespace Nesk.Mappers.PPUMappers
{
	public class PpuMapper000 : PpuMapper
	{
		private readonly byte[] ChrRom;

		public PpuMapper000(Cartridge cartridge)
		{
			if (cartridge.ChrRom.Length != 8 * 1024) // 8k
				throw new Exception($"Malformed ROM file: invalid CHR-ROM size, expected 8192, got {cartridge.ChrRom.Length}");

			ChrRom = cartridge.ChrRom;
		}

		public override byte this[int address] => address switch
		{
			>= 0x0000 and <= 0x1fff => ChrRom[address],
			_ => base[address]
		};
	}
}
