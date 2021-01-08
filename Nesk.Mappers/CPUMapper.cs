using K6502Emu;

namespace Nesk.Mappers
{
	public abstract class CPUMapper : IAddressable<byte>
	{
		private byte[] RAM = new byte[2048];
		//private PPU PPU = new PPU();
		//private APU APU = new APU();

		public byte this[int address]
		{
			get => address switch
			{
				>= 0x0000 and <= 0x1fff => RAM[address & 0x07ff],              // 2k RAM

				//>= 0x2000 and <= 0x3fff => PPU[(address - 0x2000) & 0x0007], // PPU registers
				//>= 0x4000 and <= 0x4017 => APU[(address - 0x4000) - 0x4000], // APU registers
				_ => 0
			};

			set => _ = address switch
			{
				>= 0x0000 and <= 0x1fff => RAM[address & 0x07ff] = value,            // 2k RAM

				//>= 0x2000 and <= 0x3fff => PPU[(address - 0x2000) & 0x0007] = value, // PPU registers
				//>= 0x4000 and <= 0x4017 => APU[(address - 0x4000) - 0x4000] = value, // APU registers
				_ => 0
			};
		}

		public int AddressableSize => 64 * 1024;
		public bool IsReadonly { get; set; }
	}
}
