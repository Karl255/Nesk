using K6502Emu;

namespace Nesk.Mappers
{
	public abstract class PPUMapper : IAddressable<byte>
	{
		private byte[] nametable = new byte[2048];
		private byte[] palettes = new byte[256];
		//private PPU PPU = new PPU();
		//private APU APU = new APU();

		public byte this[int address]
		{
			get => address switch
			{
				>= 0x2000 and <= 0x2fff => nametable[(address - 0x2000) & 0x07ff], // nametable
				>= 0x3f00 and <= 0x3fff => palettes[address - 0x3f00],             // palettes
				_ => 0
			};

			set => _ = address switch
			{
				>= 0x2000 and <= 0x2fff => nametable[(address - 0x2000) & 0x07ff] = value, // nametable
				>= 0x3f00 and <= 0x3fff => palettes[address - 0x3f00] = value,             // palettes
				_ => 0
			};
		}

		public int AddressableSize { get; }
		public bool IsReadonly { get; set; }
	}
}
