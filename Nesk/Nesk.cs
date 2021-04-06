using K6502Emu;
using Nesk.Mappers;

namespace Nesk
{
	public sealed class Nesk
	{
		public double FrameRate;

		private K6502 Cpu { get; init; }
		private Ppu Ppu { get; init; }
		private long TickCount = 0;

		public Nesk(Cartridge cartridge)
		{
			var ppuBus = cartridge.GetPPUMapper();
			var ppu = new Ppu(ppuBus);
			IAddressable<byte> apu = null; // TODO: implement this when APU is implemented

			var cpuBus = cartridge.GetCPUMapper(ppu, apu);
			var cpu = new K6502(cpuBus, false);

			FrameRate = cartridge.TimingMode == TimingMode.NTSC ? 29.97 : 25.00;
			Cpu = cpu;
			Ppu = ppu;
		}

		private void Tick()
		{
			TickCount++;
			TickCount %= 3;

			// the PPU clock is 3x faster than the CPU clock
			if (TickCount % 3 == 0)
				Cpu.Tick();

			Ppu.Tick();
		}

		public byte[] TickToNextFrame()
		{
			while (!Ppu.IsFrameReady)
			{
				Tick();
			}

			return Ppu.GetFrame();
		}

#if DEBUG
		public byte[] RenderPatternMemory(int palette) => Ppu.RenderPatternMemory(palette);

		/// <summary>
		/// Dumps the whole memory space to a byte array. PPU, APU and IO register locations are filled with <c>0xff</c>.
		/// </summary>
		/// <returns></returns>
		public byte[] DumpMemory()
		{
			byte[] dump = new byte[64 * 1024];

			var memory = Cpu.GetMemory;

			for (int i = 0; i < 0x2000; i++)
				dump[i] = memory[i];

			for (int i = 0x2000; i < 0x4020; i++)
				dump[i] = 0x00;

			for (int i = 0x4020; i < 0xffff; i++)
				dump[i] = memory[i];

			return dump;
		}
#endif
	}
}
