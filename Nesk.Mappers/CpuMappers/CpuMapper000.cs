﻿using System;
using K6502Emu;

namespace Nesk.Mappers.CPUMappers
{
	public class CpuMapper000 : CpuMapper
	{
		private readonly byte[] PrgRom;
		private readonly int PrgRomAddressMask;
		private readonly byte[] PrgRam;
		private readonly int PrgRamAddressMask;

		public CpuMapper000(IAddressable<byte> ppu, IAddressable<byte> apu, Cartridge cartridge, Func<uint> inputReadCallback) : base(ppu, apu, inputReadCallback)
		{
			// make sure the PRG-ROM size is 16k or 32k
			if (cartridge.PrgRom.Length != 16 * 1024    // 16k
				&& cartridge.PrgRom.Length != 32 * 1024) // 32k
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

		public override byte this[int address]
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
