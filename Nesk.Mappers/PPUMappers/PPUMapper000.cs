using System;

namespace Nesk.Mappers.PPUMappers
{
	public class PpuMapper000 : PpuMapper
	{
		private byte[] Chr { get; init; }
		private bool IsRam { get; init; } = false;

		public PpuMapper000(Cartridge cartridge)
		{
			if (cartridge.ChrRomSize == 8 * 1024)
				Chr = cartridge.ChrRom;
			else if (cartridge.ChrRomSize == 0 && cartridge.ChrRamSize == 8 * 1024)
			{
				Chr = new byte[8 * 1024];
				IsRam = true;
			}
			else
				throw new Exception($"Malformed ROM file: invalid CHR-ROM size, expected 8192, got {cartridge.ChrRom.Length}");
		}

		public override byte this[int address]
		{
			get => address switch
			{
				>= 0x0000 and <= 0x1fff => Chr[address],
				_ => base[address]
			};

			set => _ = address switch
			{
				>= 0x0000 and <= 0x1fff => IsRam ? Chr[address] = value : 0, // only write if it's a RAM
				_ => base[address] = value
			};
		}
	}
}
