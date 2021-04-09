using K6502Emu;

namespace Nesk.Mappers
{
	public abstract class PpuMapper : IAddressable<byte>
	{
		private byte[] nametable { get; init; } = new byte[2048];
		private byte[] palettes { get; init; } = new byte[32];

		public virtual byte this[int address]
		{
			get => address switch
			{
				>= 0x2000 and <= 0x3eff => nametable[address & 0x07ff], // nametable; TODO: check this accross mappers and mirroring
				>= 0x3f00 and <= 0x3fff => (address & 0x3) == 0
					? palettes[address & 0x0f]                          // mirror access to color 0 of each sprite palette x with background palette x
					: palettes[address & 0x1f],                         // the rest of the plaette colors
				_ => 0
			};

			set => _ = address switch
			{
				>= 0x2000 and <= 0x3eff => nametable[address & 0x07ff] = value, // nametable
				>= 0x3f00 and <= 0x3fff => (address & 0x3) == 0
					? palettes[address & 0x0f] = value                          // mirror access to color 0 of each sprite palette x with background palette x
					: palettes[address & 0x1f] = value,                         // the rest of the plaette colors
				_ => 0
			};
		}

		public int AddressableSize { get; }
		public bool IsReadonly { get; set; }
	}
}
