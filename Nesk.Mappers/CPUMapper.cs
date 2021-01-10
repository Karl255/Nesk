using System;
using K6502Emu;

namespace Nesk.Mappers
{
	public abstract class CPUMapper : IAddressable<byte>
	{
		private readonly byte[] RAM = new byte[2048];
		private readonly IAddressable<byte> PPU;
		private readonly IAddressable<byte> APU;

		public CPUMapper(IAddressable<byte> ppu, IAddressable<byte> apu)
		{
			PPU = ppu;
			APU = apu;
		}

		public byte this[int address]
		{
			get => address switch
			{
				>= 0x0000 and <= 0x1fff => RAM[address & 0x07ff],            // 2k RAM
				>= 0x2000 and <= 0x3fff => PPU[(address - 0x2000) & 0x0007], // PPU registers

				//>= 0x4000 and <= 0x4017 => APU[(address - 0x4000) - 0x4000], // APU registers
				_ => 0
			};

			set => _ = address switch
			{
				>= 0x0000 and <= 0x1fff => RAM[address & 0x07ff] = value,            // 2k RAM
				>= 0x2000 and <= 0x3fff => PPU[(address - 0x2000) & 0x0007] = value, // PPU registers

				//>= 0x4000 and <= 0x4017 => APU[(address - 0x4000) - 0x4000] = value, // APU registers
				_ => 0
			};
		}

		public int AddressableSize => 64 * 1024;
		public bool IsReadonly { get; set; }
	}
}
