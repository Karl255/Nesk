using K6502Emu;

namespace Nesk.Mappers
{
	public abstract class PpuMapper : IAddressable<byte>
	{
		private readonly byte[] nametable = new byte[2048];
		private readonly byte[] palettes = new byte[32];

		public virtual byte this[int address]
		{
			get => address switch
			{
				>= 0x2000 and <= 0x3eff => nametable[address & 0x07ff], // nametable; TODO: check this accross mappers and mirroring
				>= 0x3f00 and <= 0x3fff => palettes[address & 0x1f],    // palettes
				_ => 0
			};

			set => _ = address switch
			{
				>= 0x2000 and <= 0x3eff => nametable[address & 0x07ff] = value, // nametable
				>= 0x3f00 and <= 0x3fff => palettes[address & 0x1f] = value,    // palettes
				_ => 0
			};
		}

		public int AddressableSize { get; }
		public bool IsReadonly { get; set; }
	}
}
