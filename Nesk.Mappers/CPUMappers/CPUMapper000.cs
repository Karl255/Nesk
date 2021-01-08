using System;

namespace Nesk.Mappers.CPUMappers
{
	public class CPUMapper000 : CPUMapper
	{
		private byte[] PrgRom;
		private int AddressMask = 0;

		public CPUMapper000(Cartridge cartridge)
		{
			// make sure the PRG-ROM size is 8k or 16k and set the appropriate mask (for mirroring when size is 8k)
			if (cartridge.PrgRom.Length == 16 * 1024) // 16k
				AddressMask = 0x3fff;
			else if (cartridge.PrgRom.Length == 8 * 1024) // 32k
				AddressMask = 0x7fff;
			else
				throw new Exception($"Malformed ROM file: invalid PRG-ROM size, expectd 16384 or 32768, got {cartridge.PrgRom.Length}");

			PrgRom = cartridge.PrgRom;

			/*
			if (cartridge.ChrRomSize != 0x2000) // 8k
				throw new Exception($"Malformed ROM file: invalid CHR-ROM size ({cartridge.ChrRom.Length})");

			ChrRom = cartridge.ChrRom;
			*/
		}

		public new byte this[int address] => address switch
		{
			>= 0x0000 and <= 0x7fff => base[address],
			>= 0x8000 and <= 0xffff => PrgRom[(address - 0x8000) & AddressMask],
			_ => 0
		};
	}
}
