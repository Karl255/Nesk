using K6502Emu;

namespace Nesk.Mappers
{
	public abstract class PpuMapper : IAddressable<byte>
	{
		private byte[] nametable { get; init; } = new byte[2048];
		private byte[] palettes { get; init; } = { // supposed startup values inside the palette memory
			0x0f,0x01,0x00,0x01,
			0x00,0x02,0x02,0x0D,
			0x08,0x10,0x08,0x24,
			0x00,0x00,0x04,0x2C,
			0x09,0x01,0x34,0x03,
			0x00,0x04,0x00,0x14,
			0x08,0x3A,0x00,0x02,
			0x00,0x20,0x2C,0x08
		};

		public virtual byte this[int address]
		{
			get => address switch
			{
				>= 0x2000 and <= 0x3eff => nametable[address & 0x07ff], // nametable; TODO: check this accross mappers and mirroring
				>= 0x3f00 and <= 0x3fff => (address & 0b11) == 0b00
					? palettes[address & 0x0f]                          // mirror access to color 0 of each sprite palette x with background palette x
					: palettes[address & 0x1f],                         // the rest of the plaette colors
				_ => 0
			};

			set => _ = address switch
			{
				>= 0x2000 and <= 0x3eff => nametable[address & 0x07ff] = value, // nametable
				>= 0x3f00 and <= 0x3fff => (address & 0b11) == 0b00
					? palettes[address & 0x0f] = value                          // mirror access to color 0 of each sprite palette x with background palette x
					: palettes[address & 0x1f] = value,                         // the rest of the plaette colors
				_ => 0
			};
		}

		public int AddressableSize { get; }
		public bool IsReadonly { get; set; }
	}
}
