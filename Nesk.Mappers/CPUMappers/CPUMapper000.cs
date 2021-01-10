using System;
using K6502Emu;

namespace Nesk.Mappers.CPUMappers
{
	public class CPUMapper000 : CPUMapper
	{
		private readonly byte[] PrgRom;
		private readonly int PrgRomAddressMask;
		private readonly byte[] PrgRam;
		private readonly int PrgRamAddressMask;

		public CPUMapper000(IAddressable<byte> ppu, IAddressable<byte> apu, Cartridge cartridge) : base(ppu, apu)
		{
			// make sure the PRG-ROM size is 8k or 16k
			if (cartridge.PrgRom.Length != 16 * 1024    // 16k
				|| cartridge.PrgRom.Length != 8 * 1024) // 32k
				throw new Exception($"Malformed ROM file: invalid PRG-ROM size, expectd 16384 or 32768, got {cartridge.PrgRom.Length}");

			PrgRom = cartridge.PrgRom;
			// for mirroring
			PrgRomAddressMask = PrgRom.Length - 1;

			// same for PRG-ROM
			if (cartridge.HasPrgRam)
			{
				PrgRam = new byte[cartridge.PrgRamSize];
				PrgRamAddressMask = PrgRam.Length - 1;
			}
		}

		public new byte this[int address]
		{
			get => address switch
			{
				>= 0x6000 and <= 0x7fff => PrgRam?[(address - 0x6000) & PrgRamAddressMask] ?? 0, // PRG-RAM
				>= 0x8000 and <= 0xffff => PrgRom[(address - 0x8000) & PrgRomAddressMask],       // PRG-ROM
				_ => base[address],
			};

			set => _ = address switch
			{
				>= 0x6000 and <= 0x7fff => PrgRam != null ? PrgRam[(address - 0x6000) & PrgRamAddressMask] = value : 0, // PRG-RAM
				_ => base[address] = value
			};
		}
	}
}
